using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Contracts.TimeTravel;
using Xunit;

namespace FlowTime.Api.Tests;

public sealed class MetricsEndpointTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string runId = "run_metrics_fixture";
    private const int binCount = 4;
    private const int binSizeMinutes = 5;
    private static readonly DateTime startTimeUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string artifactsRoot;
    private readonly HttpClient client;

    public MetricsEndpointTests(TestWebApplicationFactory factory)
    {
        artifactsRoot = Path.Combine(Path.GetTempPath(), $"flowtime_metrics_fixture_{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        CreateRun(runId);

        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsRoot);
            builder.UseSetting("DataDirectory", artifactsRoot);
        }).CreateClient();
    }

    [Fact]
    public async Task GetMetrics_ReturnsExpectedAggregates()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/metrics?startBin=0&endBin={binCount - 1}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MetricsResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal("UTC", payload!.Window.Timezone);
        Assert.Equal(startTimeUtc, payload.Window.Start?.UtcDateTime);
        Assert.Equal(binSizeMinutes, payload.Grid.BinMinutes);
        Assert.Equal(binCount, payload.Grid.Bins);

        Assert.Equal(2, payload.Services.Count);

        var orderService = payload.Services.Single(s => s.Id == "OrderService");
        Assert.Equal(0, orderService.BinsMet);
        Assert.Equal(binCount, orderService.BinsTotal);
        Assert.Equal(0, orderService.SlaPct);
        Assert.Equal(binCount, orderService.Mini.Count);
        Assert.Equal(0.9, orderService.Mini[0]!.Value, 4);
        Assert.Equal(0.6, orderService.Mini[1]!.Value, 4);
        Assert.Equal(0.9, orderService.Mini[2]!.Value, 4);
        Assert.Equal(0.4, orderService.Mini[3]!.Value, 4);

        var queue = payload.Services.Single(s => s.Id == "SupportQueue");
        Assert.Equal(2, queue.BinsMet);
        Assert.Equal(binCount, queue.BinsTotal);
        Assert.Equal(0.5, queue.SlaPct, 3);
        Assert.Equal(1.0, queue.Mini[0]!.Value, 4);
        Assert.Equal(0.8571, queue.Mini[1]!.Value, 4);
        Assert.Equal(1.0, queue.Mini[2]!.Value, 4);
        Assert.Equal(0.8, queue.Mini[3]!.Value, 4);

        var sanitized = SanitizeMetricsResponse(payload);
        GoldenTestUtils.AssertMatchesGolden("metrics-run_metrics_fixture.json", sanitized);
    }

    [Fact]
    public async Task GetMetrics_DefaultsToTailWindow()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MetricsResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal(binCount, payload!.Grid.Bins);
    }

    [Fact]
    public async Task GetMetrics_InvalidQueryReturns400()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/metrics?startBin=abc");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("startBin must be an integer", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetrics_EndBeforeStartReturns400()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/metrics?startBin=2&endBin=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("endBin must be greater than or equal to startBin", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetrics_MissingRunReturns404()
    {
        var response = await client.GetAsync("/v1/runs/missing_run/metrics?startBin=0&endBin=1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(artifactsRoot))
            {
                Directory.Delete(artifactsRoot, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private void CreateRun(string identifier)
    {
        var runDir = Path.Combine(artifactsRoot, identifier);
        var modelDir = Path.Combine(runDir, "model");
        Directory.CreateDirectory(modelDir);

        WriteBaseSeries(modelDir);
        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), BuildValidModelYaml(), Encoding.UTF8);
        WriteMetadata(modelDir, identifier);

        var runJson = BuildRunJson(identifier);
        File.WriteAllText(Path.Combine(runDir, "run.json"), runJson, Encoding.UTF8);
    }

    private static void WriteBaseSeries(string modelDir)
    {
        WriteSeries(modelDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(modelDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });

        WriteSeries(modelDir, "SupportQueue_arrivals.csv", new double[] { 9, 7, 9, 5 });
        WriteSeries(modelDir, "SupportQueue_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "SupportQueue_errors.csv", new double[] { 0, 0, 0, 0 });
        WriteSeries(modelDir, "SupportQueue_queue.csv", new double[] { 2, 10, 20, 0 });
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
        capacity: null
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        queueDepth: "file:SupportQueue_queue.csv"
""";
    }

    private static void WriteMetadata(string modelDirectory, string identifier)
    {
        var metadata = new
        {
            templateId = "order-system",
            templateTitle = "Order System Fixture",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode = "telemetry",
            modelHash = $"sha256:{identifier}"
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "metadata.json"), json, Encoding.UTF8);

        var provenance = new
        {
            source = "flowtime-sim",
            templateId = "order-system",
            templateVersion = "1.0.0",
            mode = "telemetry",
            modelId = identifier,
            schemaVersion = 1
        };

        var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "provenance.json"), provenanceJson, Encoding.UTF8);
    }

    private static string BuildRunJson(string identifier)
    {
        var grid = new
        {
            bins = binCount,
            binSize = binSizeMinutes,
            binUnit = "minutes",
            timezone = "UTC",
            align = "left"
        };

        var manifest = new
        {
            schemaVersion = 1,
            runId = identifier,
            engineVersion = "0.0-test",
            source = "telemetry",
            grid,
            modelHash = $"sha256:{identifier}",
            scenarioHash = "sha256:test",
            createdUtc = startTimeUtc.ToString("o", CultureInfo.InvariantCulture),
            warnings = Array.Empty<string>(),
            series = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static void WriteSeries(string directory, string fileName, IReadOnlyList<double> values)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.NewLine = "\n";
        writer.WriteLine("bin_index,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{values[i]}"));
        }
    }

    private static JsonNode SanitizeMetricsResponse(MetricsResponse response)
    {
        var node = JsonSerializer.SerializeToNode(response, GoldenTestUtils.SerializerOptions)
                   ?? throw new InvalidOperationException("Metrics response serialization failed.");

        if (node is JsonObject obj && obj["services"] is JsonArray services)
        {
            foreach (var service in services.OfType<JsonObject>())
            {
                if (service["slaPct"] is JsonValue slaValue && slaValue.TryGetValue<double>(out var slaPct))
                {
                    service["slaPct"] = JsonValue.Create(Math.Round(slaPct, 6));
                }

                if (service["mini"] is JsonArray mini)
                {
                    for (var i = 0; i < mini.Count; i++)
                    {
                        if (mini[i] is JsonValue miniValue && miniValue.TryGetValue<double>(out var value))
                        {
                            mini[i] = JsonValue.Create(Math.Round(value, 6));
                        }
                    }
                }
            }
        }

        return node;
    }
}
