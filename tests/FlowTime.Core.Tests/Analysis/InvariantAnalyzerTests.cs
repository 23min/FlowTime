using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Analysis;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Analysis;

public class InvariantAnalyzerTests
{
    [Fact]
    public void Analyze_WarnsWhenOutgoingEdgeFlowDoesNotMatchServed()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "source_arrivals",
                            Served = "source_served"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served"
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("source_arrivals")] = new[] { 10d, 10d },
            [new NodeId("source_served")] = new[] { 10d, 10d },
            [new NodeId("target_arrivals")] = new[] { 10d, 10d },
            [new NodeId("target_served")] = new[] { 10d, 10d }
        };

        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                Values = new[] { 8d, 10d }
            }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated, edgeSeries: edgeSeries);

        Assert.Contains(result.Warnings, warning => warning.Code == "edge_flow_mismatch_outgoing");
    }

    [Fact]
    public void Analyze_WarnsWhenIncomingEdgeFlowDoesNotMatchArrivals()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "source_arrivals",
                            Served = "source_served"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served"
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("source_arrivals")] = new[] { 10d, 10d },
            [new NodeId("source_served")] = new[] { 10d, 10d },
            [new NodeId("target_arrivals")] = new[] { 8d, 10d },
            [new NodeId("target_served")] = new[] { 10d, 10d }
        };

        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                Values = new[] { 10d, 10d }
            }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated, edgeSeries: edgeSeries);

        Assert.Contains(result.Warnings, warning => warning.Code == "edge_flow_mismatch_incoming");
    }

    [Fact]
    public void Analyze_WarnsWhenEdgeDefinesLag()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "source_arrivals",
                            Served = "source_served"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served",
                        Lag = 2
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("source_arrivals")] = new[] { 10d, 10d },
            [new NodeId("source_served")] = new[] { 10d, 10d },
            [new NodeId("target_arrivals")] = new[] { 10d, 10d },
            [new NodeId("target_served")] = new[] { 10d, 10d }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        Assert.Contains(result.Warnings, warning => warning.Code == "edge_behavior_violation_lag");
    }

    [Fact]
    public void Analyze_WarnsWhenEdgeClassFlowsDoNotMatchServedClasses()
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
                new NodeDefinition { Id = "served_total", Kind = "expr", Expr = "arrivals_total" },
                new NodeDefinition { Id = "target_arrivals", Kind = "const", Values = new[] { 2d } },
                new NodeDefinition { Id = "target_served", Kind = "const", Values = new[] { 2d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals_total",
                            Served = "served_total"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served"
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
            [new NodeId("target_arrivals")] = new[] { 2d },
            [new NodeId("target_served")] = new[] { 2d }
        };

        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                ClassId = "Alpha",
                Values = new[] { 2d }
            }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated, edgeSeries: edgeSeries);

        Assert.Contains(result.Warnings, warning => warning.Code == "edge_class_partial_coverage");
    }

    [Fact]
    public void Analyze_WarnsWhenEdgeClassCoverageIsPartial()
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
                new NodeDefinition { Id = "served_total", Kind = "expr", Expr = "arrivals_total" },
                new NodeDefinition { Id = "target_arrivals", Kind = "const", Values = new[] { 2d } },
                new NodeDefinition { Id = "target_served", Kind = "const", Values = new[] { 2d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals_total",
                            Served = "served_total"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served"
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
            [new NodeId("target_arrivals")] = new[] { 2d },
            [new NodeId("target_served")] = new[] { 2d }
        };

        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                ClassId = "Alpha",
                Values = new[] { 1d }
            }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated, edgeSeries: edgeSeries);

        Assert.Contains(result.Warnings, warning => warning.Code == "edge_class_partial_coverage");
    }

    [Fact]
    public void Analyze_DoesNotWarnWhenEdgeClassFlowsMatchServed()
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
                new NodeDefinition { Id = "served_total", Kind = "expr", Expr = "arrivals_total" },
                new NodeDefinition { Id = "target_arrivals", Kind = "const", Values = new[] { 2d } },
                new NodeDefinition { Id = "target_served", Kind = "const", Values = new[] { 2d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals_total",
                            Served = "served_total"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Target",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "target_arrivals",
                            Served = "target_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Target",
                        Measure = "served"
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
            [new NodeId("target_arrivals")] = new[] { 2d },
            [new NodeId("target_served")] = new[] { 2d }
        };

        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                ClassId = "Alpha",
                Values = new[] { 1d }
            },
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "Source->Target",
                Metric = "flowVolume",
                ClassId = "Beta",
                Values = new[] { 1d }
            }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated, edgeSeries: edgeSeries);

        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "edge_class_mismatch");
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "edge_class_partial_coverage");
    }

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

        var evaluatedSeries = new Dictionary<NodeId, double[]>
        {
            [new NodeId("queue_outflow")] = new[] { 1d },
            [new NodeId("queue_loss")] = new[] { 1d }
        };

        var warnings = InvariantAnalyzer.DetectServiceWithBufferClassCoverageGaps(nodeDefinitions, evaluatedSeries, contributions);

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

        var evaluatedSeries = new Dictionary<NodeId, double[]>
        {
            [new NodeId("queue_outflow")] = new[] { 1d },
            [new NodeId("queue_loss")] = new[] { 1d }
        };

        var warnings = InvariantAnalyzer.DetectTopologyServiceWithBufferClassCoverageGaps(topologyNodes, evaluatedSeries, contributions);

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
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "class_series_missing_errors");
    }

    [Fact]
    public void Analyze_WarnsWhenDependencySignalsMissing()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "dep_errors", Kind = "const", Values = new[] { 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "DependencyDb",
                        Kind = "dependency",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "dep_arrivals",
                            Served = "dep_served",
                            Errors = "dep_errors"
                        }
                    }
                }
            }
        };

        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("dep_errors")] = new[] { 0d }
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        Assert.Contains(result.Warnings, warning => warning.Code == "missing_dependency_arrivals");
        Assert.Contains(result.Warnings, warning => warning.Code == "missing_dependency_served");
    }
}
