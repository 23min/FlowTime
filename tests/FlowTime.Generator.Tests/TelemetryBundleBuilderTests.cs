using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FlowTime.API.Services;
using FlowTime.Contracts.Services;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.Generator.Tests;

public sealed class TelemetryBundleBuilderTests
{
    [Fact]
    public async Task BuildAsync_CreatesCanonicalRun_WithTelemetrySources()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_bundle_base", includeTopology: true);

        var captureOutput = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureOutput);
        CreateTelemetryCapture(runDir, captureOutput);

        var manifestJson = await File.ReadAllTextAsync(Path.Combine(captureOutput, "manifest.json"));
        var captureManifest = JsonSerializer.Deserialize<TelemetryManifest>(manifestJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(captureManifest);
        Assert.NotEmpty(captureManifest!.Files);
        Assert.Equal(3, captureManifest.Files!.Count);
        Assert.Contains("OrderService_arrivals.csv", captureManifest.Files!.Select(f => f.Path));
        var captureFiles = Directory.GetFiles(captureOutput).Select(Path.GetFileName).ToArray();
        Assert.Contains("OrderService_arrivals.csv", captureFiles);

        var engineYaml = BuildTelemetryModelYaml(captureOutput);
        var modelPath = Path.Combine(temp.Path, "model.yaml");
        await File.WriteAllTextAsync(modelPath, engineYaml);

        var arrivalsUri = new Uri(Path.Combine(captureOutput, "OrderService_arrivals.csv")).AbsoluteUri;
        var builder = new TelemetryBundleBuilder();
        var outputRoot = Path.Combine(temp.Path, "runs");
        Directory.CreateDirectory(outputRoot);

        var result = await builder.BuildAsync(new TelemetryBundleOptions
        {
            CaptureDirectory = captureOutput,
            ModelPath = modelPath,
            OutputRoot = outputRoot,
        });
        Console.WriteLine($"Run directory: {result.RunDirectory}");
        Console.WriteLine($"Output directories: {string.Join(", ", Directory.GetDirectories(outputRoot).Select(Path.GetFileName))}");

        Assert.True(Directory.Exists(result.RunDirectory));

        var modelDir = Path.Combine(result.RunDirectory, "model");
        var telemetryDir = Path.Combine(modelDir, "telemetry");
        Assert.True(Directory.Exists(modelDir));
        var modelFiles = Directory.Exists(modelDir) ? Directory.GetFiles(modelDir).Select(Path.GetFileName).ToArray() : Array.Empty<string>();
        Assert.Contains("model.yaml", modelFiles);
        Assert.True(Directory.Exists(telemetryDir));
        var telemetryFiles = Directory.GetFiles(telemetryDir).Select(Path.GetFileName).ToArray();
        Assert.Contains("OrderService_arrivals.csv", telemetryFiles);

        var modelYaml = await File.ReadAllTextAsync(Path.Combine(modelDir, "model.yaml"));
        Assert.Contains("file://telemetry/OrderService_arrivals.csv", modelYaml);
        Assert.Contains("mode: telemetry", modelYaml);

        var canonicalModelDefinition = ModelService.ParseAndConvert(modelYaml);
        var orderNode = Assert.Single(canonicalModelDefinition.Topology!.Nodes, n => n.Id == "OrderService");
        Assert.Equal("file://telemetry/OrderService_arrivals.csv", orderNode.Semantics.Arrivals);
        Assert.Equal("file://telemetry/OrderService_served.csv", orderNode.Semantics.Served);
        Assert.Equal("file://telemetry/OrderService_errors.csv", orderNode.Semantics.Errors);

        var manifestReader = new RunManifestReader();
        var manifestMetadata = await manifestReader.ReadAsync(modelDir, CancellationToken.None);
        Assert.Equal("telemetry-order", manifestMetadata.TemplateId);
        Assert.Equal("telemetry", manifestMetadata.Mode);
        Assert.Contains("file://telemetry/OrderService_arrivals.csv", manifestMetadata.TelemetrySources);

        var stateService = TestStateQueryServiceFactory.Create(result.RunDirectory);
        var snapshot = await stateService.GetStateAsync(Path.GetFileName(result.RunDirectory), binIndex: 0, CancellationToken.None);
        Assert.Equal("telemetry", snapshot.Metadata.Mode);
        Assert.NotEmpty(snapshot.Nodes);
        Assert.Equal(10, snapshot.Nodes.First(n => n.Id == "OrderService").Metrics.Arrivals);
    }

    private static string BuildTelemetryModelYaml(string telemetryDir)
    {
        string UriFor(string fileName) => new Uri(Path.Combine(telemetryDir, fileName)).AbsoluteUri;

        var arrivalsUri = UriFor("OrderService_arrivals.csv");
        var servedUri = UriFor("OrderService_served.csv");
        var errorsUri = UriFor("OrderService_errors.csv");

        return $"""
schemaVersion: 1
mode: telemetry
metadata:
  id: telemetry-order
  title: Telemetry Order System
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 5
  binUnit: minutes
topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: {arrivalsUri}
        served: {servedUri}
        errors: {errorsUri}
  edges: []
nodes:
  - id: order_arrivals
    kind: const
    values: [0, 0, 0, 0]
    source: {arrivalsUri}
  - id: order_served
    kind: const
    values: [0, 0, 0, 0]
    source: {servedUri}
  - id: order_errors
    kind: const
    values: [0, 0, 0, 0]
    source: {errorsUri}
outputs:
  - series: order_arrivals
    as: OrderService_arrivals.csv
  - series: order_served
    as: OrderService_served.csv
  - series: order_errors
    as: OrderService_errors.csv
""";
    }

    private static void CreateTelemetryCapture(string runDir, string captureDir)
    {
        Directory.CreateDirectory(captureDir);
        var mappings = new (string NodeId, TelemetryMetricKind Metric, string Source, string Target)[]
        {
            ("OrderService", TelemetryMetricKind.Arrivals, "series/order_arrivals@ORDER_ARRIVALS@DEFAULT.csv", "OrderService_arrivals.csv"),
            ("OrderService", TelemetryMetricKind.Served, "series/order_served@ORDER_SERVED@DEFAULT.csv", "OrderService_served.csv"),
            ("OrderService", TelemetryMetricKind.Errors, "series/order_errors@ORDER_ERRORS@DEFAULT.csv", "OrderService_errors.csv")
        };

        var manifestFiles = new List<TelemetryManifestFile>();
        foreach (var mapping in mappings)
        {
            var sourcePath = Path.Combine(runDir, mapping.Source.Replace('/', Path.DirectorySeparatorChar));
            var targetPath = Path.Combine(captureDir, mapping.Target);
            ConvertSeriesToTelemetry(sourcePath, targetPath);
            manifestFiles.Add(new TelemetryManifestFile(mapping.NodeId, mapping.Metric, mapping.Target, ComputeSha256(targetPath), 4));
        }

        var manifest = new TelemetryManifest(
            SchemaVersion: 1,
            Window: new TelemetryManifestWindow("2025-01-01T00:00:00Z", 20),
            Grid: new TelemetryManifestGrid(4, 5, "minutes"),
            Files: manifestFiles,
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(Path.GetFileName(runDir), "sha256:1111111111111111111111111111111111111111111111111111111111111111", null, DateTime.UtcNow.ToString("O")));

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(captureDir, "manifest.json"), json);
    }

    private static void ConvertSeriesToTelemetry(string sourcePath, string targetPath)
    {
        using var reader = new StreamReader(sourcePath);
        using var writer = new StreamWriter(targetPath);
        writer.WriteLine("bin_index,value");
        _ = reader.ReadLine(); // skip header
        var index = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',', 2);
            if (parts.Length < 2)
            {
                continue;
            }

            writer.WriteLine(FormattableString.Invariant($"{index},{parts[1].Trim()}"));
            index++;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
