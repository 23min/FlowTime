using System.Text.Json;
using FlowTime.Cli.Commands;

namespace FlowTime.Cli.Tests.Commands;

/// <summary>
/// Common-behavior tests shared by the four analysis commands (sweep, sensitivity,
/// goal-seek, optimize). These exercise the <c>AnalysisCliRunner</c> by driving it
/// through <c>SweepCommand.ExecuteAsync</c> — all four commands share the same runner,
/// so coverage of one covers the shared paths. Command-specific integration tests live
/// in <c>FlowTime.Integration.Tests</c>.
/// </summary>
public class AnalysisCommandTests
{
    private static (StringWriter stdout, StringWriter stderr) NewWriters() =>
        (new StringWriter(), new StringWriter());

    // ── Help paths for each of the four commands ───────────────────

    [Fact]
    public async Task Sweep_Help_PrintsUsage()
    {
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--help"], new StringReader(""), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Contains("flowtime sweep", stdout.ToString());
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public async Task Sensitivity_Help_PrintsUsage()
    {
        var (stdout, stderr) = NewWriters();
        var code = await SensitivityCommand.ExecuteAsync(
            ["--help"], new StringReader(""), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Contains("flowtime sensitivity", stdout.ToString());
    }

    [Fact]
    public async Task GoalSeek_Help_PrintsUsage()
    {
        var (stdout, stderr) = NewWriters();
        var code = await GoalSeekCommand.ExecuteAsync(
            ["--help"], new StringReader(""), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Contains("flowtime goal-seek", stdout.ToString());
    }

    [Fact]
    public async Task Optimize_Help_PrintsUsage()
    {
        var (stdout, stderr) = NewWriters();
        var code = await OptimizeCommand.ExecuteAsync(
            ["--help"], new StringReader(""), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Contains("flowtime optimize", stdout.ToString());
    }

    // ── Input-error paths shared by all four commands (exit 2) ─────

    [Fact]
    public async Task UnknownFlag_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--wat"], new StringReader(""), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("--wat", stderr.ToString());
        Assert.Empty(stdout.ToString());
    }

    [Fact]
    public async Task MissingSpecFile_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--spec", "/nonexistent/spec.json"],
            new StringReader(""), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("not found", stderr.ToString());
    }

    [Fact]
    public async Task InvalidJson_FromStdin_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            Array.Empty<string>(),
            new StringReader("not valid json {{{"),
            stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("Invalid JSON", stderr.ToString());
    }

    [Fact]
    public async Task InvalidSpec_RaisesArgumentException_ReturnsTwo()
    {
        // SweepSpec throws ArgumentException on empty modelYaml; confirm the runner
        // catches it and maps to exit 2 with "Invalid spec:" on stderr.
        var specJson = """
            {
              "modelYaml": "",
              "paramId": "arrivals",
              "values": [10, 20]
            }
            """;
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            Array.Empty<string>(),
            new StringReader(specJson),
            stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("Invalid spec", stderr.ToString());
    }

    [Fact]
    public async Task MissingEngineBinary_ReturnsTwo()
    {
        // Valid spec JSON, but engine path points to nowhere → exit 2 with stderr hint.
        var specJson = """
            {
              "modelYaml": "schemaVersion: 1\ngrid:\n  bins: 4\n  binSize: 15\n  binUnit: minutes\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n",
              "paramId": "arrivals",
              "values": [10, 20]
            }
            """;
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", "/nonexistent/path/to/engine"],
            new StringReader(specJson),
            stdout, stderr);
        Assert.Equal(2, code);
        var err = stderr.ToString();
        Assert.Contains("Engine binary not found", err);
        Assert.Contains("FLOWTIME_RUST_BINARY", err);
    }

    [Fact]
    public async Task MissingEngineBinary_DoesNotWriteStdout()
    {
        var specJson = """
            {
              "modelYaml": "schemaVersion: 1\ngrid:\n  bins: 4\n  binSize: 15\n  binUnit: minutes\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n",
              "paramId": "arrivals",
              "values": [10, 20]
            }
            """;
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", "/nonexistent/engine"],
            new StringReader(specJson),
            stdout, stderr);
        Assert.Equal(2, code);
        Assert.Empty(stdout.ToString());
    }

    // ── Spec read from file ─────────────────────────────────────────

    [Fact]
    public async Task SpecFile_InvalidJson_ReturnsTwo()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, "not json");
            var (stdout, stderr) = NewWriters();
            var code = await SweepCommand.ExecuteAsync(
                ["--spec", tempPath], new StringReader(""), stdout, stderr);
            Assert.Equal(2, code);
        }
        finally { File.Delete(tempPath); }
    }

    // ── IsOnPath branch ───────────────────────────────────────────

    [Fact]
    public void IsOnPath_BareExecutableName_ReturnsTrue()
    {
        // A bare name with no path separator is treated as possibly-on-PATH and the
        // pre-flight file-existence check is skipped. The real resolution happens at
        // spawn time.
        Assert.True(AnalysisCliRunner.IsOnPath("flowtime-engine"));
    }

    [Fact]
    public void IsOnPath_AbsolutePath_ReturnsFalse()
    {
        Assert.False(AnalysisCliRunner.IsOnPath("/usr/local/bin/flowtime-engine"));
    }

    [Fact]
    public void IsOnPath_RelativePathWithSeparator_ReturnsFalse()
    {
        Assert.False(AnalysisCliRunner.IsOnPath("./bin/flowtime-engine"));
    }

    [Fact]
    public async Task BarePathWithValidSpec_SkipsFileExistsCheck()
    {
        // Using a bare name ("flowtime-engine") short-circuits the pre-flight
        // binary-not-found check. The evaluator is still created; only if the spawn
        // fails do we exit 3. This test confirms exit != 2 (pre-flight passed).
        var specJson = """
            {
              "modelYaml": "schemaVersion: 1\ngrid:\n  bins: 4\n  binSize: 15\n  binUnit: minutes\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n",
              "paramId": "arrivals",
              "values": [10]
            }
            """;
        var (stdout, stderr) = NewWriters();
        var code = await SweepCommand.ExecuteAsync(
            ["--engine", "definitely-not-on-path-xyz-flowtime"],
            new StringReader(specJson),
            stdout, stderr);
        // Pre-flight passes (bare name), but spawn fails → exit 3.
        Assert.NotEqual(2, code);
    }

    [Fact]
    public async Task SpecFile_ValidJson_PassesInitialChecks()
    {
        // Valid JSON spec from file, then fails at engine resolution step — still
        // proves spec was parsed OK.
        var specJson = """
            {
              "modelYaml": "schemaVersion: 1\ngrid:\n  bins: 4\n  binSize: 15\n  binUnit: minutes\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n",
              "paramId": "arrivals",
              "values": [10, 20]
            }
            """;
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, specJson);
            var (stdout, stderr) = NewWriters();
            var code = await SweepCommand.ExecuteAsync(
                ["--spec", tempPath, "--engine", "/nowhere"],
                new StringReader(""), stdout, stderr);
            // Reached engine check → parse succeeded.
            Assert.Equal(2, code);
            Assert.Contains("Engine binary not found", stderr.ToString());
        }
        finally { File.Delete(tempPath); }
    }
}
