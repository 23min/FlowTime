using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FlowTime.API.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Generator;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Integration.Tests.Loop;

public sealed class ClassesLoopTests
{
    [Fact]
    public async Task SimulationRunMatchesTelemetryRunPerClass()
    {
        using var temp = new TempDirectory();
        var orchestration = CreateOrchestrationService();

        var simResult = await CreateRunAsync(orchestration, TemplateMode.Simulation, Path.Combine(temp.Path, "runs-sim"));
        var captureDir = LoopParityFixture.CreateTelemetryCapture(temp.Path, includeWholesale: true);
        var telemetryResult = await CreateRunAsync(
            orchestration,
            TemplateMode.Telemetry,
            Path.Combine(temp.Path, "runs-telemetry"),
            captureDir);

        var simWindow = await LoadStateWindowAsync(simResult, LoopParityFixture.BinCount);
        var telemetryWindow = await LoadStateWindowAsync(telemetryResult, LoopParityFixture.BinCount);

        Assert.Equal("simulation", simWindow.Metadata.Mode);
        Assert.Equal("telemetry", telemetryWindow.Metadata.Mode);
        Assert.Equal("full", simWindow.Metadata.ClassCoverage);
        Assert.Equal("full", telemetryWindow.Metadata.ClassCoverage);

        var simNode = RequireNode(simWindow, LoopParityFixture.ServiceNodeId);
        var telemetryNode = RequireNode(telemetryWindow, LoopParityFixture.ServiceNodeId);

        AssertSeriesEqual(RequireSeries(simNode.Series, "arrivals"), RequireSeries(telemetryNode.Series, "arrivals"));
        AssertSeriesEqual(RequireSeries(simNode.Series, "served"), RequireSeries(telemetryNode.Series, "served"));
        AssertSeriesEqual(RequireSeries(simNode.Series, "errors"), RequireSeries(telemetryNode.Series, "errors"));

        Assert.NotNull(simNode.ByClass);
        Assert.NotNull(telemetryNode.ByClass);
        var simByClass = simNode.ByClass!;
        var telemetryByClass = telemetryNode.ByClass!;
        Assert.Equal(simByClass.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase), telemetryByClass.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));

        foreach (var classId in simByClass.Keys)
        {
            var simMetrics = simByClass[classId];
            var telemetryMetrics = telemetryByClass[classId];
            foreach (var metric in simMetrics.Keys)
            {
                Assert.True(telemetryMetrics.ContainsKey(metric), $"Missing metric '{metric}' for class '{classId}' in telemetry run.");
                AssertSeriesEqual(simMetrics[metric], telemetryMetrics[metric]);
            }
        }
    }

    [Fact]
    public async Task MissingClassTelemetryEmitsWarningsAndPartialCoverage()
    {
        using var temp = new TempDirectory();
        var orchestration = CreateOrchestrationService();

        var simResult = await CreateRunAsync(orchestration, TemplateMode.Simulation, Path.Combine(temp.Path, "runs-sim"));
        var captureDir = LoopParityFixture.CreateTelemetryCapture(temp.Path, includeWholesale: false);
        var telemetryResult = await CreateRunAsync(
            orchestration,
            TemplateMode.Telemetry,
            Path.Combine(temp.Path, "runs-telemetry"),
            captureDir);

        var simWindow = await LoadStateWindowAsync(simResult, LoopParityFixture.BinCount);
        var telemetryWindow = await LoadStateWindowAsync(telemetryResult, LoopParityFixture.BinCount);

        Assert.Equal("full", simWindow.Metadata.ClassCoverage);
        Assert.Equal("partial", telemetryWindow.Metadata.ClassCoverage);

        var warnings = telemetryResult.TelemetryManifest.Warnings.Select(w => w.Code).ToArray();
        Assert.Contains("class_series_partial", warnings);
        Assert.Contains("class_conservation_mismatch", warnings);

        var simNode = RequireNode(simWindow, LoopParityFixture.ServiceNodeId);
        var telemetryNode = RequireNode(telemetryWindow, LoopParityFixture.ServiceNodeId);

        AssertSeriesEqual(RequireSeries(simNode.Series, "arrivals"), RequireSeries(telemetryNode.Series, "arrivals"));
        AssertSeriesEqual(RequireSeries(simNode.Series, "served"), RequireSeries(telemetryNode.Series, "served"));

        Assert.NotNull(telemetryNode.ByClass);
        Assert.True(telemetryNode.ByClass!.ContainsKey("Retail"), "Retail class should still be present.");
        Assert.False(telemetryNode.ByClass.ContainsKey("Wholesale"), "Wholesale class should be missing from telemetry run.");
    }

    private static RunOrchestrationService CreateOrchestrationService()
    {
        var templateDirectory = LoopParityFixture.TemplateDirectory;
        var templateService = new TemplateService(templateDirectory, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        return new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);
    }

    private static async Task<RunOrchestrationResult> CreateRunAsync(
        RunOrchestrationService orchestration,
        TemplateMode mode,
        string outputRoot,
        string? captureDir = null)
    {
        var request = new RunOrchestrationRequest
        {
            TemplateId = LoopParityFixture.TemplateId,
            Mode = mode.ToSerializedValue(),
            OutputRoot = outputRoot,
            CaptureDirectory = captureDir,
            DeterministicRunId = true,
            DryRun = false,
            OverwriteExisting = true,
            Rng = new RunRngOptions { Seed = LoopParityFixture.RngSeed }
        };

        var outcome = await orchestration.CreateRunAsync(request, CancellationToken.None).ConfigureAwait(false);
        Assert.False(outcome.IsDryRun, "Loop parity tests expect runs to be executed.");
        Assert.NotNull(outcome.Result);
        return outcome.Result!;
    }

    private static async Task<StateWindowResponse> LoadStateWindowAsync(RunOrchestrationResult result, int binCount)
    {
        var stateService = TestStateQueryServiceFactory.Create(result.RunDirectory);
        return await stateService.GetStateWindowAsync(
            result.RunId,
            startBin: 0,
            endBin: binCount - 1,
            mode: GraphQueryMode.Operational,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    private static NodeSeries RequireNode(StateWindowResponse window, string nodeId)
    {
        var node = window.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(node);
        return node!;
    }

    private static double?[] RequireSeries(IDictionary<string, double?[]> series, string metric)
    {
        Assert.True(series.TryGetValue(metric, out var values), $"Metric '{metric}' missing from node series.");
        return values!;
    }

    private static void AssertSeriesEqual(double?[] expected, double?[] actual, double tolerance = 1e-6)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var expectedValue = expected[i];
            var actualValue = actual[i];
            if (expectedValue is null)
            {
                Assert.Null(actualValue);
            }
            else
            {
                Assert.NotNull(actualValue);
                Assert.InRange(Math.Abs(actualValue!.Value - expectedValue.Value), 0, tolerance);
            }
        }
    }
}

