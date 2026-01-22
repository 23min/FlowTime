extern alias SimService;

using System;
using System.IO;
using System.Linq;
using FlowTime.Cli.Commands;
using FlowTime.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using SimProgram = SimService::Program;

namespace FlowTime.Cli.Tests;

public class TelemetryRunCommandTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesRunSuccessfully()
    {
        using var temp = new TempDirectory();
        var (originalState, simDataDir, templatesDir) = PrepareSimEnvironment(temp);

        var templatePath = Path.Combine(templatesDir, "test-order.yaml");
        await File.WriteAllTextAsync(templatePath, testTemplate);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "source", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new FlowTime.Generator.TelemetryCapture();
        await capture.ExecuteAsync(new FlowTime.Generator.Models.TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        using var simFactory = new WebApplicationFactory<SimProgram>();
        var client = simFactory.CreateClient();
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL", client.BaseAddress!.ToString());
        TelemetryRunCommand.HttpClientFactoryOverride = () => simFactory.CreateClient();

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
            var runsRoot = Path.Combine(simDataDir, "runs");
            Assert.True(Directory.Exists(runsRoot));
            Assert.True(Directory.GetDirectories(runsRoot).Any());
        }
        finally
        {
            TelemetryRunCommand.HttpClientFactoryOverride = null;
            simFactory.Dispose();
            RestoreSimEnvironment(originalState);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SimulationMode_CreatesRunSuccessfully()
    {
        using var temp = new TempDirectory();
        var (originalState, simDataDir, templatesDir) = PrepareSimEnvironment(temp);

        var templatePath = Path.Combine(templatesDir, "sim-order.yaml");
        await File.WriteAllTextAsync(templatePath, simulationTemplate);

        using var simFactory = new WebApplicationFactory<SimProgram>();
        var client = simFactory.CreateClient();
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL", client.BaseAddress!.ToString());
        TelemetryRunCommand.HttpClientFactoryOverride = () => simFactory.CreateClient();

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
            var runsRoot = Path.Combine(simDataDir, "runs");
            Assert.True(Directory.Exists(runsRoot));
            var runDir = Directory.GetDirectories(runsRoot).Single();
            Assert.True(File.Exists(Path.Combine(runDir, "model", "telemetry", "telemetry-manifest.json")));
            Assert.Equal("simulation", await ReadModeAsync(Path.Combine(runDir, "model", "metadata.json")));
        }
        finally
        {
            Console.SetError(originalError);
            simFactory.Dispose();
            TelemetryRunCommand.HttpClientFactoryOverride = null;
            RestoreSimEnvironment(originalState);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_PrintsPlanWithoutCreatingRun()
    {
        using var temp = new TempDirectory();
        var (originalState, simDataDir, templatesDir) = PrepareSimEnvironment(temp);

        var templatePath = Path.Combine(templatesDir, "test-order.yaml");
        await File.WriteAllTextAsync(templatePath, testTemplate);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "source", includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new FlowTime.Generator.TelemetryCapture();
        await capture.ExecuteAsync(new FlowTime.Generator.Models.TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        using var simFactory = new WebApplicationFactory<SimProgram>();
        var client = simFactory.CreateClient();
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL", client.BaseAddress!.ToString());
        TelemetryRunCommand.HttpClientFactoryOverride = () => simFactory.CreateClient();

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
            var runsRoot = Path.Combine(simDataDir, "runs");
            if (Directory.Exists(runsRoot))
            {
                Assert.Empty(Directory.GetDirectories(runsRoot));
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            simFactory.Dispose();
            TelemetryRunCommand.HttpClientFactoryOverride = null;
            RestoreSimEnvironment(originalState);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MissingTemplate_ReturnsError()
    {
        var exitCode = await TelemetryRunCommand.ExecuteAsync(new[] { "run" });
        Assert.Equal(2, exitCode);
    }

    private const string testTemplate = """
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

    private const string simulationTemplate = """
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

    private static (SimEnvironmentSnapshot Snapshot, string DataDir, string TemplatesDir) PrepareSimEnvironment(TempDirectory temp)
    {
        var snapshot = new SimEnvironmentSnapshot(
            DataDir: Environment.GetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR"),
            TemplatesDir: Environment.GetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR"),
            CliTemplatesDir: Environment.GetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR"),
            StorageRoot: Environment.GetEnvironmentVariable("Storage__Root"),
            ApiBaseUrl: Environment.GetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL"));

        var simDataDir = Path.Combine(temp.Path, "sim-data");
        Directory.CreateDirectory(simDataDir);

        var simTemplatesDir = Path.Combine(temp.Path, "sim-templates");
        Directory.CreateDirectory(simTemplatesDir);

        var cliTemplatesDir = Path.Combine(temp.Path, "cli-templates");
        Directory.CreateDirectory(cliTemplatesDir);

        var storageRoot = Path.Combine(temp.Path, "sim-storage");
        Directory.CreateDirectory(storageRoot);

        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", simDataDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", simTemplatesDir);
        Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", cliTemplatesDir);
        Environment.SetEnvironmentVariable("Storage__Root", storageRoot);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL", null);

        return (snapshot, simDataDir, simTemplatesDir);
    }

    private static void RestoreSimEnvironment(SimEnvironmentSnapshot snapshot)
    {
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", snapshot.DataDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", snapshot.TemplatesDir);
        Environment.SetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR", snapshot.CliTemplatesDir);
        Environment.SetEnvironmentVariable("Storage__Root", snapshot.StorageRoot);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL", snapshot.ApiBaseUrl);
    }

    private sealed record SimEnvironmentSnapshot(
        string? DataDir,
        string? TemplatesDir,
        string? CliTemplatesDir,
        string? StorageRoot,
        string? ApiBaseUrl);
}
