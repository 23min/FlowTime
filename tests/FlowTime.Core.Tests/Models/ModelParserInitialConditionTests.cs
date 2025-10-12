using System.Collections.Generic;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Models;

public class ModelParserInitialConditionTests
{
    [Fact]
    public void ParseMetadata_SelfReferencingShiftWithoutInitial_Throws()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition
            {
                Bins = 3,
                BinSize = 1,
                BinUnit = "minutes"
            },
            Nodes =
            [
                new NodeDefinition
                {
                    Id = "queue_depth",
                    Kind = "expr",
                    Expr = "MAX(0, SHIFT(queue_depth, 1) + arrivals - served)"
                },
                new NodeDefinition
                {
                    Id = "arrivals",
                    Kind = "const",
                    Values = new[] { 1.0, 1.0, 1.0 }
                },
                new NodeDefinition
                {
                    Id = "served",
                    Kind = "const",
                    Values = new[] { 1.0, 1.0, 1.0 }
                }
            ],
            Topology = new TopologyDefinition
            {
                Nodes =
                [
                    new TopologyNodeDefinition
                    {
                        Id = "queue_depth",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:arrivals.csv",
                            Served = "file:served.csv",
                            Errors = "file:errors.csv"
                        }
                    }
                ],
                Edges = []
            }
        };

        var ex = Assert.Throws<ModelParseException>(() => ModelParser.ParseMetadata(model));
        Assert.Equal(
            "Expression node 'queue_depth' uses SHIFT on itself and requires an initial condition (topology.nodes[].initialCondition.queueDepth).",
            ex.Message);
    }

    [Fact]
    public void ParseMetadata_SelfReferencingShiftWithInitial_Succeeds()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition
            {
                Bins = 3,
                BinSize = 1,
                BinUnit = "minutes"
            },
            Nodes =
            [
                new NodeDefinition
                {
                    Id = "queue_depth",
                    Kind = "expr",
                    Expr = "MAX(0, SHIFT(queue_depth, 1) + arrivals - served)"
                },
                new NodeDefinition
                {
                    Id = "arrivals",
                    Kind = "const",
                    Values = new[] { 1.0, 1.0, 1.0 }
                },
                new NodeDefinition
                {
                    Id = "served",
                    Kind = "const",
                    Values = new[] { 1.0, 1.0, 1.0 }
                }
            ],
            Topology = new TopologyDefinition
            {
                Nodes =
                [
                    new TopologyNodeDefinition
                    {
                        Id = "queue_depth",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:arrivals.csv",
                            Served = "file:served.csv",
                            Errors = "file:errors.csv"
                        },
                        InitialCondition = new InitialConditionDefinition
                        {
                            QueueDepth = 0.0
                        }
                    }
                ],
                Edges = []
            }
        };

        var metadata = ModelParser.ParseMetadata(model);
        Assert.NotNull(metadata.Topology);
        Assert.NotNull(metadata.Window);
    }
}