internal static class LoopParityFixture
{
    public const string TemplateId = "loop-parity-template";
    public const string ServiceNodeId = "OrderService";
    public const int BinCount = 12;
    public const int BinSizeMinutes = 5;
    public const int RngSeed = 424242;

    public static string TemplateDirectory => Path.Combine(GetRepoRoot(), "tests", "fixtures", "templates");

    private static readonly double[] retailSeries = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };
    private static readonly double[] wholesaleSeries = { 3, 4, 3, 4, 3, 4, 3, 4, 3, 4, 3, 4 };
    private static readonly double[] totalSeries = retailSeries.Zip(wholesaleSeries, (a, b) => a + b).ToArray();
    private static readonly double[] zeroSeries = Enumerable.Repeat(0d, BinCount).ToArray();

    public static string CreateTelemetryCapture(string root, bool includeWholesale)
    {
        var captureDir = Path.Combine(root, includeWholesale ? "capture-loop-parity" : "capture-loop-parity-partial");
        Directory.CreateDirectory(captureDir);

        var files = new List<TelemetryManifestFile>();

        files.AddRange(WriteMetricFiles(
            captureDir,
            TelemetryMetricKind.Arrivals,
            retailSeries,
            wholesaleSeries,
            totalSeries,
            includeWholesale));

        files.AddRange(WriteMetricFiles(
            captureDir,
            TelemetryMetricKind.Served,
            retailSeries,
            wholesaleSeries,
            totalSeries,
            includeWholesale));

        files.AddRange(WriteMetricFiles(
            captureDir,
            TelemetryMetricKind.Errors,
            zeroSeries,
            zeroSeries,
            zeroSeries,
            includeWholesale));

        var manifest = new TelemetryManifest(
            SchemaVersion: 2,
            Window: new TelemetryManifestWindow("2025-01-01T00:00:00Z", BinCount * BinSizeMinutes),
            Grid: new TelemetryManifestGrid(BinCount, BinSizeMinutes, "minutes"),
            Files: files,
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(
                RunId: includeWholesale ? "run_loop_parity_full" : "run_loop_parity_partial",
                ScenarioHash: "sha256:1111111111111111111111111111111111111111111111111111111111111111",
                ModelHash: null,
                CapturedAtUtc: DateTime.UtcNow.ToString("O")),
            SupportsClassMetrics: true,
            Classes: new[] { "Retail", "Wholesale" },
            ClassCoverage: includeWholesale ? "full" : "partial");

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(captureDir, "manifest.json"), json);

        return captureDir;
    }

    private static IEnumerable<TelemetryManifestFile> WriteMetricFiles(
        string captureDir,
        TelemetryMetricKind metric,
        IReadOnlyList<double> retail,
        IReadOnlyList<double> wholesale,
        IReadOnlyList<double> totals,
        bool includeWholesale)
    {
        var files = new List<TelemetryManifestFile>
        {
            WriteCsv(captureDir, BuildFileName(metric, "Retail"), retail, "Retail", metric),
        };

        if (includeWholesale)
        {
            files.Add(WriteCsv(captureDir, BuildFileName(metric, "Wholesale"), wholesale, "Wholesale", metric));
        }

        files.Add(WriteCsv(captureDir, BuildFileName(metric, "DEFAULT"), totals, "DEFAULT", metric));
        return files;
    }

    private static TelemetryManifestFile WriteCsv(
        string captureDir,
        string fileName,
        IReadOnlyList<double> values,
        string classId,
        TelemetryMetricKind metric)
    {
        var path = Path.Combine(captureDir, fileName);
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("bin_index,classId,value");
            for (var i = 0; i < values.Count; i++)
            {
                writer.WriteLine(FormattableString.Invariant($"{i},{classId},{values[i]:G17}"));
            }
        }

        var hash = ComputeSha256(path);
        return new TelemetryManifestFile(
            NodeId: ServiceNodeId,
            Metric: metric,
            Path: fileName,
            Hash: hash,
            Points: values.Count,
            ClassId: classId);
    }

    private static string BuildFileName(TelemetryMetricKind metric, string classId)
        => $"{ServiceNodeId}_{metric}_{classId}.csv";

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetRepoRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
}
