using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Contracts.TimeTravel;
using Xunit;

namespace FlowTime.Api.Tests;

public class StateContractSerializationTests
{
    [Fact]
    public void StateSnapshotResponse_SerializesEdgesWhenPresent()
    {
        var payload = new StateSnapshotResponse
        {
            Metadata = new StateMetadata
            {
                RunId = "run-test",
                TemplateId = "template-test",
                TemplateTitle = "Template Test",
                TemplateVersion = "1.0.0",
                Mode = "simulation",
                Schema = new SchemaMetadata
                {
                    Id = "time-travel-state",
                    Version = "1",
                    Hash = "sha256:0000000000000000000000000000000000000000000000000000000000000000"
                },
                Storage = new StorageDescriptor
                {
                    ModelPath = "model.yaml",
                    MetadataPath = "metadata.json",
                    ProvenancePath = "provenance.json"
                }
            },
            Bin = new BinDetail
            {
                Index = 1,
                DurationMinutes = 5
            },
            Nodes = Array.Empty<NodeSnapshot>(),
            Edges = new List<EdgeSeries>
            {
                new()
                {
                    Id = "edge_order_support",
                    From = "OrderService",
                    To = "SupportQueue",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["flowTotal"] = new double?[] { 1, 2, 3 }
                    }
                }
            },
            Warnings = Array.Empty<StateWarning>()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var node = JsonNode.Parse(json);

        Assert.NotNull(node);
        var edges = node!["edges"] as JsonArray;
        Assert.NotNull(edges);
        Assert.NotEmpty(edges);
    }
}
