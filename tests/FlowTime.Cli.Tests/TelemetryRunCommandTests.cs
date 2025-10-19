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
    public async Task ExecuteAsync_SimulationMode_CreatesRunSuccessfully()
    {
        using var temp = new TempDirectory();
        var originalDataDir = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        var originalTemplatesDir = Environment.GetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR");
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", temp.Path);

        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        var templatePath = Path.Combine(templatesDir, "sim-order.yaml");
        await File.WriteAllTextAsync(templatePath, SimulationTemplate);
        Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", templatesDir);

        var args = new[]
        {
            "run",
            "--template-id", "sim-order",
            "--mode", "simulation",
            "--deterministic-run-id"
        };

        var errorWriter = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(errorWriter);

        try
        {
            var exitCode = await TelemetryRunCommand.ExecuteAsync(args);
            Console.SetError(originalError);

            if (exitCode != 0)
            {
                throw new Xunit.Sdk.XunitException($"CLI simulation run failed: {errorWriter}");
            }

            Assert.Equal(0, exitCode);
            var runsRoot = Path.Combine(temp.Path, "runs");
            Assert.True(Directory.Exists(runsRoot));
            var runDir = Directory.GetDirectories(runsRoot).Single();
            Assert.True(File.Exists(Path.Combine(runDir, "model", "telemetry", "telemetry-manifest.json")));
            Assert.Equal("simulation", await ReadModeAsync(Path.Combine(runDir, "model", "metadata.json")));
        }
        finally
        {
            Console.SetError(originalError);
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

    private const string SimulationTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order
  title: Simulation CLI Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
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
        arrivals: arrivals
        served: served
        errors: errors
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [10, 12, 14, 16]
  - id: served
    kind: const
    values: [8, 11, 13, 15]
  - id: errors
    kind: const
    values: [1, 0, 0, 0]

outputs:
  - series: "*"
""";

    private static async Task<string> ReadModeAsync(string metadataPath)
    {
        using var stream = File.OpenRead(metadataPath);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("mode").GetString() ?? string.Empty;
    }
}
