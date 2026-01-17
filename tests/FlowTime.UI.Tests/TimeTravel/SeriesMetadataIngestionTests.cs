using System.Collections;
using System.Text.Json;
using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class SeriesMetadataIngestionTests
{
    [Fact]
    public void Window_SeriesMetadata_IsDeserialized()
    {
        var json = """
        {
          "metadata": {
            "runId": "run-1",
            "templateId": "order-system",
            "mode": "telemetry",
            "telemetrySourcesResolved": true,
            "schema": { "id": "time-travel/v1", "version": "1", "hash": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
            "storage": { "modelPath": "runs/run-1/model.yaml" }
          },
          "window": { "startBin": 0, "endBin": 1, "binCount": 2 },
          "timestampsUtc": [ "2025-01-01T00:00:00Z", "2025-01-01T00:05:00Z" ],
          "nodes": [
            {
              "id": "OrderService",
              "kind": "service",
              "series": { "arrivals": [1, 2] },
              "seriesMetadata": { "arrivals": { "aggregation": "avg", "origin": "derived" } },
              "telemetry": { "sources": [], "warnings": [] }
            }
          ],
          "warnings": []
        }
        """;

        var window = JsonSerializer.Deserialize<TimeTravelStateWindowDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(window);

        var property = typeof(TimeTravelNodeSeriesDto).GetProperty("SeriesMetadata");
        Assert.NotNull(property);

        var metadata = property!.GetValue(window!.Nodes[0]);
        var dict = Assert.IsAssignableFrom<IDictionary>(metadata);
        Assert.NotNull(dict["arrivals"]);

        var aggregation = dict["arrivals"]!.GetType().GetProperty("Aggregation")?.GetValue(dict["arrivals"]) as string;
        var origin = dict["arrivals"]!.GetType().GetProperty("Origin")?.GetValue(dict["arrivals"]) as string;
        Assert.Equal("avg", aggregation);
        Assert.Equal("derived", origin);
    }

    [Fact]
    public void Snapshot_SeriesMetadata_IsDeserialized()
    {
        var json = """
        {
          "metadata": {
            "runId": "run-1",
            "templateId": "order-system",
            "mode": "telemetry",
            "telemetrySourcesResolved": true,
            "schema": { "id": "time-travel/v1", "version": "1", "hash": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" },
            "storage": { "modelPath": "runs/run-1/model.yaml" }
          },
          "bin": { "index": 0, "durationMinutes": 5 },
          "nodes": [
            {
              "id": "OrderService",
              "kind": "service",
              "metrics": { "arrivals": 10, "served": 9, "errors": 1 },
              "seriesMetadata": { "arrivals": { "aggregation": "sum", "origin": "telemetry" } },
              "derived": { "utilization": 0.5 },
              "telemetry": { "sources": [], "warnings": [] }
            }
          ],
          "warnings": []
        }
        """;

        var snapshot = JsonSerializer.Deserialize<TimeTravelStateSnapshotDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(snapshot);

        var property = typeof(TimeTravelNodeSnapshotDto).GetProperty("SeriesMetadata");
        Assert.NotNull(property);

        var metadata = property!.GetValue(snapshot!.Nodes[0]);
        var dict = Assert.IsAssignableFrom<IDictionary>(metadata);
        Assert.NotNull(dict["arrivals"]);

        var aggregation = dict["arrivals"]!.GetType().GetProperty("Aggregation")?.GetValue(dict["arrivals"]) as string;
        var origin = dict["arrivals"]!.GetType().GetProperty("Origin")?.GetValue(dict["arrivals"]) as string;
        Assert.Equal("sum", aggregation);
        Assert.Equal("telemetry", origin);
    }
}
