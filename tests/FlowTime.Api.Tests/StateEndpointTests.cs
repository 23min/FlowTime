using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTime.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Sdk;

namespace FlowTime.Api.Tests;

public class StateEndpointTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string runId = "run_state_fixture";
    private const string invalidRunId = "run_state_invalid";
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

        var payload = await response.Content.ReadFromJsonAsync<StateResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);
        Assert.Equal(runId, payload!.RunId);
        Assert.Equal("telemetry", payload.Mode);
        Assert.Equal(1, payload.Bin.Index);
        Assert.Equal(startTimeUtc.AddMinutes(binSizeMinutes), payload.Bin.StartUtc);

        Assert.Contains("OrderService", payload.Nodes.Keys);
        var service = payload.Nodes["OrderService"];
        Assert.Equal("service", service.Kind);
        Assert.Equal(10, service.Arrivals!.Value);
        Assert.Equal(6, service.Served!.Value);
        Assert.Equal(1, service.Errors!.Value);
        Assert.Equal(0.85714, service.Utilization!.Value, 5);
        Assert.Equal("yellow", service.Color);

        Assert.Contains("SupportQueue", payload.Nodes.Keys);
        var queue = payload.Nodes["SupportQueue"];
        Assert.Equal("queue", queue.Kind);
        Assert.Equal(10, queue.Queue!.Value);
        Assert.Equal(8.33333, queue.LatencyMinutes!.Value, 5);
        Assert.Equal("red", queue.Color);
        Assert.Equal(5, queue.SlaMinutes);
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

        Assert.Equal(runId, payload!.RunId);
        Assert.Equal(4, payload.Slice.Bins);
        Assert.Equal(startTimeUtc, payload.Window.Start);
        Assert.Equal(4, payload.Timestamps.Count);
        Assert.Equal(startTimeUtc, payload.Timestamps[0]);
        Assert.Equal(startTimeUtc.AddMinutes(3 * binSizeMinutes), payload.Timestamps[^1]);

        var serviceSeries = Assert.Contains("OrderService", payload.Nodes).Series;
        Assert.Equal(new double?[] { 10.0, 10.0, 10.0, 10.0 }, serviceSeries["arrivals"]);
        Assert.Equal(new double?[] { 9.0, 6.0, 9.0, 4.0 }, serviceSeries["served"]);
        Assert.Equal(new double?[] { 12.0, 7.0, 9.0, 4.0 }, serviceSeries["capacity"]);

        var utilization = serviceSeries["utilization"];
        Assert.Equal(4, utilization.Length);
        Assert.Equal(0.75, utilization[0]!.Value, 5);
        Assert.Equal(1.0, utilization[2]!.Value, 5);

        var queueSeries = Assert.Contains("SupportQueue", payload.Nodes).Series;
        Assert.True(queueSeries.ContainsKey("latencyMinutes"));
        var latency = queueSeries["latencyMinutes"];
        Assert.Equal(4, latency.Length);
        Assert.Equal(1.11111, latency[0]!.Value, 5);
        Assert.Equal(8.33333, latency[1]!.Value, 5);
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
        CreateRun(runId, BuildValidModelYaml());
    }

    private void CreateInvalidRun()
    {
        CreateRun(invalidRunId, BuildInvalidModelYaml());
    }

    private void CreateRun(string runIdentifier, string modelYaml)
    {
        var runDir = Path.Combine(artifactsRoot, runIdentifier);
        var modelDir = Path.Combine(runDir, "model");
        Directory.CreateDirectory(modelDir);

        WriteBaseSeries(modelDir);
        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), modelYaml, System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runDir, "run.json"), BuildRunJson(runIdentifier), System.Text.Encoding.UTF8);
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

    private static string BuildRunJson(string runIdentifier)
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
            source = "engine",
            grid,
            modelHash = "sha256:test",
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
