using System;
using FlowTime.Core;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Parsing;

public class ModelParserTopologyTests
{
    [Fact]
    public void ParseMetadata_WithTopology_ReturnsWindowAndTopology()
    {
        var model = new ModelDefinition
        {
            SchemaVersion = 1,
            Grid = new GridDefinition
            {
                Bins = 4,
                BinSize = 5,
                BinUnit = "minutes",
                StartTimeUtc = "2025-10-07T00:00:00Z"
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "OrderService",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:OrderService_arrivals.csv",
                            Served = "file:OrderService_served.csv",
                            Errors = "file:OrderService_errors.csv"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition { Source = "OrderService", Target = "PaymentService", Weight = 0.8 }
                }
            }
        };

        var metadata = ModelParser.ParseMetadata(model);

        Assert.Equal(4, metadata.Window.Bins);
        Assert.Equal(TimeUnit.Minutes, metadata.Window.BinUnit);
        Assert.Equal(new DateTime(2025, 10, 7, 0, 0, 0, DateTimeKind.Utc), metadata.Window.StartTime);
        Assert.NotNull(metadata.Topology);
        Assert.Single(metadata.Topology!.Nodes);
        Assert.Single(metadata.Topology.Edges);
        Assert.Equal("OrderService", metadata.Topology.Nodes[0].Id);
    }

    [Fact]
    public void ParseMetadata_WithoutTopology_HandlesNull()
    {
        var model = new ModelDefinition
        {
            SchemaVersion = 1,
            Grid = new GridDefinition
            {
                Bins = 2,
                BinSize = 15,
                BinUnit = "minutes",
                StartTimeUtc = null
            }
        };

        var metadata = ModelParser.ParseMetadata(model);

        Assert.Equal(2, metadata.Window.Bins);
        Assert.Null(metadata.Window.StartTime);
        Assert.Null(metadata.Topology);
    }
}
