using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FlowTime.Contracts.Services;
using FlowTime.Generator;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Xunit;

namespace FlowTime.Generator.Tests;

public sealed class TelemetryLoaderByClassTests
{
    [Fact]
    public async Task BuildAsync_WithClassAwareBundle_WritesPerClassSeries()
    {
        using var temp = new TempDirectory();
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var bins = 4;
        WriteClassCsv(captureDir, "OrderService_arrivals_DEFAULT.csv", "DEFAULT", new[] { 10d, 9d, 8d, 7d });
        WriteClassCsv(captureDir, "OrderService_arrivals_Retail.csv", "Retail", new[] { 6d, 5d, 4d, 3d });
        WriteClassCsv(captureDir, "OrderService_arrivals_Wholesale.csv", "Wholesale", new[] { 4d, 4d, 4d, 4d });

        WriteClassCsv(captureDir, "OrderService_served_DEFAULT.csv", "DEFAULT", new[] { 8d, 8d, 8d, 8d });
        WriteClassCsv(captureDir, "OrderService_served_Retail.csv", "Retail", new[] { 5d, 5d, 5d, 4d });
        WriteClassCsv(captureDir, "OrderService_served_Wholesale.csv", "Wholesale", new[] { 3d, 3d, 3d, 4d });

        WriteClassCsv(captureDir, "OrderService_errors_DEFAULT.csv", "DEFAULT", new[] { 2d, 1d, 1d, 1d });
        WriteClassCsv(captureDir, "OrderService_errors_Retail.csv", "Retail", new[] { 1d, 0d, 0d, 0d });
        WriteClassCsv(captureDir, "OrderService_errors_Wholesale.csv", "Wholesale", new[] { 1d, 1d, 1d, 1d });

        var manifestEntries = new List<TelemetryManifestFile>
        {
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Arrivals, "OrderService_arrivals_DEFAULT.csv", "DEFAULT", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Arrivals, "OrderService_arrivals_Retail.csv", "Retail", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Arrivals, "OrderService_arrivals_Wholesale.csv", "Wholesale", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Served, "OrderService_served_DEFAULT.csv", "DEFAULT", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Served, "OrderService_served_Retail.csv", "Retail", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Served, "OrderService_served_Wholesale.csv", "Wholesale", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Errors, "OrderService_errors_DEFAULT.csv", "DEFAULT", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Errors, "OrderService_errors_Retail.csv", "Retail", bins),
            ManifestEntry(captureDir, "OrderService", TelemetryMetricKind.Errors, "OrderService_errors_Wholesale.csv", "Wholesale", bins)
        };

        WriteManifest(captureDir, bins, manifestEntries);

        var modelPath = Path.Combine(temp.Path, "telemetry-model.yaml");
        await File.WriteAllTextAsync(modelPath, BuildTelemetryModelYaml(captureDir));

        var builder = new TelemetryBundleBuilder();
        var result = await builder.BuildAsync(new TelemetryBundleOptions
        {
            CaptureDirectory = captureDir,
            ModelPath = modelPath,
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        });

        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        var series = doc.RootElement.GetProperty("series").EnumerateArray().ToList();
        Assert.Contains(series, element => element.GetProperty("id").GetString()!.Contains("@Retail", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(series, element => element.GetProperty("id").GetString()!.Contains("@Wholesale", StringComparison.OrdinalIgnoreCase));

        var retailSeriesPath = Path.Combine(result.RunDirectory, "series", "order_arrivals@ORDER_ARRIVALS@Retail.csv");
        Assert.True(File.Exists(retailSeriesPath));
        var retailLines = await File.ReadAllLinesAsync(retailSeriesPath);
        Assert.Equal("6", retailLines[1].Split(',')[1]);
    }

    private static TelemetryManifestFile ManifestEntry(string captureDir, string nodeId, TelemetryMetricKind metric, string relativePath, string classId, int bins)
    {
        var fullPath = Path.Combine(captureDir, relativePath);
        return new TelemetryManifestFile(
            NodeId: nodeId,
            Metric: metric,
            Path: relativePath,
            Hash: ComputeSha256(fullPath),
            Points: bins,
            ClassId: classId);
    }

    private static void WriteManifest(string captureDir, int bins, IReadOnlyList<TelemetryManifestFile> files)
    {
        var manifest = new TelemetryManifest(
            SchemaVersion: 2,
            Window: new TelemetryManifestWindow("2025-01-01T00:00:00Z", bins * 5),
            Grid: new TelemetryManifestGrid(bins, 5, "minutes"),
            Files: files,
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(
                RunId: "run_source",
                ScenarioHash: "sha256:1111111111111111111111111111111111111111111111111111111111111111",
                ModelHash: null,
                CapturedAtUtc: DateTime.UtcNow.ToString("O")),
            Classes: new[] { "Retail", "Wholesale" },
            ClassCoverage: "full");

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(captureDir, "manifest.json"), json);
    }

    private static void WriteClassCsv(string captureDir, string fileName, string classId, IReadOnlyList<double> values)
    {
        var path = Path.Combine(captureDir, fileName);
        using var writer = new StreamWriter(path);
        writer.WriteLine("bin_index,classId,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{classId},{values[i]:G17}"));
        }
    }

    private static string BuildTelemetryModelYaml(string captureDir)
    {
        string UriFor(string fileName) => new Uri(Path.Combine(captureDir, fileName)).AbsoluteUri;

        return $"""
schemaVersion: 1
mode: telemetry
grid:
  bins: 4
  binSize: 5
  binUnit: minutes
metadata:
  id: telemetry-class-test
  title: Class Telemetry Fixture
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

nodes:
  - id: order_arrivals
    kind: const
    values: [0, 0, 0, 0]
    source: {UriFor("OrderService_arrivals_DEFAULT.csv")}
  - id: order_served
    kind: const
    values: [0, 0, 0, 0]
    source: {UriFor("OrderService_served_DEFAULT.csv")}
  - id: order_errors
    kind: const
    values: [0, 0, 0, 0]
    source: {UriFor("OrderService_errors_DEFAULT.csv")}

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: order_arrivals
        served: order_served
        errors: order_errors
  edges: []

outputs:
  - series: order_arrivals
    as: OrderService_arrivals_DEFAULT.csv
  - series: order_served
    as: OrderService_served_DEFAULT.csv
  - series: order_errors
    as: OrderService_errors_DEFAULT.csv
""";
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
