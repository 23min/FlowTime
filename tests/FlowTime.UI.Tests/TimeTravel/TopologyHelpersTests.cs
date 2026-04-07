using System;
using System.Linq;
using FlowTime.UI.Components.Topology;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyHelpersTests
{
    private const double verticalSpacing = 140d;
    private const int leafLane = 2;

    [Fact]
    public void GraphMapperAssignsLayersAndConnections()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("ingress", "service"),
                CreateGraphNode("processor", "service"),
                CreateGraphNode("egress", "queue")
            },
            new[]
            {
                new GraphEdgeModel("edge_ingress_processor", "ingress:out", "processor:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_processor_egress", "processor:out", "egress:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response);

        Assert.Equal(3, graph.Nodes.Count);
        var ingress = graph.Nodes.Single(n => n.Id == "ingress");
        var processor = graph.Nodes.Single(n => n.Id == "processor");
        var egress = graph.Nodes.Single(n => n.Id == "egress");

        Assert.Equal(0, ingress.Layer);
        Assert.Contains("processor", ingress.Outputs);
        Assert.Empty(ingress.Inputs);

        Assert.Equal(1, processor.Layer);
        Assert.Contains("ingress", processor.Inputs);
        Assert.Contains("egress", processor.Outputs);

        Assert.Equal(2, egress.Layer);
        Assert.Contains("processor", egress.Inputs);
        Assert.Empty(egress.Outputs);
    }

    [Fact]
    public void GraphMapperRespectsUiHintsWhenProvided()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("alpha", "service"),
                CreateGraphNode("beta", "queue", ui: new GraphNodeUiModel(160, 48))
            },
            new[]
            {
                new GraphEdgeModel("edge_alpha_beta", "alpha:out", "beta:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response);

        var alpha = graph.Nodes.Single(n => n.Id == "alpha");
        var beta = graph.Nodes.Single(n => n.Id == "beta");

        Assert.False(alpha.IsPositionFixed);
        Assert.True(beta.IsPositionFixed);
        Assert.Equal(160, beta.X);
        Assert.Equal(48, beta.Y);
    }

    [Fact]
    public void GraphMapperPreservesEdgeMetadata()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("svc", "service"),
                CreateGraphNode("analytics", "service")
            },
            new[]
            {
                new GraphEdgeModel("edge_svc_analytics", "svc:out", "analytics:in", 1, "effort", "load", 0.5, 1)
            });

        var graph = GraphMapper.Map(response);
        var edge = Assert.Single(graph.Edges);

        Assert.Equal("edge_svc_analytics", edge.Id);
        Assert.Equal("svc", edge.From);
        Assert.Equal("analytics", edge.To);
        Assert.Equal("effort", edge.EdgeType);
        Assert.Equal("load", edge.Field);
        Assert.Equal(0.5, edge.Multiplier);
        Assert.Equal(1, edge.Lag);
    }

    [Fact]
    public void HappyPathLayoutAlignsOperationalBackbone()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("ingress", "service"),
                CreateGraphNode("processor", "service"),
                CreateGraphNode("egress", "service")
            },
            new[]
            {
                new GraphEdgeModel("edge_ingress_processor", "ingress:out", "processor:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_processor_egress", "processor:out", "egress:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);

        var ingress = graph.Nodes.Single(n => n.Id == "ingress");
        var processor = graph.Nodes.Single(n => n.Id == "processor");
        var egress = graph.Nodes.Single(n => n.Id == "egress");

        Assert.Equal(0, ingress.Lane);
        Assert.Equal(0, processor.Lane);
        Assert.Equal(0, egress.Lane);
        Assert.Equal(ResolveRow(ingress) + 1, ResolveRow(processor));
        Assert.Equal(ResolveRow(processor) + 1, ResolveRow(egress));
    }

    [Fact]
    public void HappyPathLayoutPositionsSupportingNodesBesideBackbone()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("constant", "const"),
                CreateGraphNode("expression", "expression"),
                CreateGraphNode("service", "service")
            },
            new[]
            {
                new GraphEdgeModel("edge_constant_expression", "constant:out", "expression:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_expression_service", "expression:out", "service:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);

        var svc = graph.Nodes.Single(n => n.Id == "service");
        var expr = graph.Nodes.Single(n => n.Id == "expression");
        var constant = graph.Nodes.Single(n => n.Id == "constant");
        Assert.True(expr.Lane < svc.Lane);
        Assert.True(constant.Lane <= expr.Lane);
        Assert.Equal(ResolveRow(svc) - 1, ResolveRow(expr));
        Assert.Equal(ResolveRow(expr) - 1, ResolveRow(constant));
    }

    [Fact]
    public void HappyPathLayoutIgnoresIsolatedSupportNodes()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("ingress", "service"),
                CreateGraphNode("processor", "service"),
                CreateGraphNode("orphan_metric", "expression")
            },
            new[]
            {
                new GraphEdgeModel("edge_ingress_processor", "ingress:out", "processor:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);

        var processor = graph.Nodes.Single(n => n.Id == "processor");
        Assert.NotNull(processor);
        Assert.DoesNotContain(graph.Nodes, n => n.Id == "orphan_metric");
    }

    [Fact]
    public void HappyPathLayoutProducesUniqueLaneRowPairs()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("svc_a", "service"),
                CreateGraphNode("svc_b", "service"),
                CreateGraphNode("svc_c", "service"),
                CreateGraphNode("expr_in", "expression"),
                CreateGraphNode("expr_mid", "expression"),
                CreateGraphNode("expr_top", "expression"),
                CreateGraphNode("leaf_one", "expression"),
                CreateGraphNode("leaf_two", "expression")
            },
            new[]
            {
                new GraphEdgeModel("edge_ab", "svc_a:out", "svc_b:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_bc", "svc_b:out", "svc_c:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_mid_b", "expr_mid:out", "svc_b:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_top_mid", "expr_top:out", "expr_mid:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_in_top", "expr_in:out", "expr_top:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_c_leaf1", "svc_c:out", "leaf_one:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_b_leaf2", "svc_b:out", "leaf_two:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);
        var occupied = new HashSet<(int Lane, int Row)>();

        foreach (var node in graph.Nodes)
        {
            var row = ResolveRow(node);
            Assert.True(occupied.Add((node.Lane, row)), $"Duplicate cell detected for {node.Id}");
        }
    }

    [Fact]
    public void HappyPathLayoutStacksSupportingChainsAboveChildren()
    {
        var response = new GraphResponseModel(
            new[]
            {
                CreateGraphNode("svc", "service"),
                CreateGraphNode("expr_upper", "expression"),
                CreateGraphNode("expr_lower", "expression"),
                CreateGraphNode("const_seed", "const")
            },
            new[]
            {
                new GraphEdgeModel("edge_upper_svc", "expr_upper:out", "svc:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_lower_upper", "expr_lower:out", "expr_upper:in", 1, null, null, null, null),
                new GraphEdgeModel("edge_seed_lower", "const_seed:out", "expr_lower:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);
        var svc = graph.Nodes.Single(n => n.Id == "svc");
        var upper = graph.Nodes.Single(n => n.Id == "expr_upper");
        var lower = graph.Nodes.Single(n => n.Id == "expr_lower");
        var seed = graph.Nodes.Single(n => n.Id == "const_seed");

        Assert.True(upper.Lane < svc.Lane);
        Assert.True(lower.Lane <= upper.Lane);
        Assert.Equal(ResolveRow(svc) - 1, ResolveRow(upper));
        Assert.Equal(ResolveRow(upper) - 1, ResolveRow(lower));
        Assert.Equal(ResolveRow(lower) - 1, ResolveRow(seed));
    }

    [Fact]
    public void ColorScaleReturnsSuccessForHealthyNodes()
    {
        var metrics = new NodeBinMetrics(0.96, 0.72, 0.01, null, null, DateTimeOffset.UtcNow);
        var fill = ColorScale.GetFill(metrics);
        Assert.Equal("#009E73", fill);
    }

    [Theory]
    [InlineData(0.90, "#E69F00")]
    [InlineData(0.82, "#E69F00")]
    public void ColorScaleReturnsWarningWhenSlaDrops(double successRate, string expected)
    {
        var metrics = new NodeBinMetrics(successRate, 0.75, 0.02, null, null, DateTimeOffset.UtcNow);
        var fill = ColorScale.GetFill(metrics);
        Assert.Equal(expected, fill);
    }

    [Fact]
    public void ColorScaleReturnsErrorWhenSlaIsSeverelyBreached()
    {
        var metrics = new NodeBinMetrics(0.70, 0.65, 0.1, null, null, DateTimeOffset.UtcNow);
        var fill = ColorScale.GetFill(metrics);
        Assert.Equal("#D55E00", fill);
    }

    [Fact]
    public void ColorScaleTreatsHighUtilizationAsWarning()
    {
        var metrics = new NodeBinMetrics(0.98, 0.96, 0.0, null, null, DateTimeOffset.UtcNow);
        var fill = ColorScale.GetFill(metrics);
        Assert.Equal("#E69F00", fill);
    }

    [Fact]
    public void ColorScaleFallsBackToNeutralWhenMissingData()
    {
        var metrics = new NodeBinMetrics(null, null, null, null, null, null);
        var fill = ColorScale.GetFill(metrics);
        Assert.Equal(ColorScale.NeutralColor, fill);
    }

    [Fact]
    public void TooltipFormatterProducesExpectedLines()
    {
        var timestamp = new DateTimeOffset(2025, 1, 1, 0, 15, 0, TimeSpan.Zero);
        var metrics = WithNodeFacts(new NodeBinMetrics(0.9634, 0.82, 0.04, 12, 5.4, timestamp), "serviceWithBuffer");

        var content = TooltipFormatter.Format("OrderService", metrics);

        Assert.Equal("OrderService", content.Title);
        Assert.Contains("SLA 96.3%", content.Lines);
        Assert.Contains("Utilization 82%", content.Lines);
        Assert.Contains("Errors 4.0%", content.Lines);
        Assert.Contains("Queue 12", content.Lines);
        Assert.Equal("01 Jan 2025 00:15 UTC", content.Subtitle);
    }

    [Fact]
    public void TooltipFormatter_SinkWithoutSchedule_UsesSlaLabel()
    {
        var timestamp = new DateTimeOffset(2025, 1, 1, 0, 15, 0, TimeSpan.Zero);
        var metrics = WithNodeFacts(
            new NodeBinMetrics(
                1.0,
                null,
                0.0,
                null,
                null,
                timestamp,
                NodeKind: "sink"),
            "sink");

        var content = TooltipFormatter.Format("Downtown", metrics);

        Assert.Contains("SLA 100.0%", content.Lines);
        Assert.DoesNotContain("Schedule SLA 100.0%", content.Lines);
    }

    [Fact]
    public void TooltipFormatter_IncludesCompletionSla_WhenProvided()
    {
        var timestamp = new DateTimeOffset(2025, 1, 1, 0, 15, 0, TimeSpan.Zero);
        var metrics = WithNodeFacts(
            new NodeBinMetrics(
                0.92,
                0.78,
                0.02,
                8,
                4.1,
                timestamp,
                RawMetrics: new Dictionary<string, double?> { ["completionSla"] = 0.92 }),
            "service");

        var content = TooltipFormatter.Format("OrderService", metrics);

        Assert.Contains("Completion SLA 92.0%", content.Lines);
    }

private static GraphNodeSemanticsModel CreateSemantics()
    => new(
        Arrivals: "series:arrivals",
        Served: "series:served",
        Errors: "series:errors",
        Attempts: null,
        Failures: null,
        ExhaustedFailures: null,
        RetryEcho: null,
        RetryBudgetRemaining: null,
        Queue: null,
        Capacity: null,
        Parallelism: null,
        Series: null,
        Expression: null,
        Distribution: null,
        InlineValues: null,
        Aliases: null,
        Metadata: null,
        MaxAttempts: null,
        BackoffStrategy: null,
        ExhaustedPolicy: null);

    private static GraphNodeModel CreateGraphNode(string id, string kind, GraphNodeUiModel? ui = null)
    {
        return new GraphNodeModel(id, kind, CreateSemantics(), Ui: ui)
        {
            Category = ResolveCategory(kind),
            Analytical = CreateAnalytical(kind)
        };
    }

    private static NodeBinMetrics WithNodeFacts(NodeBinMetrics metrics, string kind)
    {
        var analytical = CreateAnalytical(kind);
        return metrics with
        {
            NodeKind = kind,
            NodeCategory = ResolveCategory(kind),
            AnalyticalIdentity = analytical.Identity,
            HasQueueSemantics = analytical.HasQueueSemantics,
            HasServiceSemantics = analytical.HasServiceSemantics
        };
    }

    private static string ResolveCategory(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "queue" => "queue",
            "dlq" => "dlq",
            "router" => "router",
            "dependency" => "dependency",
            "sink" => "sink",
            "const" or "constant" or "pmf" => "constant",
            "expr" or "expression" => "expression",
            _ => "service"
        };
    }

    private static GraphNodeAnalyticalModel CreateAnalytical(string kind)
    {
        var normalized = kind.ToLowerInvariant();
        var category = ResolveCategory(kind);
        var hasQueueSemantics = normalized is "queue" or "dlq" or "servicewithbuffer";
        var hasServiceSemantics = category == "service";

        return new GraphNodeAnalyticalModel
        {
            Identity = normalized switch
            {
                "const" => "constant",
                "expr" => "expression",
                _ => kind
            },
            HasQueueSemantics = hasQueueSemantics,
            HasServiceSemantics = hasServiceSemantics,
            HasCycleTimeDecomposition = hasQueueSemantics && hasServiceSemantics,
            StationarityWarningApplicable = hasQueueSemantics
        };
    }

    private static int ResolveRow(TopologyNode node)
    {
        return (int)Math.Round(node.Y / verticalSpacing, MidpointRounding.AwayFromZero);
    }
}
