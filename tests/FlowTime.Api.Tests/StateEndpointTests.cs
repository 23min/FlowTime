using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.TimeTravel;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Sdk;

namespace FlowTime.Api.Tests;

public class StateEndpointTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string runId = "run_state_fixture";
    private const string invalidRunId = "run_state_invalid";
    private const string telemetryWarningRunId = "run_state_warning";
    private const string simulationInvalidRunId = "run_state_sim_invalid";
    private const int binCount = 4;
    private const int binSizeMinutes = 5;
    private static readonly DateTime startTimeUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string artifactsRoot;
    private readonly HttpClient client;

    public StateEndpointTests(TestWebApplicationFactory factory)
    {
        artifactsRoot = Path.Combine(Path.GetTempPath(), $"flowtime_state_fixture_{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);
        CreateFixtureRun();
        CreateInvalidRun();
        CreateTelemetryWarningRun();
        CreateSimulationInvalidRun();

        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsRoot);
        }).CreateClient();
    }

    [Fact]
    public async Task GetState_ReturnsSnapshotWithDerivedMetrics()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state?binIndex=1");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"telemetry\"", responseBody);

        var payload = JsonSerializer.Deserialize<StateSnapshotResponse>(responseBody, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);
        var manifestMetadata = await new RunManifestReader().ReadAsync(Path.Combine(artifactsRoot, runId, "model"), CancellationToken.None);
        Assert.NotEmpty(manifestMetadata.TelemetrySources);

        Assert.Equal(runId, payload!.Metadata.RunId);
        Assert.Equal("telemetry", payload.Metadata.Mode);
        Assert.True(payload.Metadata.TelemetrySourcesResolved);
        Assert.Empty(payload.Warnings);

        Assert.Equal(1, payload.Bin.Index);
        Assert.Equal(startTimeUtc.AddMinutes(binSizeMinutes), payload.Bin.StartUtc?.UtcDateTime);

        var service = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Equal("service", service.Kind);
        Assert.Equal(10, service.Metrics.Arrivals);
        Assert.Equal(6, service.Metrics.Served);
        Assert.Equal(1, service.Metrics.Errors);
        Assert.Equal(0.85714, service.Derived.Utilization!.Value, 5);
        Assert.Equal("yellow", service.Derived.Color);
        Assert.Contains(service.Telemetry.Sources, s => s.Contains("OrderService_arrivals") || s.Contains("OrderService_served"));
        Assert.Empty(service.Telemetry.Warnings);

        var queue = Assert.Single(payload.Nodes, n => n.Id == "SupportQueue");
        Assert.Equal("queue", queue.Kind);
        Assert.Equal(10, queue.Metrics.Queue);
        Assert.Equal(8.33333, queue.Derived.LatencyMinutes!.Value, 5);
        Assert.Equal("red", queue.Derived.Color);
        Assert.Contains(queue.Telemetry.Sources, s => s.Contains("SupportQueue_arrivals"));
        Assert.Empty(queue.Telemetry.Warnings);
    }

    [Fact]
    public async Task GetStateWindow_ReturnsAlignedSeries()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        Assert.Equal(runId, payload!.Metadata.RunId);
        Assert.Equal(4, payload.Window.BinCount);
        Assert.Equal(4, payload.TimestampsUtc.Count);
        Assert.Equal(startTimeUtc, payload.TimestampsUtc[0].UtcDateTime);
        Assert.Equal(startTimeUtc.AddMinutes(3 * binSizeMinutes), payload.TimestampsUtc[^1].UtcDateTime);

        var serviceSeries = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Equal(new double?[] { 10.0, 10.0, 10.0, 10.0 }, serviceSeries.Series["arrivals"]);
        Assert.Equal(new double?[] { 9.0, 6.0, 9.0, 4.0 }, serviceSeries.Series["served"]);
        Assert.Equal(new double?[] { 12.0, 7.0, 9.0, 4.0 }, serviceSeries.Series["capacity"]);
        Assert.Empty(payload.Warnings);
        Assert.Empty(serviceSeries.Telemetry.Warnings);

        var utilization = serviceSeries.Series.ContainsKey("utilization") ? serviceSeries.Series["utilization"] : Array.Empty<double?>();
        Assert.Equal(4, utilization.Length);
        Assert.Equal(0.75, utilization[0]!.Value, 5);
        Assert.Equal(1.0, utilization[2]!.Value, 5);

        var queueSeries = Assert.Single(payload.Nodes, n => n.Id == "SupportQueue");
        Assert.True(queueSeries.Series.ContainsKey("latencyMinutes"));
        var latency = queueSeries.Series["latencyMinutes"];
        Assert.Equal(4, latency.Length);
        Assert.Equal(1.11111, latency[0]!.Value, 5);
        Assert.Equal(8.33333, latency[1]!.Value, 5);
        Assert.Empty(queueSeries.Telemetry.Warnings);
    }

    [Fact]
    public async Task GetState_TelemetryWithInvalidSeries_ReturnsWarnings()
    {
        var response = await client.GetAsync($"/v1/runs/{telemetryWarningRunId}/state?binIndex=0");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateSnapshotResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var service = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");
        Assert.Contains(service.Telemetry.Warnings, w => w.Code == "telemetry_series_invalid");
        Assert.Equal("warning", Assert.Single(service.Telemetry.Warnings).Severity);
        Assert.Empty(payload.Warnings);
    }

    [Fact]
    public async Task GetState_SimulationWithInvalidSeries_ReturnsModeValidationError()
    {
        var response = await client.GetAsync($"/v1/runs/{simulationInvalidRunId}/state?binIndex=0");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mode_validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("invalid values", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetState_WithUnknownRun_ReturnsNotFound()
    {
        var response = await client.GetAsync("/v1/runs/unknown/state?binIndex=0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task GetState_WithSelfShiftMissingInitial_ReturnsConflict()
    {
        var response = await client.GetAsync($"/v1/runs/{invalidRunId}/state?binIndex=0");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = error.GetProperty("error").GetString();
        Assert.Contains("Model metadata for run", message);
        Assert.Contains("uses SHIFT on itself and requires an initial condition", message);
    }

    private void CreateFixtureRun()
    {
        CreateRun(runId, BuildValidModelYaml(), mode: "telemetry");
    }

    private void CreateInvalidRun()
    {
        CreateRun(invalidRunId, BuildInvalidModelYaml(), mode: "telemetry");
    }

    private void CreateTelemetryWarningRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_served.csv"] = new double[] { double.NaN, 6, 9, 4 }
        };

        CreateRun(telemetryWarningRunId, BuildValidModelYaml(), mode: "telemetry", overrides);
    }

    private void CreateSimulationInvalidRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_served.csv"] = new double[] { double.NaN, double.NaN, double.NaN, double.NaN }
        };

        CreateRun(simulationInvalidRunId, BuildValidModelYaml(), mode: "simulation", overrides);
    }

    private void CreateRun(string runIdentifier, string modelYaml, string mode, Dictionary<string, double[]>? overrides = null)
    {
        var runDir = Path.Combine(artifactsRoot, runIdentifier);
        var modelDir = Path.Combine(runDir, "model");
        Directory.CreateDirectory(modelDir);

        WriteBaseSeries(modelDir);
        if (overrides != null)
        {
            foreach (var (fileName, values) in overrides)
            {
                WriteSeries(modelDir, fileName, values);
            }
        }
        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), modelYaml, System.Text.Encoding.UTF8);
        WriteMetadata(modelDir, runIdentifier, mode);
        File.WriteAllText(Path.Combine(runDir, "run.json"), BuildRunJson(runIdentifier, mode), System.Text.Encoding.UTF8);
    }

    private static void WriteBaseSeries(string modelDir)
    {
        WriteSeries(modelDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(modelDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_capacity.csv", new double[] { 12, 7, 9, 4 });

        WriteSeries(modelDir, "SupportQueue_arrivals.csv", new double[] { 9, 7, 9, 5 });
        WriteSeries(modelDir, "SupportQueue_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "SupportQueue_errors.csv", new double[] { 0, 0, 0, 0 });
        WriteSeries(modelDir, "SupportQueue_queue.csv", new double[] { 2, 10, 20, 0 });
    }

    private static void WriteSeries(string directory, string fileName, IReadOnlyList<double> values)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path);
        writer.NewLine = "\n";
        writer.WriteLine("bin_index,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{values[i]}"));
        }
    }

    private static void WriteMetadata(string modelDirectory, string runIdentifier, string mode)
    {
        var metadata = new
        {
            templateId = "order-system",
            templateTitle = "Order System Fixture",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode,
            modelHash = $"sha256:{runIdentifier}"
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "metadata.json"), metadataJson, System.Text.Encoding.UTF8);

        var provenance = new
        {
            source = "flowtime-sim",
            templateId = "order-system",
            templateVersion = "1.0.0",
            mode = "telemetry",
            modelId = runIdentifier,
            schemaVersion = 1
        };

        var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "provenance.json"), provenanceJson, System.Text.Encoding.UTF8);
    }

    private static string BuildRunJson(string runIdentifier, string mode)
    {
        var grid = new
        {
            bins = binCount,
            binSize = binSizeMinutes,
            binUnit = "minutes",
            binMinutes = binSizeMinutes,
            timezone = "UTC",
            align = "left"
        };

        var manifest = new
        {
            schemaVersion = 1,
            runId = runIdentifier,
            engineVersion = "0.0-test",
            source = mode,
            grid,
            modelHash = $"sha256:{runIdentifier}",
            scenarioHash = "sha256:test",
            createdUtc = startTimeUtc.ToString("o", CultureInfo.InvariantCulture),
            warnings = Array.Empty<string>(),
            series = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static string BuildValidModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        slaMin: null
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queue: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
  edges: []

""";
    }

    private static string BuildInvalidModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

nodes:
  - id: "SupportQueue"
    kind: "expr"
    expr: "SHIFT(SupportQueue, 1)"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        slaMin: null
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queue: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
  edges: []

""";
    }

    public void Dispose()
    {
        client.Dispose();
        if (Directory.Exists(artifactsRoot))
        {
            Directory.Delete(artifactsRoot, recursive: true);
        }
    }
}
