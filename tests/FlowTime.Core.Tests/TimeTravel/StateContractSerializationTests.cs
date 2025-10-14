using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.Contracts.TimeTravel;

namespace FlowTime.Core.Tests.TimeTravel;

public class StateContractSerializationTests
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [Fact]
    public void StateSnapshotResponse_SerializesExpectedShape()
    {
        var response = new StateSnapshotResponse
        {
            Metadata = new StateMetadata
            {
                RunId = "order-run",
                TemplateId = "order-system",
                TemplateTitle = "Order System",
                TemplateVersion = "1.1.0",
                Mode = "simulation",
                ProvenanceHash = "sha256:abc12345",
                TelemetrySourcesResolved = true,
                Schema = new SchemaMetadata
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "sha256:abc12345"
                },
                Storage = new StorageDescriptor
                {
                    ModelPath = "data/models/order-system/schema-1/mode-simulation/sha256-abc123/model.yaml"
                }
            },
            Bin = new BinDetail
            {
                Index = 42,
                StartUtc = new DateTime(2025, 10, 7, 3, 30, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2025, 10, 7, 3, 35, 0, DateTimeKind.Utc),
                DurationMinutes = 5
            },
            Nodes = new[]
            {
                new NodeSnapshot
                {
                    Id = "OrderService",
                    Kind = "service",
                    Metrics = new NodeMetrics
                    {
                        Arrivals = 120,
                        Served = 118,
                        Capacity = 150,
                        Queue = 8
                    },
                    Derived = new NodeDerivedMetrics
                    {
                        Utilization = 0.79,
                        LatencyMinutes = 0.34,
                        ThroughputRatio = 0.98,
                        Color = "yellow"
                    },
                    Telemetry = new NodeTelemetryInfo
                    {
                        Sources = new[] { "file://examples/http-demo/order-service-served.csv" },
                        Warnings = Array.Empty<NodeTelemetryWarning>()
                    }
                }
            },
            Warnings = new[]
            {
                new StateWarning
                {
                    Code = "telemetry_missing_capacity",
                    Message = "Capacity series missing for node OrderService; derived metrics limited.",
                    Severity = "warning",
                    NodeId = "OrderService"
                }
            }
        };

        var json = JsonSerializer.Serialize(response, jsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("metadata", out var metadata));
        Assert.Equal("order-run", metadata.GetProperty("runId").GetString());
        Assert.Equal("time-travel/v1", metadata.GetProperty("schema").GetProperty("id").GetString());
        Assert.True(metadata.GetProperty("telemetrySourcesResolved").GetBoolean());

        Assert.True(root.TryGetProperty("bin", out var bin));
        Assert.Equal(42, bin.GetProperty("index").GetInt32());

        Assert.True(root.TryGetProperty("nodes", out var nodes));
        Assert.Equal(JsonValueKind.Array, nodes.ValueKind);
        Assert.Equal(1, nodes.GetArrayLength());

        var firstNode = nodes[0];
        Assert.Equal("OrderService", firstNode.GetProperty("id").GetString());
        Assert.True(firstNode.TryGetProperty("metrics", out var metrics));
        Assert.Equal(118, metrics.GetProperty("served").GetInt32());

        var telemetry = firstNode.GetProperty("telemetry");
        Assert.Equal(JsonValueKind.Array, telemetry.GetProperty("sources").ValueKind);
        Assert.Equal("file://examples/http-demo/order-service-served.csv", telemetry.GetProperty("sources")[0].GetString());

        Assert.True(root.TryGetProperty("warnings", out var warnings));
        Assert.Equal(1, warnings.GetArrayLength());
        Assert.Equal("telemetry_missing_capacity", warnings[0].GetProperty("code").GetString());
    }
}
