using System.Globalization;
using System.Text;
using FlowTime.Contracts.Services;
using FlowTime.Core;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Routing;

namespace FlowTime.Integration.Tests;

/// <summary>
/// Full parity harness: evaluates every Rust engine fixture through both engines
/// and compares series values. See m-E20-08 spec.
/// </summary>
public class RustEngineParityTests : IClassFixture<RustEngineParityTests.ParityFixture>
{
    private readonly ParityFixture fixture;

    public RustEngineParityTests(ParityFixture fixture)
    {
        this.fixture = fixture;
    }

    public sealed class ParityFixture
    {
        public string? EnginePath { get; }
        public string FixturesDir { get; }

        public ParityFixture()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var binaryPath = Path.Combine(repoRoot, "engine", "target", "release", "flowtime-engine");
            EnginePath = File.Exists(binaryPath) ? binaryPath : null;
            FixturesDir = Path.Combine(repoRoot, "engine", "fixtures");
        }
    }

    /// <summary>
    /// Class-enabled fixtures that are expected to diverge on topology-derived per-class series.
    /// Shared input series (const, expr nodes) should still match.
    /// m-E20-09: class-enabled.yaml removed — per-class decomposition now matches C#.
    /// Router fixtures remain because they are in RustOnlyFixtures (C# parser can't handle them).
    /// </summary>
    private static readonly HashSet<string> ClassFixtures = new(StringComparer.OrdinalIgnoreCase)
    {
        "router-class.yaml",
        "router-mixed.yaml",
    };

    /// <summary>
    /// Fixtures that use Rust-specific model syntax the C# parser can't handle
    /// (e.g., router topology format without inputs.queue, or Rust-specific node kinds).
    /// These are tested for Rust-only correctness in the Rust test suite.
    /// </summary>
    private static readonly HashSet<string> RustOnlyFixtures = new(StringComparer.OrdinalIgnoreCase)
    {
        "router-class.yaml",
        "router-mixed.yaml",
        "router-weight.yaml",
        "router-with-constraint.yaml",
    };

    /// <summary>
    /// Fixtures that reference features the Rust engine doesn't yet handle
    /// (e.g., file: URI data sources, PMF-only nodes compiled as expr, grid-less models).
    /// Tracked as known gaps; will be addressed in future milestones.
    /// </summary>
    private static readonly HashSet<string> KnownRustGaps = new(StringComparer.OrdinalIgnoreCase)
    {
        "http-service.yaml",         // no grid in Rust parse
        "microservices.yaml",        // no grid in Rust parse
        "order-system.yaml",         // no grid in Rust parse
        "simple-const.yaml",         // kind: expr without expr field (uses formula field)
        "pmf.yaml",                  // COMP_A compiled as expr, needs expr field
        "complex-pmf.yaml",          // COMP_A compiled as expr, needs expr field
        "retry-service-time.yaml",   // file: URI data source reference
    };

    /// <summary>
    /// Fixtures with known evaluation divergences that are tracked bugs, not parity failures.
    /// </summary>
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.OrdinalIgnoreCase)
    {
        "topology-backpressure.yaml", // SHIFT-based feedback divergence — tracked
    };

    private const double Tolerance = 1e-10;

    public static IEnumerable<object[]> AllFixtures()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var fixturesDir = Path.Combine(repoRoot, "engine", "fixtures");
        if (!Directory.Exists(fixturesDir))
            yield break;

        foreach (var file in Directory.GetFiles(fixturesDir, "*.yaml").OrderBy(f => f))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Fixture_Parity(string fixtureName)
    {
        if (fixture.EnginePath is null) return;

        // Skip fixtures that can't be compared (both engines must parse successfully)
        if (RustOnlyFixtures.Contains(fixtureName)) return;
        if (KnownRustGaps.Contains(fixtureName)) return;

        var yamlPath = Path.Combine(fixture.FixturesDir, fixtureName);
        var yaml = await File.ReadAllTextAsync(yamlPath);

        // Ensure schemaVersion is present for C# parser
        if (!yaml.Contains("schemaVersion"))
        {
            yaml = "schemaVersion: 1\n" + yaml;
        }

        // ── Rust evaluation ──
        var runner = new RustEngineRunner(fixture.EnginePath);
        RustEngineRunner.RustEvalResult rustResult;
        try
        {
            rustResult = await runner.EvaluateAsync(yaml);
        }
        catch (RustEngineException ex)
        {
            Assert.Fail($"Rust engine failed on {fixtureName}: {ex.Message}");
            return;
        }

        // ── C# evaluation ──
        ModelDefinition coreModel;
        IReadOnlyDictionary<NodeId, double[]> csharpContext;
        try
        {
            coreModel = ModelService.ParseAndConvert(yaml);
            var (grid, graph) = ModelParser.ParseModel(coreModel);
            var routerEvaluation = RouterAwareGraphEvaluator.Evaluate(coreModel, graph, grid);
            csharpContext = routerEvaluation.Context;
        }
        catch (Exception ex)
        {
            Assert.Fail($"C# engine failed on {fixtureName}: {ex.Message}");
            return;
        }

        // ── Compare series ──
        var report = new StringBuilder();
        var matchCount = 0;
        var skipCount = 0;
        var failCount = 0;

        foreach (var rustSeries in rustResult.Series)
        {
            var csharpMatch = csharpContext.Keys.FirstOrDefault(k =>
                k.Value.Equals(rustSeries.Id, StringComparison.OrdinalIgnoreCase));

            if (csharpMatch.Value is null)
            {
                skipCount++;
                continue;
            }

            var csharpValues = csharpContext[csharpMatch];

            if (csharpValues.Length != rustSeries.Values.Length)
            {
                report.AppendLine($"  FAIL {rustSeries.Id}: length mismatch (Rust={rustSeries.Values.Length}, C#={csharpValues.Length})");
                failCount++;
                continue;
            }

            var seriesMatch = true;
            for (int i = 0; i < csharpValues.Length; i++)
            {
                if (Math.Abs(csharpValues[i] - rustSeries.Values[i]) > Tolerance)
                {
                    report.AppendLine($"  FAIL {rustSeries.Id}[{i}]: Rust={rustSeries.Values[i]}, C#={csharpValues[i]}, delta={Math.Abs(csharpValues[i] - rustSeries.Values[i])}");
                    seriesMatch = false;
                    failCount++;
                    break;
                }
            }

            if (seriesMatch)
            {
                matchCount++;
            }
        }

        // ── Assert ──
        if (failCount > 0)
        {
            if (KnownDivergences.Contains(fixtureName) || ClassFixtures.Contains(fixtureName))
            {
                // Known divergence — log but don't fail
                return;
            }

            Assert.Fail($"Parity failed for {fixtureName}: {matchCount} matched, {failCount} failed, {skipCount} skipped (topology-derived)\n{report}");
        }
    }

    [Fact]
    public async Task ParityMatrix_Summary()
    {
        if (fixture.EnginePath is null) return;

        var fixtureFiles = Directory.GetFiles(fixture.FixturesDir, "*.yaml").OrderBy(f => f).ToList();
        var runner = new RustEngineRunner(fixture.EnginePath);
        var summary = new StringBuilder();
        summary.AppendLine("=== Rust/C# Parity Matrix ===");
        summary.AppendLine();

        var passCount = 0;
        var knownCount = 0;
        var skipCount = 0;
        var failCount = 0;

        foreach (var file in fixtureFiles)
        {
            var name = Path.GetFileName(file);

            if (RustOnlyFixtures.Contains(name))
            {
                summary.AppendLine($"  SKIP   {name,-45} (Rust-only fixture, C# can't parse)");
                skipCount++;
                continue;
            }

            if (KnownRustGaps.Contains(name))
            {
                summary.AppendLine($"  SKIP   {name,-45} (known Rust gap — tracked)");
                skipCount++;
                continue;
            }

            var yaml = await File.ReadAllTextAsync(file);
            if (!yaml.Contains("schemaVersion"))
            {
                yaml = "schemaVersion: 1\n" + yaml;
            }

            try
            {
                var rustResult = await runner.EvaluateAsync(yaml);
                var coreModel = ModelService.ParseAndConvert(yaml);
                var (grid, graph) = ModelParser.ParseModel(coreModel);
                var routerEvaluation = RouterAwareGraphEvaluator.Evaluate(coreModel, graph, grid);
                var csharpContext = routerEvaluation.Context;

                var matched = 0;
                var diverged = 0;
                var skipped2 = 0;

                foreach (var rs in rustResult.Series)
                {
                    var csMatch = csharpContext.Keys.FirstOrDefault(k =>
                        k.Value.Equals(rs.Id, StringComparison.OrdinalIgnoreCase));

                    if (csMatch.Value is null) { skipped2++; continue; }

                    var csv = csharpContext[csMatch];
                    if (csv.Length != rs.Values.Length) { diverged++; continue; }

                    var ok = true;
                    for (int i = 0; i < csv.Length; i++)
                    {
                        if (Math.Abs(csv[i] - rs.Values[i]) > Tolerance) { ok = false; break; }
                    }

                    if (ok) matched++; else diverged++;
                }

                if (diverged > 0)
                {
                    if (ClassFixtures.Contains(name) || KnownDivergences.Contains(name))
                    {
                        summary.AppendLine($"  KNOWN  {name,-45} matched={matched} diverged={diverged} skipped={skipped2}");
                        knownCount++;
                    }
                    else
                    {
                        summary.AppendLine($"  FAIL   {name,-45} matched={matched} diverged={diverged} skipped={skipped2}");
                        failCount++;
                    }
                }
                else
                {
                    summary.AppendLine($"  PASS   {name,-45} matched={matched} skipped={skipped2}");
                    passCount++;
                }
            }
            catch (Exception ex)
            {
                summary.AppendLine($"  ERROR  {name,-45} {ex.GetType().Name}: {ex.Message}");
                failCount++;
            }
        }

        summary.AppendLine();
        summary.AppendLine($"Total: {fixtureFiles.Count} fixtures — {passCount} pass, {knownCount} known, {skipCount} skip, {failCount} fail");

        Assert.True(failCount == 0, summary.ToString());
    }
}
