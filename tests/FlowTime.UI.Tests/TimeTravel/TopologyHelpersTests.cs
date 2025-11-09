using System;
using System.Linq;
using FlowTime.UI.Components.Topology;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyHelpersTests
{
    [Fact]
    public void GraphMapperAssignsLayersAndConnections()
    {
        var response = new GraphResponseModel(
            new[]
            {
                new GraphNodeModel("ingress", "service", CreateSemantics(), null),
                new GraphNodeModel("processor", "service", CreateSemantics(), null),
                new GraphNodeModel("egress", "queue", CreateSemantics(), null)
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
                new GraphNodeModel("alpha", "service", CreateSemantics(), null),
                new GraphNodeModel("beta", "queue", CreateSemantics(), new GraphNodeUiModel(160, 48))
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
                new GraphNodeModel("svc", "service", CreateSemantics(), null),
                new GraphNodeModel("analytics", "service", CreateSemantics(), null)
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
                new GraphNodeModel("ingress", "service", CreateSemantics(), null),
                new GraphNodeModel("processor", "service", CreateSemantics(), null),
                new GraphNodeModel("egress", "service", CreateSemantics(), null)
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

        Assert.Equal(ingress.X, processor.X, 3);
        Assert.Equal(processor.X, egress.X, 3);
        Assert.True(processor.Y > ingress.Y);
        Assert.True(egress.Y > processor.Y);
    }

    [Fact]
    public void HappyPathLayoutPositionsSupportingNodesBesideBackbone()
    {
        var response = new GraphResponseModel(
            new[]
            {
                new GraphNodeModel("constant", "const", CreateSemantics(), null),
                new GraphNodeModel("expression", "expression", CreateSemantics(), null),
                new GraphNodeModel("service", "service", CreateSemantics(), null)
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
        Assert.True(expr.X < svc.X);
        Assert.True(constant.X <= expr.X + 0.001);
        Assert.True(expr.Y < svc.Y);
        Assert.True(constant.Y < expr.Y);
    }

    [Fact]
    public void HappyPathLayoutDropsLeafNodesBelowBackbone()
    {
        var response = new GraphResponseModel(
            new[]
            {
                new GraphNodeModel("ingress", "service", CreateSemantics(), null),
                new GraphNodeModel("processor", "service", CreateSemantics(), null),
                new GraphNodeModel("orphan_metric", "expression", CreateSemantics(), null)
            },
            new[]
            {
                new GraphEdgeModel("edge_ingress_processor", "ingress:out", "processor:in", 1, null, null, null, null)
            });

        var graph = GraphMapper.Map(response, respectUiPositions: false, layout: LayoutMode.HappyPath);

        var processor = graph.Nodes.Single(n => n.Id == "processor");
        var orphan = graph.Nodes.Single(n => n.Id == "orphan_metric");

        Assert.True(orphan.Y > processor.Y);
        Assert.True(orphan.Y - processor.Y >= 40);
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
        Assert.Equal("#7A7A7A", fill);
    }

    [Fact]
    public void TooltipFormatterProducesExpectedLines()
    {
        var timestamp = new DateTimeOffset(2025, 1, 1, 0, 15, 0, TimeSpan.Zero);
        var metrics = new NodeBinMetrics(0.9634, 0.82, 0.04, 12, 5.4, timestamp);

        var content = TooltipFormatter.Format("OrderService", metrics);

        Assert.Equal("OrderService", content.Title);
        Assert.Contains("SLA 96.3%", content.Lines);
        Assert.Contains("Utilization 82%", content.Lines);
        Assert.Contains("Errors 4.0%", content.Lines);
        Assert.Contains("Queue 12", content.Lines);
        Assert.Equal("01 Jan 2025 00:15 UTC", content.Subtitle);
    }

    private static GraphNodeSemanticsModel CreateSemantics()
        => new(
            Arrivals: "series:arrivals",
            Served: "series:served",
            Errors: "series:errors",
            Attempts: null,
            Failures: null,
            RetryEcho: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null);
}
