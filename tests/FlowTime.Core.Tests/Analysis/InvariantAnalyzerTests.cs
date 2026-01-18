using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Analysis;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Analysis;

public class InvariantAnalyzerTests
{
    [Fact]
    public void Analyze_DoesNotWarn_WhenParallelismScalesCapacity()
    {
        var model = new ModelDefinition
        {
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "BufferService",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors",
                            Capacity = "capacity",
                            Parallelism = 2
                        }
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 9d, 9d, 9d },
            [new NodeId("served")] = new[] { 9d, 9d, 9d },
            [new NodeId("errors")] = new[] { 0d, 0d, 0d },
            [new NodeId("capacity")] = new[] { 5d, 5d, 5d }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "served_exceeds_capacity");
    }

    [Fact]
    public void DetectServiceWithBufferClassCoverageGaps_WarnsWhenOutflowOrLossMissingClasses()
    {
        var nodeDefinitions = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Queue"] = new NodeDefinition
            {
                Id = "Queue",
                Kind = "serviceWithBuffer",
                Inflow = "queue_inflow",
                Outflow = "queue_outflow",
                Loss = "queue_loss"
            }
        };

        var contributions = new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        contributions[new NodeId("queue_inflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d },
            ["Airport"] = new[] { 2d }
        };
        contributions[new NodeId("queue_outflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d }
        };

        var warnings = InvariantAnalyzer.DetectServiceWithBufferClassCoverageGaps(nodeDefinitions, contributions);

        Assert.Contains(warnings, warning => warning.Code == "class_series_partial_outflow");
        Assert.Contains(warnings, warning => warning.Code == "class_series_missing_loss");
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void DetectTopologyServiceWithBufferClassCoverageGaps_WarnsWhenServedOrErrorsMissingClasses()
    {
        var topologyNodes = new List<TopologyNodeDefinition>
        {
            new()
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
        };

        var contributions = new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        contributions[new NodeId("queue_inflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d },
            ["Airport"] = new[] { 2d }
        };
        contributions[new NodeId("queue_outflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d }
        };

        var warnings = InvariantAnalyzer.DetectTopologyServiceWithBufferClassCoverageGaps(topologyNodes, contributions);

        Assert.Contains(warnings, warning => warning.Code == "class_series_partial_served");
        Assert.Contains(warnings, warning => warning.Code == "class_series_missing_errors");
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Analyze_WarnsWhenEvaluatedSeriesContainsUnknownNodes()
    {
        var model = new ModelDefinition
        {
            Nodes =
            {
                new NodeDefinition { Id = "known", Kind = "const", Values = new[] { 1d } }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("known")] = new[] { 1d },
            [new NodeId("injected_series")] = new[] { 2d }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        Assert.Contains(result.Warnings, warning => warning.Code == "post_eval_injection");
    }

    [Fact]
    public void Analyze_WarnsOnTopologyClassCoverageGaps()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Classes =
            {
                new ClassDefinition { Id = "Alpha" },
                new ClassDefinition { Id = "Beta" }
            },
            Traffic = new TrafficDefinition
            {
                Arrivals =
                {
                    new ArrivalDefinition
                    {
                        NodeId = "arrivals_alpha",
                        ClassId = "Alpha",
                        Pattern = new ArrivalPatternDefinition { Kind = "constant", RatePerBin = 1 }
                    },
                    new ArrivalDefinition
                    {
                        NodeId = "arrivals_beta",
                        ClassId = "Beta",
                        Pattern = new ArrivalPatternDefinition { Kind = "constant", RatePerBin = 1 }
                    }
                }
            },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals_alpha", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "arrivals_beta", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "arrivals_total", Kind = "expr", Expr = "arrivals_alpha + arrivals_beta" },
                new NodeDefinition { Id = "served_total", Kind = "const", Values = new[] { 2d } },
                new NodeDefinition { Id = "errors_total", Kind = "const", Values = new[] { 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceNode",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals_total",
                            Served = "served_total",
                            Errors = "errors_total"
                        }
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals_alpha")] = new[] { 1d },
            [new NodeId("arrivals_beta")] = new[] { 1d },
            [new NodeId("arrivals_total")] = new[] { 2d },
            [new NodeId("served_total")] = new[] { 2d },
            [new NodeId("errors_total")] = new[] { 0d }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        Assert.Contains(result.Warnings, warning => warning.Code == "class_series_missing_served");
        Assert.Contains(result.Warnings, warning => warning.Code == "class_series_missing_errors");
    }
}
