using System.Diagnostics.Metrics;
using System.Globalization;
using FlowTime.Contracts.Services;
using FlowTime.Generator.Models;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Hashing;
using FlowTime.Sim.Core.Services;
using FlowTime.Tests.Support;
using FlowTime.Core.Compiler;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
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
        await File.WriteAllTextAsync(templatePath, testTemplate);

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
        await File.WriteAllTextAsync(templatePath, testTemplate);

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
        Assert.True(plan.TelemetryManifest.Files.Count > 0);
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_WritesArtifactsAndManifest()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-order.yaml");
        await File.WriteAllTextAsync(templatePath, simulationTemplate);

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
    public async Task CreateRunAsync_SimulationMode_UsesCompiledModelForDerivedSeries()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-queue.yaml");
        await File.WriteAllTextAsync(templatePath, simulationQueueTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-queue",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 3,
                ["binSize"] = 60
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result!;

        var queuePath = Path.Combine(result.RunDirectory, "series", "queue_depth@QUEUE_DEPTH@DEFAULT.csv");
        var actualQueue = ReadSeriesValues(queuePath);

        var resolvedYaml = simulationQueueTemplate
            .Replace("${bins}", "3", StringComparison.Ordinal)
            .Replace("${binSize}", "60", StringComparison.Ordinal);
        var canonicalModel = ModelService.ParseAndConvert(resolvedYaml);
        var compiledModel = ModelCompiler.Compile(canonicalModel);
        var (grid, graph) = ModelParser.ParseModel(compiledModel);
        var evaluated = graph.Evaluate(grid);
        var expectedQueue = evaluated[new NodeId("queue_depth")].ToArray();

        Assert.Equal(expectedQueue, actualQueue);
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_WritesEdgeSeries()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-edge.yaml");
        await File.WriteAllTextAsync(templatePath, simulationEdgeTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-edge",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 2,
                ["binSize"] = 5
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result!;

        var edgePath = Path.Combine(result.RunDirectory, "series", "edge_source_to_sink_flowVolume@EDGE_SOURCE_TO_SINK_FLOWVOLUME@DEFAULT.csv");
        Assert.True(File.Exists(edgePath), "Expected edge flow series to be emitted for simulation runs.");
        Assert.Equal(new[] { 4d, 6d }, ReadSeriesValues(edgePath));
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_WritesEdgeSeriesByClass()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);

        var templatePath = Path.Combine(templatesDir, "sim-edge-classes.yaml");
        await File.WriteAllTextAsync(templatePath, simulationEdgeClassTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-edge-classes",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 2,
                ["binSize"] = 5
            },
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result!;

        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var alphaPath = Path.Combine(seriesDir, "edge_source_to_sink_flowVolume@EDGE_SOURCE_TO_SINK_FLOWVOLUME@Alpha.csv");
        var betaPath = Path.Combine(seriesDir, "edge_source_to_sink_flowVolume@EDGE_SOURCE_TO_SINK_FLOWVOLUME@Beta.csv");

        Assert.True(File.Exists(alphaPath), "Expected Alpha class edge flow series to be emitted.");
        Assert.True(File.Exists(betaPath), "Expected Beta class edge flow series to be emitted.");
        Assert.Equal(new[] { 2d, 2d }, ReadSeriesValues(alphaPath));
        Assert.Equal(new[] { 1d, 3d }, ReadSeriesValues(betaPath));
    }

    [Fact]
    public async Task CreateRunAsync_DeterministicSimulation_RunIdUsesInputHash()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), simulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>(),
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result!;

        var expectedHash = RunHashCalculator.ComputeHash(new RunHashInput(
            "sim-order",
            "1.0.0",
            "simulation",
            new Dictionary<string, object?>(),
            new Dictionary<string, string>(),
            "pcg32",
            123));
        var expectedRunId = $"run_sim-order_{expectedHash[7..]}";

        Assert.Equal(expectedRunId, result.RunId);
        Assert.EndsWith(expectedRunId, result.RunDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.WasReused);
    }

    [Fact]
    public async Task CreateRunAsync_DeterministicSimulation_ReusesExistingBundle()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), simulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>(),
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true
        };

        var first = (await orchestration.CreateRunAsync(request)).Result!;
        var sentinel = Path.Combine(first.RunDirectory, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");

        var second = (await orchestration.CreateRunAsync(request)).Result!;

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.RunDirectory, second.RunDirectory);
        Assert.True(File.Exists(sentinel));
        Assert.False(first.WasReused);
        Assert.True(second.WasReused);
    }

    [Fact]
    public async Task CreateRunAsync_DeterministicSimulation_OverwriteRegeneratesBundle()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), simulationTemplate);

        var templateService = new TemplateService(templatesDir, NullLogger<TemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var baseRequest = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            Parameters = new Dictionary<string, object?>(),
            OutputRoot = Path.Combine(temp.Path, "runs"),
            DeterministicRunId = true,
            OverwriteExisting = false
        };

        var first = (await orchestration.CreateRunAsync(baseRequest)).Result!;
        var sentinel = Path.Combine(first.RunDirectory, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "marker");

        var overwriteRequest = baseRequest with { OverwriteExisting = true };
        var second = (await orchestration.CreateRunAsync(overwriteRequest)).Result!;

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.RunDirectory, second.RunDirectory);
        Assert.False(second.WasReused);
        Assert.False(File.Exists(sentinel));
    }

    [Fact]
    public async Task CreateRunAsync_SimulationMode_CarriesRunWarningsIntoTelemetryManifest()
    {
        using var temp = new TempDirectory();
        var templatesDir = Path.Combine(temp.Path, "templates");
        Directory.CreateDirectory(templatesDir);
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), simulationTemplate);

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
        await File.WriteAllTextAsync(Path.Combine(templatesDir, "sim-order.yaml"), simulationTemplate);

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
        await File.WriteAllTextAsync(templatePath, simulationTemplateMissingWindow);

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

    private const string testTemplate = """
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

    private const string simulationTemplate = """
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

    private const string simulationEdgeTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-edge
  title: Simulation Edge Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 2
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: Source
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
    - id: Sink
      kind: sink
      nodeRole: sink
      semantics:
        arrivals: served
        served: served
  edges:
    - id: source_to_sink
      from: Source:out
      to: Sink:in

