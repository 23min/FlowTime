using System.Text.Json;
using FlowTime.Cli.Commands;
using FlowTime.Core.Configuration;

namespace FlowTime.Integration.Tests;

/// <summary>
/// End-to-end integration tests for the Time Machine CLI commands against the real
/// Rust engine binary. Skipped cleanly when the binary is not available.
/// </summary>
public class TimeMachineCliIntegrationTests
{
    private const string ValidYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
          - id: served
            kind: expr
            expr: "arrivals * 0.5"
        """;

    private static string? TryResolveEnginePath()
    {
        var solutionRoot = DirectoryProvider.FindSolutionRoot();
        if (solutionRoot is null) return null;
        var path = Path.Combine(solutionRoot, "engine", "target", "release", "flowtime-engine");
        return File.Exists(path) ? path : null;
    }

    private static (StringWriter stdout, StringWriter stderr) NewWriters() =>
        (new StringWriter(), new StringWriter());

    // ── validate ──────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidModel_ExitsZero()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(), new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal("analyse", doc.RootElement.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task Validate_InvalidModel_ExitsOne()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(),
            new StringReader("schemaVersion: 1\nnodes: []"),
            stdout, stderr);
        // Not a full valid model — schema requires grid + nodes; depending on tier
        // the exact path varies, but the exit code must signal invalid (1).
        Assert.Equal(1, code);
    }

    // ── sweep ─────────────────────────────────────────────────────

    [Fact]
    public async Task Sweep_EndToEnd_ReturnsCorrectSeries()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return; // skip if no binary

        var spec = new
        {
            modelYaml = ValidYaml,
            paramId = "arrivals",
            values = new[] { 10.0, 20.0, 30.0 },
            captureSeriesIds = new[] { "served" },
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var points = doc.RootElement.GetProperty("points").EnumerateArray().ToArray();
        Assert.Equal(3, points.Length);

        // served = arrivals * 0.5; verify point 0 (arrivals=10 → served=5) and point 2 (arrivals=30 → served=15)
        var served0 = points[0].GetProperty("series").GetProperty("served")
            .EnumerateArray().Select(e => e.GetDouble()).ToArray();
        Assert.All(served0, v => Assert.Equal(5.0, v, precision: 6));

        var served2 = points[2].GetProperty("series").GetProperty("served")
            .EnumerateArray().Select(e => e.GetDouble()).ToArray();
        Assert.All(served2, v => Assert.Equal(15.0, v, precision: 6));
    }

    [Fact]
    public async Task Sweep_NoSessionFlag_StillProducesCorrectResults()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        var spec = new
        {
            modelYaml = ValidYaml,
            paramId = "arrivals",
            values = new[] { 10.0 },
            captureSeriesIds = new[] { "served" },
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", enginePath, "--no-session"],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        // Can't parse served series the same way (per-eval path uses full IDs), but
        // we can at least verify the result is valid JSON with the expected structure.
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Single(doc.RootElement.GetProperty("points").EnumerateArray());
    }

    // ── optimize ──────────────────────────────────────────────────

    [Fact]
    public async Task Optimize_ConvergesOnBowlFunction()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // Minimize |served - 7| by varying arrivals in [0, 100]. |x| = MAX(x, -x).
        // served = arrivals * 0.5, so the optimum is at arrivals = 14.
        var modelYaml = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10, 10, 10]
              - id: served
                kind: expr
                expr: "arrivals * 0.5"
              - id: residual
                kind: expr
                expr: "MAX(served - 7, 7 - served)"
            """;
        var spec = new
        {
            modelYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "residual",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
            tolerance = 0.01,
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await OptimizeCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());
        using var doc = JsonDocument.Parse(stdout.ToString());
        var paramValues = doc.RootElement.GetProperty("paramValues");
        var optArrivals = paramValues.GetProperty("arrivals").GetDouble();
        Assert.True(Math.Abs(optArrivals - 14.0) < 0.5,
            $"Expected arrivals≈14, got {optArrivals}");
    }

    // ── sensitivity end-to-end ────────────────────────────────────

    [Fact]
    public async Task Sensitivity_EndToEnd_ReturnsPerParamDerivatives()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // served = arrivals * 0.5 → ∂served/∂arrivals = 0.5 (mean).
        var spec = new
        {
            modelYaml = ValidYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "served",
            perturbation = 0.05,
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await SensitivityCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var points = doc.RootElement.GetProperty("points").EnumerateArray().ToArray();
        Assert.Single(points);
        var gradient = points[0].GetProperty("gradient").GetDouble();
        Assert.Equal(0.5, gradient, precision: 4);
    }

    // ── goal-seek end-to-end ──────────────────────────────────────

    [Fact]
    public async Task GoalSeek_EndToEnd_ConvergesOnTarget()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // Find the arrivals value that drives served (mean) to 25.
        // served = arrivals * 0.5 → target arrivals = 50.
        var spec = new
        {
            modelYaml = ValidYaml,
            paramId = "arrivals",
            metricSeriesId = "served",
            target = 25.0,
            searchLo = 0.0,
            searchHi = 200.0,
            tolerance = 0.1,
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await GoalSeekCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("converged").GetBoolean());
        var paramValue = doc.RootElement.GetProperty("paramValue").GetDouble();
        Assert.Equal(50.0, paramValue, precision: 1);
    }

    /// <summary>
    /// Confirms the additive <c>trace</c> field added by D-2026-04-21-034 appears in the
    /// CLI JSON passthrough for both goal-seek and optimize. No CLI-side code change is
    /// required — the field flows through automatically via the shared JSON options.
    /// </summary>
    [Fact]
    public async Task GoalSeek_JsonOutput_IncludesTraceField()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        var spec = new
        {
            modelYaml = ValidYaml,
            paramId = "arrivals",
            metricSeriesId = "served",
            target = 25.0,
            searchLo = 0.0,
            searchHi = 200.0,
            tolerance = 0.1,
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await GoalSeekCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("trace", out var trace),
            "CLI JSON output must include 'trace' field.");
        Assert.Equal(JsonValueKind.Array, trace.ValueKind);
        // At minimum the two boundary evaluations must be present.
        Assert.True(trace.GetArrayLength() >= 2);

        // Spot-check the first entry's camelCase shape.
        var first = trace[0];
        Assert.True(first.TryGetProperty("iteration", out _));
        Assert.True(first.TryGetProperty("paramValue", out _));
        Assert.True(first.TryGetProperty("metricMean", out _));
        Assert.True(first.TryGetProperty("searchLo", out _));
        Assert.True(first.TryGetProperty("searchHi", out _));
    }

    [Fact]
    public async Task Optimize_JsonOutput_IncludesTraceField()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // Same bowl fixture as Optimize_ConvergesOnBowlFunction.
        var modelYaml = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10, 10, 10]
              - id: served
                kind: expr
                expr: "arrivals * 0.5"
              - id: residual
                kind: expr
                expr: "MAX(served - 7, 7 - served)"
            """;
        var spec = new
        {
            modelYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "residual",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
            tolerance = 0.01,
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await OptimizeCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("trace", out var trace),
            "CLI JSON output must include 'trace' field.");
        Assert.Equal(JsonValueKind.Array, trace.ValueKind);
        Assert.True(trace.GetArrayLength() >= 1);

        var first = trace[0];
        Assert.True(first.TryGetProperty("iteration", out _));
        Assert.True(first.TryGetProperty("paramValues", out var pv));
        Assert.Equal(JsonValueKind.Object, pv.ValueKind);
        Assert.True(pv.TryGetProperty("arrivals", out _));
        Assert.True(first.TryGetProperty("metricMean", out _));
    }

    // ── runtime / engine errors (exit 3) ──────────────────────────

    [Fact]
    public async Task Sweep_EngineCompileError_ExitsThree()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // Model uses a function the engine doesn't support — surfaces as an
        // InvalidOperationException from the evaluator → exit 3 (engine error).
        var badModel = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10, 10, 10]
              - id: bogus
                kind: expr
                expr: "nonexistent_function(arrivals)"
            """;
        var spec = new
        {
            modelYaml = badModel,
            paramId = "arrivals",
            values = new[] { 10.0 },
        };
        var specJson = JsonSerializer.Serialize(spec);

        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", enginePath],
            new StringReader(specJson), stdout, stderr);

        Assert.Equal(3, code);
        Assert.NotEmpty(stderr.ToString());
        Assert.Empty(stdout.ToString());
    }

    // ── output to file ───────────────────────────────────────────

    [Fact]
    public async Task Validate_OutputToFile_WritesJsonFile()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var (stdout, stderr) = NewWriters();
            var code = await ValidateCommand.ExecuteAsync(
                ["-o", tempPath], new StringReader(ValidYaml), stdout, stderr);
            Assert.Equal(0, code);

            // Stdout should be empty; file has the JSON.
            Assert.Empty(stdout.ToString());
            using var doc = JsonDocument.Parse(File.ReadAllText(tempPath));
            Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public async Task Sweep_OutputToFile_WritesJsonFile()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        var spec = new
        {
            modelYaml = ValidYaml,
            paramId = "arrivals",
            values = new[] { 10.0 },
            captureSeriesIds = new[] { "served" },
        };
        var specJson = JsonSerializer.Serialize(spec);
        var outPath = Path.GetTempFileName();
        try
        {
            var (stdout, stderr) = NewWriters();
            var code = await SweepCommand.ExecuteAsync(
                ["--engine", enginePath, "-o", outPath],
                new StringReader(specJson), stdout, stderr);
            Assert.Equal(0, code);
            Assert.Empty(stdout.ToString());

            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            Assert.Single(doc.RootElement.GetProperty("points").EnumerateArray());
        }
        finally { File.Delete(outPath); }
    }
}
