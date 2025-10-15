using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Cli.Commands;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Xunit;

namespace FlowTime.Cli.Tests;

public sealed class TelemetryWorkflowEndToEndTests
{
    [Fact]
    public async Task CaptureSimBundleWorkflow_ProducesStateQueryableRun()
    {
        using var temp = new TempDirectory();
        var sourceRunDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_workflow", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "telemetry-workflow");

        var exit = await TelemetryCaptureCommand.ExecuteAsync(new[]
        {
            "capture",
            "--run-dir", sourceRunDir,
            "--output", captureDir
        });

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(captureDir, "manifest.json")));

        var templatePath = Path.Combine(temp.Path, "sim-model.yaml");
        await File.WriteAllTextAsync(templatePath, BuildTelemetryModelYaml(captureDir));

        var builder = new TelemetryBundleBuilder();
        var outputRoot = Path.Combine(temp.Path, "runs");
        Directory.CreateDirectory(outputRoot);

        var result = await builder.BuildAsync(new TelemetryBundleOptions
        {
            CaptureDirectory = captureDir,
            ModelPath = templatePath,
            OutputRoot = outputRoot,
            DeterministicRunId = true
        });

        var canonicalModelPath = Path.Combine(result.RunDirectory, "model", "model.yaml");
        Assert.True(File.Exists(canonicalModelPath));
        var canonicalYaml = await File.ReadAllTextAsync(canonicalModelPath);
        Assert.Contains("file://telemetry/OrderService_arrivals.csv", canonicalYaml);

        var stateService = TestStateQueryServiceFactory.Create(result.RunDirectory);
        var snapshot = await stateService.GetStateAsync(result.RunId, binIndex: 0, CancellationToken.None);
        Assert.Equal("telemetry", snapshot.Metadata.Mode);
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

}