nodes:
  - id: arrivals
    kind: const
    values: [5, 7]
  - id: served
    kind: const
    values: [4, 6]
  - id: errors
    kind: const
    values: [0, 0]

outputs:
  - series: "*"
""";

    private const string simulationEdgeClassTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-edge-classes
  title: Simulation Edge Class Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 2
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

classes:
  - id: Alpha
  - id: Beta

traffic:
  arrivals:
    - nodeId: arrivals_alpha
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: arrivals_beta
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1

topology:
  nodes:
    - id: Source
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
    - id: Sink
      kind: sink
      nodeRole: sink
      semantics:
        arrivals: served
        served: served
  edges:
    - id: source_to_sink
      from: Source:out
      to: Sink:in

nodes:
  - id: arrivals_alpha
    kind: const
    values: [2, 2]
  - id: arrivals_beta
    kind: const
    values: [1, 3]
  - id: arrivals
    kind: expr
    expr: "arrivals_alpha + arrivals_beta"
  - id: served
    kind: expr
    expr: "arrivals"
  - id: errors
    kind: const
    values: [0, 0]

outputs:
  - series: "*"
""";

    private const string simulationQueueTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-queue
  title: Simulation Queue Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 3
  - name: binSize
    type: integer
    default: 60

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: QueueNode
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
        queueDepth: queue_depth
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [5, 5, 5]
  - id: served
    kind: const
    values: [3, 3, 3]
  - id: errors
    kind: const
    values: [1, 0, 1]

outputs:
  - series: "*"
""";

    private static double[] ReadSeriesValues(string path)
    {
        var lines = File.ReadAllLines(path);
        var values = new List<double>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 2)
            {
                continue;
            }

            values.Add(double.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        return values.ToArray();
    }

    private const string simulationTemplateMissingWindow = """
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
