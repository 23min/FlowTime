using System.Collections.Generic;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Aggregation;

public class TopologyClassContributionTests
{
    [Fact]
    public void ClassContributionBuilder_PropagatesServiceWithBufferTopologyClasses()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "queue_inflow", Kind = "const", Values = new[] { 6d } },
                new NodeDefinition { Id = "queue_outflow", Kind = "const", Values = new[] { 5d } },
                new NodeDefinition { Id = "queue_loss", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "line_arrivals", Kind = "expr", Expr = "queue_outflow" },
                new NodeDefinition { Id = "line_served", Kind = "expr", Expr = "line_arrivals" }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Queue",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "queue_inflow",
                            Served = "queue_outflow",
                            Errors = "queue_loss"
                        }
                    }
                }
            }
        };

        var grid = new TimeGrid(1, 1, TimeUnit.Hours);
        var totals = new Dictionary<NodeId, double[]>
        {
            [new NodeId("queue_inflow")] = new[] { 6d },
            [new NodeId("queue_outflow")] = new[] { 5d },
            [new NodeId("queue_loss")] = new[] { 1d },
            [new NodeId("line_arrivals")] = new[] { 5d },
            [new NodeId("line_served")] = new[] { 5d }
        };
        var classAssignments = new Dictionary<NodeId, string>
        {
            [new NodeId("queue_inflow")] = "Airport"
        };

        var contributions = ClassContributionBuilder.Build(model, grid, totals, classAssignments, out _);

        var outflow = Assert.Contains(new NodeId("queue_outflow"), contributions);
        Assert.Equal(new[] { 5d }, outflow["Airport"]);

        var loss = Assert.Contains(new NodeId("queue_loss"), contributions);
        Assert.Equal(new[] { 1d }, loss["Airport"]);

        var lineArrivals = Assert.Contains(new NodeId("line_arrivals"), contributions);
        Assert.Equal(new[] { 5d }, lineArrivals["Airport"]);

        var lineServed = Assert.Contains(new NodeId("line_served"), contributions);
        Assert.Equal(new[] { 5d }, lineServed["Airport"]);
    }
}
