using System.Diagnostics.Metrics;
using FlowTime.Generator.Models;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Services;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Logging;
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

        var outcome = await orchestration.CreateRunAsync(request);

        Assert.NotNull(outcome);
        Assert.False(outcome.IsDryRun);
        Assert.NotNull(outcome.Result);
        var result = outcome.Result!;
        Assert.True(result.TelemetrySourcesResolved);
        Assert.True(Directory.Exists(result.RunDirectory));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "model", "model.yaml")));
        Assert.Equal("telemetry", result.ManifestMetadata.Mode);
        Assert.Null(outcome.Plan);
    }

    [Fact]
    public async Task CreateRunAsync_DryRun_ReturnsPlan()
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
            DryRun = true,
            OverwriteExisting = false
        };

        var outcome = await orchestration.CreateRunAsync(request);

        Assert.True(outcome.IsDryRun);
        Assert.Null(outcome.Result);
        Assert.NotNull(outcome.Plan);
        var plan = outcome.Plan!;
        Assert.Equal("test-order", plan.TemplateId);
        Assert.Equal("telemetry", plan.Mode);
        Assert.Equal(Path.Combine(temp.Path, "runs"), plan.OutputRoot);
        Assert.Equal(captureDir, plan.CaptureDirectory);
        Assert.True(plan.Files.Count > 0);
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_WritesArtifactsAndManifest()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-order.yaml");
        await File.WriteAllTextAsync(templatePath, SimulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);

        Assert.False(outcome.IsDryRun);
        Assert.NotNull(outcome.Result);
        var result = outcome.Result!;

        Assert.Equal("simulation", result.ManifestMetadata.Mode);
        Assert.True(result.TelemetrySourcesResolved);
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "model", "model.yaml")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "series", "index.json")));

        var telemetryManifestPath = Path.Combine(result.RunDirectory, "model", "telemetry", "telemetry-manifest.json");
        Assert.True(File.Exists(telemetryManifestPath));

        var manifestJson = await File.ReadAllTextAsync(telemetryManifestPath);
        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        Assert.Equal("simulation", result.ManifestMetadata.Mode);
        Assert.Equal(0, doc.RootElement.GetProperty("files").GetArrayLength());
        Assert.Equal("sim-order", result.ManifestMetadata.TemplateId);
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_CarriesRunWarningsIntoTelemetryManifest()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), SimulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result!;

        // Missing capacity series on the service node should emit an info-level warning.
        var manifestPath = Path.Combine(result.RunDirectory, "model", "telemetry", "telemetry-manifest.json");
        Assert.True(File.Exists(manifestPath));

        using var manifest = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var warnings = manifest.RootElement.GetProperty("warnings");
        Assert.True(warnings.GetArrayLength() > 0);
        var first = warnings[0];
        Assert.Equal("missing_capacity_series", first.GetProperty("code").GetString());
        Assert.Equal("info", first.GetProperty("severity").GetString());
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_EmitsMetricsAndLogs()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), SimulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var logger = new TestLogger<RunOrchestrationService>();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, logger);

        var metrics = new List<(string Name, double Value)>();
        var counters = new List<(string Name, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listenerHandle) =>
        {
            if (instrument.Meter.Name == "FlowTime.RunOrchestration")
            {
                listenerHandle.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            counters.Add((instrument.Name, measurement));
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            metrics.Add((instrument.Name, measurement));
        });
        listener.Start();

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>(),
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);

        listener.RecordObservableInstruments();
        listener.Dispose();

        Assert.False(outcome.IsDryRun);
        Assert.Contains(counters, c => c.Name == "run_created_total" && c.Value == 1);
        Assert.Contains(metrics, m => m.Name == "simulation_evaluation_duration_ms" && m.Value > 0);
        Assert.Contains(logger.Entries, entry => entry.EventId.Name == "RunOrchestrationCompleted" && entry.Message.Contains("Completed simulation run"));
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMissingWindow_ThrowsTemplateValidation()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-invalid.yaml");
        await File.WriteAllTextAsync(templatePath, SimulationTemplateMissingWindow);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-invalid",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>(),
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        await Assert.ThrowsAsync<FlowTime.Sim.Core.Templates.Exceptions.TemplateValidationException>(() => orchestration.CreateRunAsync(request));
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

    private const string SimulationTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order
  title: Simulation Order Template
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

    private const string SimulationTemplateMissingWindow = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-invalid
  title: Missing Window Template
  version: 1.0.0

grid:
  bins: 4
  binSize: 5
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
""";

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(EventId EventId, LogLevel LogLevel, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((eventId, logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
