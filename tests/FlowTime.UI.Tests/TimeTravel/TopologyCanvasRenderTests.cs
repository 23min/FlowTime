using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using FlowTime.UI.Components.Topology;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyCanvasRenderTests : TestContext
{
    private const double NodeWidth = 54;
    private const double NodeHeight = 24;
    private const double NodeCornerRadius = 3;
    private const double ViewportPadding = 48;

    public TopologyCanvasRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.registerHandlers", _ => true).SetVoidResult();
    }

    [Fact]
    public void RenderRequestsCanvasDraw()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        cut.WaitForAssertion(() => Assert.Single(renderCall.Invocations));

        var invocation = renderCall.Invocations.Single();
        Assert.Equal(2, invocation.Arguments.Count);
        Assert.True(invocation.Arguments[0] is ElementReference);
        Assert.IsType<CanvasRenderRequest>(invocation.Arguments[1]);

        var payload = (CanvasRenderRequest)invocation.Arguments[1]!;
        Assert.Equal(graph.Nodes.Count, payload.Nodes.Count);
    }

    [Fact]
    public void RenderIncludesEffortEdgeMetadata()
    {
        var nodes = new[]
        {
            new TopologyNode("source", "service", Array.Empty<string>(), new[] { "downstream" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("downstream", "service", new[] { "source" }, Array.Empty<string>(), 1, 0, 200, 120, false, EmptySemantics()),
            new TopologyNode("analytics", "service", new[] { "source" }, Array.Empty<string>(), 1, 1, 220, 160, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("edge_source_downstream", "source", "downstream", 1, "throughput", "served", null, null),
            new TopologyEdge("edge_source_analytics", "source", "analytics", 1, "effort", "load", 0.4, 2)
        };

        var graph = new TopologyGraph(nodes, edges);
        var metrics = CreateMetrics();

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var invocation = renderCall.Invocations.Single();
        var payload = Assert.IsType<CanvasRenderRequest>(invocation.Arguments[1]);

        var effortEdge = Assert.Single(payload.Edges.Where(e => e.Id == "edge_source_analytics"));
        Assert.Equal("effort", effortEdge.EdgeType);
        Assert.Equal("load", effortEdge.Field);
        Assert.Equal(0.4, effortEdge.Multiplier);
        Assert.Equal(2, effortEdge.Lag);

        var throughputEdge = Assert.Single(payload.Edges.Where(e => e.Id == "edge_source_downstream"));
        Assert.Equal("throughput", throughputEdge.EdgeType);
        Assert.Equal("served", throughputEdge.Field);
        Assert.Null(throughputEdge.Multiplier);
        Assert.Null(throughputEdge.Lag);
    }

    [Fact]
    public void OverlayPayloadReflectsRetryToggles()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var overlay = new TopologyOverlaySettings
        {
            ShowRetryMetrics = false,
            ShowEdgeMultipliers = false
        };

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlay));

        var invocation = renderCall.Invocations.Single();
        var payload = Assert.IsType<CanvasRenderRequest>(invocation.Arguments[1]);

        Assert.False(payload.Overlays.ShowRetryMetrics);
        Assert.False(payload.Overlays.ShowEdgeMultipliers);
    }

    [Fact]
    public void UpdatesMetricsTriggerAdditionalRender()
    {
        var graph = CreateGraph();
        var initialMetrics = CreateMetrics();
        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, initialMetrics));

        cut.WaitForAssertion(() => Assert.Single(renderCall.Invocations));

        var updatedMetrics = new Dictionary<string, NodeBinMetrics>(initialMetrics, StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = new NodeBinMetrics(0.65, 0.95, 0.12, 18, 7.1, DateTimeOffset.UtcNow)
        };

        cut.SetParametersAndRender(p => p.Add(x => x.NodeMetrics, updatedMetrics));

        cut.WaitForAssertion(() => Assert.Equal(2, renderCall.Invocations.Count));
    }

    [Fact]
    public void RendersPlaceholderWhenGraphMissing()
    {
        var cut = RenderComponent<TopologyCanvas>();

        Assert.Contains("Select a run", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FocusDisplaysTooltipWithMetrics()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true).SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var target = cut.Find("[data-node-id='processor']");
        target.Focus();

        cut.WaitForAssertion(() => Assert.Equal("true", target.GetAttribute("data-focused")));
    }

    [Fact]
    public void EscapeKeyHidesTooltip()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true).SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var target = cut.Find("[data-node-id='processor']");
        target.Focus();
        cut.WaitForAssertion(() => Assert.Equal("true", target.GetAttribute("data-focused")));

        target.KeyDown(new KeyboardEventArgs { Key = "Escape", Code = "Escape" });

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", target.GetAttribute("data-focused"));
        });
    }

    [Fact]
    public void ArrowNavigationMovesFocusToNeighbor()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var ingress = cut.Find("[data-node-id='ingress']");
        ingress.Focus();
        cut.WaitForAssertion(() => Assert.Equal("true", ingress.GetAttribute("data-focused")));

        ingress.KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });

        cut.WaitForAssertion(() =>
        {
            var processor = cut.Find("[data-node-id='processor']");
            Assert.Equal("true", processor.GetAttribute("data-focused"));
        });
    }

    [Fact]
    public void RenderRequestUsesRectangularNodesAndViewport()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var invocation = renderCall.Invocations.Single();
        var payload = Assert.IsType<CanvasRenderRequest>(invocation.Arguments[1]);

        Assert.All(payload.Nodes, node =>
        {
            Assert.Equal(NodeWidth, node.Width);
            Assert.Equal(NodeHeight, node.Height);
            Assert.Equal(NodeCornerRadius, node.CornerRadius);
        });

        var expectedMinX = graph.Nodes.Min(n => n.X) - (NodeWidth / 2);
        var expectedMaxX = graph.Nodes.Max(n => n.X) + (NodeWidth / 2);
        var expectedMinY = graph.Nodes.Min(n => n.Y) - (NodeHeight / 2);
        var expectedMaxY = graph.Nodes.Max(n => n.Y) + (NodeHeight / 2);

        Assert.Equal(expectedMinX, payload.Viewport.MinX, 3);
        Assert.Equal(expectedMaxX, payload.Viewport.MaxX, 3);
        Assert.Equal(expectedMinY, payload.Viewport.MinY, 3);
        Assert.Equal(expectedMaxY, payload.Viewport.MaxY, 3);
        Assert.Equal(ViewportPadding, payload.Viewport.Padding);
    }

    [Fact]
    public void OverlayPayloadIncludesDerivedThresholds()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.SlaWarningThreshold = 0.92;
        overlays.UtilizationWarningThreshold = 0.86;
        overlays.ErrorRateAlertThreshold = 0.08;

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        const int activeBin = 3;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasRenderRequest>(renderCall.Invocations.Single().Arguments[1]);
        var overlayPayload = payload.Overlays;

        Assert.Equal(0.92, overlayPayload.SlaSuccessThreshold, 3);
        Assert.Equal(0.77, overlayPayload.SlaWarningCutoff, 3);
        Assert.Equal(0.86, overlayPayload.UtilizationWarningCutoff, 3);
        Assert.Equal(0.91, overlayPayload.UtilizationCriticalCutoff, 3);
        Assert.Equal(0.032, overlayPayload.ErrorWarningCutoff, 3);
        Assert.Equal(0.08, overlayPayload.ErrorCriticalCutoff, 3);
        Assert.Equal(activeBin, overlayPayload.SelectedBin);
    }

    [Fact]
    public void FocusLabelUsesOverlaySeriesFromSlices()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.ColorBasis = TopologyColorBasis.Utilization;

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = CreateSparklineWithSlices(
                new double?[] { 0.90, 0.91, 0.92 },
                new double?[] { 0.75, 0.81, 0.82 },
                startIndex: 0)
        };

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        const int activeBin = 2;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasRenderRequest>(renderCall.Invocations.Single().Arguments[1]);
        var processor = Assert.Single(payload.Nodes, node => node.Id == "processor");

        Assert.Equal("82%", processor.FocusLabel);
    }

    [Fact]
    public void FullDagDisabledFiltersNonOperationalNodes()
    {
        var graph = CreateGraphWithFullDagNodes();
        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.IncludeServiceNodes = true;
        overlays.IncludeExpressionNodes = true;
        overlays.IncludeConstNodes = true;
        overlays.EnableFullDag = false;

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.OverlaySettings, overlays));

        var payload = Assert.IsType<CanvasRenderRequest>(renderCall.Invocations.Single().Arguments[1]);

        Assert.Single(payload.Nodes);
        Assert.Equal("svc", payload.Nodes[0].Id);
        Assert.Empty(payload.Edges);
    }

    [Fact]
    public void FullDagEnabledIncludesExpressionAndConstNodes()
    {
        var graph = CreateGraphWithFullDagNodes();
        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.EnableFullDag = true;
        overlays.IncludeServiceNodes = true;
        overlays.IncludeExpressionNodes = true;
        overlays.IncludeConstNodes = true;

        var renderCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true);
        renderCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.OverlaySettings, overlays));

        var payload = Assert.IsType<CanvasRenderRequest>(renderCall.Invocations.Single().Arguments[1]);

        Assert.Equal(3, payload.Nodes.Count);
        Assert.Equal(2, payload.Edges.Count);
        Assert.True(payload.Overlays.EnableFullDag);

        var ids = payload.Nodes.Select(n => n.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(new[] { "calc", "source", "svc" }, ids);
    }

    [Fact]
    public void ShowsFilteredEmptyStateWhenNoNodesVisible()
    {
        var graph = CreateGraph();
        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.IncludeServiceNodes = false;

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.render", _ => true).SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.OverlaySettings, overlays));

        cut.WaitForAssertion(() =>
        {
            var filtered = cut.Find("[data-testid='topology-filtered-empty']");
            Assert.Contains("Adjust the feature bar filters", filtered.InnerHtml, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static TopologyGraph CreateGraphWithFullDagNodes()
    {
        var nodes = new[]
        {
            new TopologyNode("source", "const", Array.Empty<string>(), new[] { "calc" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("calc", "expr", new[] { "source" }, new[] { "svc" }, 1, 0, 200, 120, false, EmptySemantics()),
            new TopologyNode("svc", "service", new[] { "calc" }, Array.Empty<string>(), 2, 0, 400, 240, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("edge_source_calc", "source", "calc", 1, null, null, null, null),
            new TopologyEdge("edge_calc_svc", "calc", "svc", 1, null, null, null, null)
        };

        return new TopologyGraph(nodes, edges);
    }

    private static TopologyGraph CreateGraph()
    {
        var nodes = new[]
        {
            new TopologyNode("ingress", "service", Array.Empty<string>(), new[] { "processor" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("processor", "service", new[] { "ingress" }, new[] { "egress" }, 1, 0, 240, 140, false, EmptySemantics()),
            new TopologyNode("egress", "queue", new[] { "processor" }, Array.Empty<string>(), 2, 0, 480, 280, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("edge_ingress_processor", "ingress", "processor", 1, null, null, null, null),
            new TopologyEdge("edge_processor_egress", "processor", "egress", 1, null, null, null, null)
        };

        return new TopologyGraph(nodes, edges);
    }

    private static IReadOnlyDictionary<string, NodeBinMetrics> CreateMetrics()
    {
        return new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            ["ingress"] = new NodeBinMetrics(0.96, 0.70, 0.01, 5, 2.3, DateTimeOffset.UtcNow),
            ["processor"] = new NodeBinMetrics(0.88, 0.80, 0.02, 8, 3.2, DateTimeOffset.UtcNow),
            ["egress"] = new NodeBinMetrics(0.75, 0.92, 0.04, 12, 5.8, DateTimeOffset.UtcNow)
        };
    }

    private static NodeSparklineData CreateSparklineWithSlices(double?[] successRate, double?[] utilization, int startIndex)
    {
        var additional = new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase)
        {
            ["successRate"] = new SparklineSeriesSlice(successRate, startIndex),
            ["utilization"] = new SparklineSeriesSlice(utilization, startIndex)
        };

        return NodeSparklineData.Create(
            successRate,
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            startIndex,
            additionalSeries: additional);
    }

    private static TopologyNodeSemantics EmptySemantics() => new(
        Arrivals: null,
        Served: null,
        Errors: null,
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
