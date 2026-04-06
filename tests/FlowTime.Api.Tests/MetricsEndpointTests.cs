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
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(seriesDir);

        var seriesEntries = WriteBaseSeries(seriesDir);
        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), BuildValidModelYaml(), Encoding.UTF8);
        WriteMetadata(modelDir, identifier);
        WriteSeriesIndex(seriesDir, seriesEntries);

        var runJson = BuildRunJson(identifier, seriesEntries);
        File.WriteAllText(Path.Combine(runDir, "run.json"), runJson, Encoding.UTF8);
    }

    private static (string id, string path, string unit)[] WriteBaseSeries(string seriesDir)
    {
        var entries = new[]
        {
            (id: "OrderService_arrivals@ORDERSERVICE_ARRIVALS@DEFAULT", path: "series/OrderService_arrivals.csv", unit: "count"),
            (id: "OrderService_served@ORDERSERVICE_SERVED@DEFAULT", path: "series/OrderService_served.csv", unit: "count"),
            (id: "OrderService_errors@ORDERSERVICE_ERRORS@DEFAULT", path: "series/OrderService_errors.csv", unit: "count"),
            (id: "SupportQueue_arrivals@SUPPORTQUEUE_ARRIVALS@DEFAULT", path: "series/SupportQueue_arrivals.csv", unit: "count"),
            (id: "SupportQueue_served@SUPPORTQUEUE_SERVED@DEFAULT", path: "series/SupportQueue_served.csv", unit: "count"),
            (id: "SupportQueue_errors@SUPPORTQUEUE_ERRORS@DEFAULT", path: "series/SupportQueue_errors.csv", unit: "count"),
            (id: "SupportQueue_queue@SUPPORTQUEUE_QUEUE@DEFAULT", path: "series/SupportQueue_queue.csv", unit: "count")
        };

        WriteSeries(seriesDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(seriesDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(seriesDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });

        WriteSeries(seriesDir, "SupportQueue_arrivals.csv", new double[] { 9, 7, 9, 5 });
        WriteSeries(seriesDir, "SupportQueue_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(seriesDir, "SupportQueue_errors.csv", new double[] { 0, 0, 0, 0 });
        WriteSeries(seriesDir, "SupportQueue_queue.csv", new double[] { 2, 10, 20, 0 });

        return entries;
    }

    private static string BuildValidModelYaml()
    {
                return string.Join("\n", new[]
                {
                        "schemaVersion: 1",
                        string.Empty,
                        "grid:",
                        $"  bins: {binCount}",
                        $"  binSize: {binSizeMinutes}",
                        "  binUnit: minutes",
                        $"  startTimeUtc: \"{startTimeUtc:O}\"",
                        string.Empty,
                        "topology:",
                        "  nodes:",
                        "    - id: \"OrderService\"",
                        "      kind: \"service\"",
                        "      semantics:",
                        "        arrivals: \"file:../series/OrderService_arrivals.csv\"",
                        "        served: \"file:../series/OrderService_served.csv\"",
                        "        errors: \"file:../series/OrderService_errors.csv\"",
                        "    - id: \"SupportQueue\"",
                        "      kind: \"queue\"",
                        "      semantics:",
                        "        arrivals: \"file:../series/SupportQueue_arrivals.csv\"",
                        "        served: \"file:../series/SupportQueue_served.csv\"",
                        "        errors: \"file:../series/SupportQueue_errors.csv\"",
                        "        queueDepth: \"file:../series/SupportQueue_queue.csv\"",
                        "  edges: []"
                });
    }

    private static void WriteMetadata(string modelDirectory, string identifier)
    {
        var telemetryMetadata = FlowTime.Core.TimeTravel.TelemetrySourceMetadataExtractor.Extract(
            File.ReadAllText(Path.Combine(modelDirectory, "model.yaml"), Encoding.UTF8));

        var metadata = new
        {
            templateId = "order-system",
            templateTitle = "Order System Fixture",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode = "telemetry",
            modelHash = $"sha256:{identifier}",
            telemetrySources = telemetryMetadata.TelemetrySources,
            nodeSources = telemetryMetadata.NodeSources
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

    private static string BuildRunJson(string identifier, IReadOnlyList<(string id, string path, string unit)> seriesEntries)
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
            series = seriesEntries.Select(entry => new
            {
                id = entry.id,
                path = entry.path,
                unit = entry.unit
            }).ToArray()
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static void WriteSeriesIndex(string seriesDirectory, IReadOnlyCollection<(string id, string path, string unit)> entries)
    {
        var payload = new
        {
            schemaVersion = 1,
            grid = new
            {
                bins = binCount,
                binSize = binSizeMinutes,
                binUnit = "minutes"
            },
            series = entries.Select(entry => new
            {
                id = entry.id,
                kind = "derived",
                path = entry.path,
                unit = entry.unit,
                componentId = ExtractComponentId(entry.id),
                @class = ExtractClassId(entry.id),
                classKind = ExtractClassKind(entry.id),
                points = binCount,
                hash = $"sha256:{entry.id}"
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(seriesDirectory, "index.json"), json, Encoding.UTF8);
    }

    private static string ExtractComponentId(string id)
    {
        var firstAt = id.IndexOf('@');
        if (firstAt < 0 || firstAt == id.Length - 1)
        {
            return id;
        }

        var remainder = id[(firstAt + 1)..];
        var secondAt = remainder.IndexOf('@');
        return secondAt < 0 ? remainder : remainder[..secondAt];
    }

    private static string ExtractClassId(string id)
    {
        var firstAt = id.IndexOf('@');
        if (firstAt < 0)
        {
            return "DEFAULT";
        }

        var remainder = id[(firstAt + 1)..];
        var secondAt = remainder.IndexOf('@');
        if (secondAt < 0 || secondAt == remainder.Length - 1)
        {
            return "DEFAULT";
        }

        return remainder[(secondAt + 1)..];
    }

    private static string ExtractClassKind(string id)
    {
        var classId = ExtractClassId(id);
        return string.Equals(classId, "DEFAULT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(classId, "*", StringComparison.OrdinalIgnoreCase)
            ? "fallback"
            : "specific";
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
