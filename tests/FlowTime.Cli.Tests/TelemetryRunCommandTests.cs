using System;
using System.IO;
using System.Linq;
using FlowTime.Cli.Commands;
using FlowTime.Tests.Support;

namespace FlowTime.Cli.Tests;

public class TelemetryRunCommandTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesRunSuccessfully()
    {
        using var temp = new TempDirectory();
        var originalDataDir = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        var originalTemplatesDir = Environment.GetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR");
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", temp.Path);

        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        var templatePath = Path.Combine(templatesDir, "test-order.yaml");
        await File.WriteAllTextAsync(templatePath, TestTemplate);
        Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", templatesDir);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "source", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new FlowTime.Generator.TelemetryCapture();
        await capture.ExecuteAsync(new FlowTime.Generator.Models.TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        var args = new[]
        {
            "run",
            "--template-id", "test-order",
            "--capture-dir", captureDir,
            "--bind", "telemetryArrivals=OrderService_arrivals.csv",
            "--bind", "telemetryServed=OrderService_served.csv",
            "--deterministic-run-id"
        };

        try
        {
            var exitCode = await TelemetryRunCommand.ExecuteAsync(args);

            Assert.Equal(0, exitCode);
            var runsRoot = Path.Combine(temp.Path, "runs");
            Assert.True(Directory.Exists(runsRoot));
            Assert.True(Directory.GetDirectories(runsRoot).Any());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", originalDataDir);
            Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", originalTemplatesDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_PrintsPlanWithoutCreatingRun()
    {
        using var temp = new TempDirectory();
        var originalDataDir = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        var originalTemplatesDir = Environment.GetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR");
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", temp.Path);

        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        var templatePath = Path.Combine(templatesDir, "test-order.yaml");
        await File.WriteAllTextAsync(templatePath, TestTemplate);
        Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", templatesDir);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "source", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new FlowTime.Generator.TelemetryCapture();
        await capture.ExecuteAsync(new FlowTime.Generator.Models.TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        var args = new[]
        {
            "run",
            "--template-id", "test-order",
            "--capture-dir", captureDir,
            "--bind", "telemetryArrivals=OrderService_arrivals.csv",
            "--bind", "telemetryServed=OrderService_served.csv",
            "--dry-run"
        };

        var output = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(output);
            var exitCode = await TelemetryRunCommand.ExecuteAsync(args);
            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("Run created", output.ToString(), StringComparison.OrdinalIgnoreCase);
            var runsRoot = Path.Combine(temp.Path, "runs");
            Assert.False(Directory.Exists(runsRoot));
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", originalDataDir);
            Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", originalTemplatesDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MissingTemplate_ReturnsError()
    {
        var exitCode = await TelemetryRunCommand.ExecuteAsync(new[] { "run" });
        Assert.Equal(2, exitCode);
    }

    private const string TestTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-order
  title: CLI Run Test Template
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

grid:
  bins: 4
  binSize: 5
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
