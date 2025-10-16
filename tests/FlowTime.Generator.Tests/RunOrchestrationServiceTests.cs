using FlowTime.Generator.Models;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Services;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Generator.Tests;

public class RunOrchestrationServiceTests
{
    [Fact]
    public async Task CreateRunAsync_DeterministicRun_Succeeds()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "test-order.yaml");
        await File.WriteAllTextAsync(templatePath, TestTemplate);

        var sourceRunDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "source_run", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new TelemetryCapture();
        await capture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = sourceRunDir,
            OutputDirectory = captureDir
        });

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "test-order",
            Mode = "telemetry",
            CaptureDirectory = captureDir,
            TelemetryBindings = new Dictionary<string, string>
            {
                ["telemetryArrivals"] = "OrderService_arrivals.csv",
                ["telemetryServed"] = "OrderService_served.csv"
            },
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true,
            DryRun = false,
            OverwriteExisting = false
        };

        var result = await orchestration.CreateRunAsync(request);

        Assert.NotNull(result);
        Assert.True(result.TelemetrySourcesResolved);
        Assert.True(Directory.Exists(result.RunDirectory));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "model", "model.yaml")));
        Assert.Equal("telemetry", result.ManifestMetadata.Mode);
    }

    [Fact]
    public async Task TryLoadRunAsync_ReturnsNull_WhenDirectoryMissing()
    {
        using var temp = new TempDirectory();

        var templateService = new TemplateService(new Dictionary<string, (FlowTime.Sim.Core.Templates.Template, string)>(), NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var result = await orchestration.TryLoadRunAsync(Path.Combine(temp.Path, "missing"));

        Assert.Null(result);
    }

    private const string TestTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-order
  title: Run Orchestration Service Test Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: telemetryArrivals
    type: string
    default: ""
  - name: telemetryServed
    type: string
    default: ""
  - name: bins
    type: integer
    default: 4
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: "order_arrivals"
        served: "order_served"
        errors: "order_errors"
  edges: []

nodes:
  - id: order_arrivals
    kind: const
    source: ${telemetryArrivals}
  - id: order_served
    kind: const
    source: ${telemetryServed}
  - id: order_errors
    kind: const
    values: [0, 0, 0, 0]

outputs:
  - series: order_arrivals
    as: OrderService_arrivals.csv
  - series: order_served
    as: OrderService_served.csv
  - series: order_errors
    as: OrderService_errors.csv
""";
}
