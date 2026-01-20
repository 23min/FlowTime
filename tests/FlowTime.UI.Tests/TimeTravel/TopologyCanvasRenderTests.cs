using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Configuration;
using FlowTime.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyCanvasRenderTests : TestContext
{
    private const double nodeWidth = 54;
    private const double nodeHeight = 24;
    private const double nodeCornerRadius = 3;
    private const double viewportPadding = 48;

    public TopologyCanvasRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.registerHandlers", invocation => invocation.Arguments.Count == 3).SetVoidResult();
        Services.AddSingleton(BuildDiagnostics.Create(typeof(TopologyCanvas).Assembly));
    }

    [Fact]
    public void RenderRequestsCanvasDraw()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        cut.WaitForAssertion(() => Assert.Single(sceneCall.Invocations));
        cut.WaitForAssertion(() => Assert.Single(overlayCall.Invocations));

        var invocation = sceneCall.Invocations.Single();
        Assert.Equal(2, invocation.Arguments.Count);
        Assert.True(invocation.Arguments[0] is ElementReference);
        var scenePayload = Assert.IsType<CanvasScenePayload>(invocation.Arguments[1]);
        Assert.Equal(graph.Nodes.Count, scenePayload.Nodes.Count);

        var overlayInvocation = overlayCall.Invocations.Single();
        Assert.True(overlayInvocation.Arguments[0] is ElementReference);
        Assert.IsType<CanvasOverlayPayload>(overlayInvocation.Arguments[1]);
    }

    [Fact]
    public void RenderIncludesEffortEdgeMetadata()
    {
        var nodes = new[]
        {
            new TopologyNode("source", "service", "service", Array.Empty<string>(), new[] { "downstream" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("downstream", "service", "service", new[] { "source" }, Array.Empty<string>(), 1, 0, 200, 120, false, EmptySemantics()),
            new TopologyNode("analytics", "service", "service", new[] { "source" }, Array.Empty<string>(), 1, 1, 220, 160, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("edge_source_downstream", "source", "downstream", 1, "throughput", "served", null, null),
            new TopologyEdge("edge_source_analytics", "source", "analytics", 1, "effort", "load", 0.4, 2)
        };

        var graph = new TopologyGraph(nodes, edges);
        var metrics = CreateMetrics();

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var sceneInvocation = sceneCall.Invocations.Single();
        var payload = Assert.IsType<CanvasScenePayload>(sceneInvocation.Arguments[1]);

        var effortEdge = Assert.Single(payload.Edges, e => e.Id == "edge_source_analytics");
        Assert.Equal("effort", effortEdge.EdgeType);
        Assert.Equal("load", effortEdge.Field);
        Assert.Equal(0.4, effortEdge.Multiplier);
        Assert.Equal(2, effortEdge.Lag);

        var throughputEdge = Assert.Single(payload.Edges, e => e.Id == "edge_source_downstream");
        Assert.Equal("throughput", throughputEdge.EdgeType);
        Assert.Equal("served", throughputEdge.Field);
        Assert.Null(throughputEdge.Multiplier);
        Assert.Null(throughputEdge.Lag);
    }

    [Fact]
    public void RenderIncludesParallelismSemantics()
    {
        var semantics = EmptySemantics() with { Parallelism = "2" };
        var graph = new TopologyGraph(
            new[]
            {
                new TopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>());

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);
        var node = Assert.Single(payload.Nodes);
        Assert.NotNull(node.Semantics);
        Assert.Equal("2", node.Semantics!.Parallelism);
    }

    [Fact]
    public void Topology_FocusView_DefaultsToUpstreamOnly()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.FocusViewEnabled, true)
            .Add(p => p.FocusNodeId, "processor")
            .Add(p => p.FocusIncludeDownstream, false));

        var scenePayload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);

        Assert.Contains(scenePayload.Nodes, node => node.Id == "ingress");
        Assert.Contains(scenePayload.Nodes, node => node.Id == "processor");
        Assert.DoesNotContain(scenePayload.Nodes, node => node.Id == "egress");
    }

    [Fact]
    public void Topology_FocusView_RendersWhenFiltersAreCleared()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.IncludeServiceNodes = false;
        overlays.IncludeDlqNodes = false;
        overlays.IncludeExpressionNodes = false;
        overlays.IncludeConstNodes = false;

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.FocusViewEnabled, true)
            .Add(p => p.FocusNodeId, "processor")
            .Add(p => p.FocusIncludeDownstream, false));

        var scenePayload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);

        Assert.Contains(scenePayload.Nodes, node => node.Id == "ingress");
        Assert.Contains(scenePayload.Nodes, node => node.Id == "processor");
        Assert.DoesNotContain(scenePayload.Nodes, node => node.Id == "egress");
    }

    [Fact]
    public void Topology_FocusView_RelayoutsFilteredGraph()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var originalProcessorX = graph.Nodes.Single(node => node.Id == "processor").X;

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.FocusViewEnabled, true)
            .Add(p => p.FocusNodeId, "processor")
            .Add(p => p.FocusIncludeDownstream, false));

        var scenePayload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);
        var processor = Assert.Single(scenePayload.Nodes, node => node.Id == "processor");

        Assert.NotEqual(originalProcessorX, processor.X);
    }

    [Fact]
    public void OverlayPayloadReflectsRetryToggles()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var overlay = new TopologyOverlaySettings
        {
            ShowRetryMetrics = false,
            ShowEdgeMultipliers = false,
            ShowRetryBudget = false,
            ShowTerminalEdges = false
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlay));

        var invocation = overlayCall.Invocations.Single();
        var payload = Assert.IsType<CanvasOverlayPayload>(invocation.Arguments[1]);

        Assert.False(payload.Overlays.ShowRetryMetrics);
        Assert.False(payload.Overlays.ShowEdgeMultipliers);
        Assert.False(payload.Overlays.ShowRetryBudget);
        Assert.False(payload.Overlays.ShowTerminalEdges);
    }

    [Fact]
    public void OverlayPayloadIncludesEdgeOverlayMode()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var overlay = new TopologyOverlaySettings
        {
            EdgeOverlay = EdgeOverlayMode.RetryRate,
            ShowEdgeOverlayLabels = false
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlay));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);

        Assert.Equal(EdgeOverlayMode.RetryRate, payload.Overlays.EdgeOverlay);
        Assert.False(payload.Overlays.ShowEdgeOverlayLabels);
    }

    [Theory]
    [InlineData(TopologyColorBasis.Sla, "95%")]
    [InlineData(TopologyColorBasis.Utilization, "50%")]
    [InlineData(TopologyColorBasis.Errors, "2.3%")]
    [InlineData(TopologyColorBasis.Queue, "42.0")]
    [InlineData(TopologyColorBasis.ServiceTime, "2.0m")]
    [InlineData(TopologyColorBasis.FlowLatency, "80.0m")]
    [InlineData(TopologyColorBasis.Arrivals, "120.0")]
    public void FocusLabel_UsesActiveMetrics_WhenAvailable(TopologyColorBasis basis, string expectedLabel)
    {
        var nodeId = "queue-node";
        var graph = new TopologyGraph(
            new[]
            {
                new TopologyNode(nodeId, "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>());

        var metrics = new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            [nodeId] = new NodeBinMetrics(
                SuccessRate: 0.95,
                Utilization: 0.5,
                ErrorRate: 0.023,
                QueueDepth: 42,
                LatencyMinutes: null,
                Timestamp: DateTimeOffset.UtcNow,
                NodeKind: "serviceWithBuffer",
                ServiceTimeMs: 120_000,
                FlowLatencyMs: 4_800_000,
                RawMetrics: new Dictionary<string, double?> { ["arrivals"] = 120 })
        };

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            [nodeId] = CreateConflictingSparkline()
        };

        var overlays = new TopologyOverlaySettings { ColorBasis = basis };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, 0));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var nodeOverlay = Assert.Single(payload.Nodes, node => node.Id == nodeId);

        Assert.Equal(expectedLabel, nodeOverlay.FocusLabel);
    }

    [Fact]
    public void FocusLabel_FlowLatency_NoCompletions_ShowsDash()
    {
        var nodeId = "queue-node";
        var graph = new TopologyGraph(
            new[]
            {
                new TopologyNode(nodeId, "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>());

        var metrics = new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            [nodeId] = new NodeBinMetrics(
                SuccessRate: null,
                Utilization: null,
                ErrorRate: null,
                QueueDepth: null,
                LatencyMinutes: null,
                Timestamp: DateTimeOffset.UtcNow,
                FlowLatencyMs: null,
                RawMetrics: new Dictionary<string, double?> { ["served"] = 0 })
        };

        var overlays = new TopologyOverlaySettings { ColorBasis = TopologyColorBasis.FlowLatency };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, 0));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var nodeOverlay = Assert.Single(payload.Nodes, node => node.Id == nodeId);

        Assert.Equal("-", nodeOverlay.FocusLabel);
    }

    [Fact]
    public void RetryOverlay_UsesServerEdgeSeries()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var overlay = new TopologyOverlaySettings
        {
            EdgeOverlay = EdgeOverlayMode.RetryRate,
            ShowRetryMetrics = true
        };

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        var edgeSeries = new[]
        {
            new TimeTravelEdgeSeriesDto
            {
                Id = "edge_ingress_processor_attempts",
                From = "ingress",
                To = "processor",
                EdgeType = "dependency",
                Field = "attempts",
                Multiplier = 2,
                Lag = 1,
                Series = new Dictionary<string, double?[]>
                {
                    ["attemptsVolume"] = new double?[] { null, 20, 14 },
                    ["failuresVolume"] = new double?[] { null, 2, 2 },
                    ["retryRate"] = new double?[] { null, 0.1, 0.142857 }
                }
            }
        };

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlay)
            .Add(p => p.ActiveBin, 1)
            .Add(p => p.EdgeSeries, edgeSeries));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);
        Assert.NotNull(payload.EdgeSeries);
        Assert.Equal(edgeSeries, payload.EdgeSeries);
        Assert.Equal(0, payload.EdgeSeriesStartIndex);
    }

    [Fact]
    public void RenderForwardsEdgeSeriesStartIndex()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var edgeSeries = new[]
        {
            new TimeTravelEdgeSeriesDto
            {
                Id = "edge_ingress_processor_flow",
                From = "ingress",
                To = "processor",
                Series = new Dictionary<string, double?[]>
                {
                    ["flowVolume"] = new double?[] { 10, 12, 8 }
                }
            }
        };

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.EdgeSeries, edgeSeries)
            .Add(p => p.EdgeSeriesStartIndex, 7));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);
        Assert.Equal(7, payload.EdgeSeriesStartIndex);
    }

    [Fact]
    public void RenderForwardsEdgeClassSeries()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var edgeSeries = new[]
        {
            new TimeTravelEdgeSeriesDto
            {
                Id = "edge_ingress_processor_flow",
                From = "ingress",
                To = "processor",
                Series = new Dictionary<string, double?[]>
                {
                    ["flowVolume"] = new double?[] { 10, 12, 8 }
                },
                ByClass = new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Priority"] = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["flowVolume"] = new double?[] { 6, 7, 4 }
                    }
                }
            }
        };

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.EdgeSeries, edgeSeries));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);
        var series = Assert.Single(payload.EdgeSeries ?? Array.Empty<TimeTravelEdgeSeriesDto>());
        Assert.NotNull(series.ByClass);
        Assert.True(series.ByClass!.ContainsKey("Priority"));
    }

    [Fact]
    public void OverlayIncludesEdgeQualityAndClassSelection()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var selectedClasses = new[] { "Priority", "Standard" };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.SelectedClasses, selectedClasses)
            .Add(p => p.HasClassSelection, true)
            .Add(p => p.EdgeQuality, "approx"));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        Assert.True(payload.Overlays.HasClassSelection);
        Assert.Equal(selectedClasses, payload.Overlays.SelectedClasses);
        Assert.Equal("approx", payload.Overlays.EdgeQuality);
    }

    [Fact]
    public void OverlayIncludesEdgeWarnings()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var edgeWarnings = new Dictionary<string, IReadOnlyList<EdgeWarningPayload>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ingress->processor"] = new[]
            {
                new EdgeWarningPayload("edge_flow_mismatch_incoming", "Arrivals do not match sum of incoming edge flows.")
            }
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.EdgeWarnings, edgeWarnings));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        Assert.NotNull(payload.EdgeWarnings);
        Assert.True(payload.EdgeWarnings!.ContainsKey("ingress->processor"));
    }

    [Fact]
    public void OverlayIncludesFallbackIndicatorWhenEdgeQualityMissing()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.EdgeQuality, "missing"));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        Assert.True(payload.Overlays.ShowEdgeFallbackIndicator);
    }

    [Fact]
    public void UpdatesMetricsTriggerAdditionalRender()
    {
        var graph = CreateGraph();
        var initialMetrics = CreateMetrics();
        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, initialMetrics));

        cut.WaitForAssertion(() => Assert.Single(sceneCall.Invocations));
        cut.WaitForAssertion(() => Assert.Single(overlayCall.Invocations));

        var updatedMetrics = new Dictionary<string, NodeBinMetrics>(initialMetrics, StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = new NodeBinMetrics(0.65, 0.95, 0.12, 18, 7.1, DateTimeOffset.UtcNow)
        };

        cut.SetParametersAndRender(p => p.Add(x => x.NodeMetrics, updatedMetrics));

        cut.WaitForAssertion(() => Assert.Single(sceneCall.Invocations));
        cut.WaitForAssertion(() => Assert.Equal(2, overlayCall.Invocations.Count));
    }

    [Fact]
    public void DimmedNodesDoNotRebuildProxyStatics()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var initialSignature = cut.Instance.DebugProxySignature;
        var initialStatic = cut.Instance.DebugProxyStatics["processor"];

        cut.SetParametersAndRender(p => p.Add(x => x.DimmedNodes, new[] { "processor" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(initialSignature, cut.Instance.DebugProxySignature);
            Assert.Same(initialStatic, cut.Instance.DebugProxyStatics["processor"]);
        });

        var proxy = cut.Find("[data-node-id='processor']");
        cut.WaitForAssertion(() => Assert.Equal("true", proxy.GetAttribute("data-dimmed")));
    }

    [Fact]
    public void ActiveBinChangesReuseProxyStatics()
    {
        var graph = CreateGraph();
        var initialMetrics = CreateMetrics();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, initialMetrics)
            .Add(p => p.ActiveBin, 0));

        var firstSignature = cut.Instance.DebugProxySignature;
        var cachedStatic = cut.Instance.DebugProxyStatics["processor"];

        var updatedMetrics = CreateMetrics();

        cut.SetParametersAndRender(p => p
            .Add(x => x.ActiveBin, 2)
            .Add(x => x.NodeMetrics, updatedMetrics));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(firstSignature, cut.Instance.DebugProxySignature);
            Assert.Same(cachedStatic, cut.Instance.DebugProxyStatics["processor"]);
        });
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
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

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
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

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
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

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
        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var invocation = sceneCall.Invocations.Single();
        var payload = Assert.IsType<CanvasScenePayload>(invocation.Arguments[1]);

        Assert.All(payload.Nodes, node =>
        {
            if (string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase))
            {
            Assert.True(node.Width >= nodeWidth);
            }
            else
            {
                Assert.Equal(nodeWidth, node.Width);
            }

            Assert.Equal(nodeHeight, node.Height);
            Assert.Equal(nodeCornerRadius, node.CornerRadius);
        });

        var expectedMinX = payload.Nodes.Min(n => n.X - (n.Width / 2));
        var expectedMaxX = payload.Nodes.Max(n => n.X + (n.Width / 2));
        var expectedMinY = payload.Nodes.Min(n => n.Y - (n.Height / 2));
        var expectedMaxY = payload.Nodes.Max(n => n.Y + (n.Height / 2));

        Assert.Equal(expectedMinX, payload.Viewport.MinX, 3);
        Assert.Equal(expectedMaxX, payload.Viewport.MaxX, 3);
        Assert.Equal(expectedMinY, payload.Viewport.MinY, 3);
        Assert.Equal(expectedMaxY, payload.Viewport.MaxY, 3);
        Assert.Equal(viewportPadding, payload.Viewport.Padding);
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

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        const int activeBin = 3;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var overlayPayload = payload.Overlays;

        Assert.Equal(0.92, overlayPayload.SlaSuccessThreshold, 3);
        Assert.Equal(0.77, overlayPayload.SlaWarningCutoff, 3);
        Assert.Equal(0.86, overlayPayload.UtilizationWarningCutoff, 3);
        Assert.Equal(0.91, overlayPayload.UtilizationCriticalCutoff, 3);
        Assert.Equal(0.032, overlayPayload.ErrorWarningCutoff, 3);
        Assert.Equal(0.08, overlayPayload.ErrorCriticalCutoff, 3);
        Assert.Equal(400, overlayPayload.ServiceTimeWarningThresholdMs, 3);
        Assert.Equal(700, overlayPayload.ServiceTimeCriticalThresholdMs, 3);
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

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        const int activeBin = 2;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var processor = Assert.Single(payload.Nodes, node => (node.Id ?? string.Empty) == "processor");

        Assert.Equal("80%", processor.FocusLabel);
    }

    [Fact]
    public void FocusLabelClearsWhenSelectedBasisHasNoData()
    {
        var graph = CreateGraph();
        var initialMetrics = CreateMetrics();
        var metricsWithoutQueue = new Dictionary<string, NodeBinMetrics>(initialMetrics, StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = new NodeBinMetrics(0.88, 0.80, 0.02, null, null, DateTimeOffset.UtcNow, ServiceTimeMs: 260, NodeKind: "service")
        };

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.ColorBasis = TopologyColorBasis.Utilization;

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = CreateSparklineWithSlices(
                new double?[] { 0.90, 0.91, 0.92 },
                new double?[] { 0.75, 0.81, 0.82 },
                startIndex: 0)
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        const int activeBin = 2;

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, initialMetrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var initialPayload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Last().Arguments[1]);
        var initialProcessor = Assert.Single(initialPayload.Nodes, node => (node.Id ?? string.Empty) == "processor");
        Assert.Equal("80%", initialProcessor.FocusLabel);

        var updatedOverlays = overlays.Clone();
        updatedOverlays.ColorBasis = TopologyColorBasis.Queue;

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.NodeMetrics, metricsWithoutQueue)
            .Add(p => p.OverlaySettings, updatedOverlays));

        var updatedPayload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Last().Arguments[1]);
        var updatedProcessor = Assert.Single(updatedPayload.Nodes, node => (node.Id ?? string.Empty) == "processor");
        Assert.Equal(string.Empty, updatedProcessor.FocusLabel);
    }

    [Fact]
    public void FocusLabelForSlaUsesSuccessRateAndClampsToOne()
    {
        var graph = CreateGraph();

        var metrics = new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = new NodeBinMetrics(1.0, null, null, null, null, DateTimeOffset.UtcNow, NodeKind: "service")
        };

        var additional = new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase)
        {
            ["successRate"] = new SparklineSeriesSlice(new double?[] { 1.0 }, 0),
            ["values"] = new SparklineSeriesSlice(new double?[] { 12.48 }, 0)
        };

        var sparkline = NodeSparklineData.Create(
            new double?[] { 12.48 },
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            0,
            additionalSeries: additional);

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = sparkline
        };

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.ColorBasis = TopologyColorBasis.Sla;

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, 0));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var processor = Assert.Single(payload.Nodes, node => (node.Id ?? string.Empty) == "processor");

        Assert.Equal("100%", processor.FocusLabel);
    }

    [Fact]
    public void FocusLabelForSinkUsesFocusBasisInsteadOfCustomValue()
    {
        var nodes = new[]
        {
            new TopologyNode("terminal", "sink", "sink", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
        };
        var graph = new TopologyGraph(nodes, Array.Empty<TopologyEdge>());

        var metrics = new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            ["terminal"] = new NodeBinMetrics(0.92, 0.81, 0.0, null, null, DateTimeOffset.UtcNow, CustomValue: 42, NodeKind: "sink")
        };

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.ColorBasis = TopologyColorBasis.Utilization;

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["terminal"] = CreateSparklineWithSlices(
                new double?[] { 0.90, 0.91, 0.92 },
                new double?[] { 0.75, 0.81, 0.82 },
                startIndex: 0)
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        const int activeBin = 2;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var terminal = Assert.Single(payload.Nodes, node => (node.Id ?? string.Empty) == "terminal");

        Assert.Equal("82%", terminal.FocusLabel);
    }

    [Fact]
    public void SinkOverlayMetrics_SuppressQueueAndUtilization()
    {
        var nodes = new[]
        {
            new TopologyNode("terminal", "sink", "sink", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
        };
        var graph = new TopologyGraph(nodes, Array.Empty<TopologyEdge>());

        var metrics = new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            ["terminal"] = new NodeBinMetrics(0.92, 0.81, 0.0, 12, 5.4, DateTimeOffset.UtcNow, RetryTax: 0.12, NodeKind: "sink")
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var terminal = Assert.Single(payload.Nodes, node => (node.Id ?? string.Empty) == "terminal");

        Assert.NotNull(terminal.Metrics);
        Assert.Null(terminal.Metrics!.Utilization);
        Assert.Null(terminal.Metrics.QueueDepth);
        Assert.Null(terminal.Metrics.LatencyMinutes);
        Assert.Null(terminal.Metrics.RetryTax);
    }

    [Fact]
    public void FocusLabelForServiceTimeBasisIncludesMilliseconds()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();

        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.ColorBasis = TopologyColorBasis.ServiceTime;

        var sparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = CreateServiceTimeSparkline(
                new double?[] { 210, 240, 270 },
                startIndex: 0)
        };

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        const int activeBin = 1;

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, sparklines)
            .Add(p => p.OverlaySettings, overlays)
            .Add(p => p.ActiveBin, activeBin));

        var payload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        var processor = Assert.Single(payload.Nodes, node => (node.Id ?? string.Empty) == "processor");

        Assert.Contains("ms", processor.FocusLabel ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SparklineUpdatesTriggerSceneRender()
    {
        var graph = CreateGraph();
        var metrics = CreateMetrics();
        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        var initialSparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = CreateSparklineWithSlices(
                new double?[] { 0.7, 0.8, 0.9 },
                new double?[] { 0.6, 0.65, 0.7 },
                startIndex: 0)
        };

        var cut = RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.NodeMetrics, metrics)
            .Add(p => p.NodeSparklines, initialSparklines));

        cut.WaitForAssertion(() => Assert.Single(sceneCall.Invocations));
        cut.WaitForAssertion(() => Assert.Single(overlayCall.Invocations));

        var updatedSparklines = new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["processor"] = CreateSparklineWithSlices(
                new double?[] { 0.2, 0.4, 0.6 },
                new double?[] { 0.3, 0.35, 0.4 },
                startIndex: 1)
        };

        cut.SetParametersAndRender(p => p.Add(x => x.NodeSparklines, updatedSparklines));

        cut.WaitForAssertion(() => Assert.Equal(2, sceneCall.Invocations.Count));
        cut.WaitForAssertion(() => Assert.Equal(2, overlayCall.Invocations.Count));
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

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.OverlaySettings, overlays));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);

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

        var sceneCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true);
        sceneCall.SetVoidResult();
        var overlayCall = JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true);
        overlayCall.SetVoidResult();

        RenderComponent<TopologyCanvas>(parameters => parameters
            .Add(p => p.Graph, graph)
            .Add(p => p.OverlaySettings, overlays));

        var payload = Assert.IsType<CanvasScenePayload>(sceneCall.Invocations.Single().Arguments[1]);

        Assert.Equal(3, payload.Nodes.Count);
        Assert.Equal(2, payload.Edges.Count);
        var overlayPayload = Assert.IsType<CanvasOverlayPayload>(overlayCall.Invocations.Single().Arguments[1]);
        Assert.True(overlayPayload.Overlays.EnableFullDag);

        var ids = payload.Nodes.Select(n => n.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(new[] { "calc", "source", "svc" }, ids);
    }

    [Fact]
    public void ShowsFilteredEmptyStateWhenNoNodesVisible()
    {
        var graph = CreateGraph();
        var overlays = TopologyOverlaySettings.Default.Clone();
        overlays.IncludeServiceNodes = false;

        JSInterop.SetupVoid("FlowTime.TopologyCanvas.renderScene", _ => true).SetVoidResult();
        JSInterop.SetupVoid("FlowTime.TopologyCanvas.applyOverlayDelta", _ => true).SetVoidResult();

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
            new TopologyNode("source", "const", "const", Array.Empty<string>(), new[] { "calc" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("calc", "expr", "expr", new[] { "source" }, new[] { "svc" }, 1, 0, 200, 120, false, EmptySemantics()),
            new TopologyNode("svc", "service", "service", new[] { "calc" }, Array.Empty<string>(), 2, 0, 400, 240, false, EmptySemantics())
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
            new TopologyNode("ingress", "service", "service", Array.Empty<string>(), new[] { "processor" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("processor", "service", "service", new[] { "ingress" }, new[] { "egress" }, 1, 0, 240, 140, false, EmptySemantics()),
            new TopologyNode("egress", "queue", "queue", new[] { "processor" }, Array.Empty<string>(), 2, 0, 480, 280, false, EmptySemantics())
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
            ["ingress"] = new NodeBinMetrics(0.96, 0.70, 0.01, 5, 2.3, DateTimeOffset.UtcNow, ServiceTimeMs: 230),
            ["processor"] = new NodeBinMetrics(0.88, 0.80, 0.02, 8, 3.2, DateTimeOffset.UtcNow, ServiceTimeMs: 260),
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

    private static NodeSparklineData CreateServiceTimeSparkline(double?[] serviceTimes, int startIndex)
    {
        var additional = new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceTimeMs"] = new SparklineSeriesSlice(serviceTimes, startIndex)
        };

        return NodeSparklineData.Create(
            serviceTimes,
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            startIndex,
            additionalSeries: additional);
    }

    private static NodeSparklineData CreateConflictingSparkline()
    {
        var values = new double?[] { 999 };
        var additional = new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase)
        {
            ["successRate"] = new SparklineSeriesSlice(new double?[] { 0.1 }, 0),
            ["utilization"] = new SparklineSeriesSlice(new double?[] { 0.9 }, 0),
            ["errorRate"] = new SparklineSeriesSlice(new double?[] { 0.5 }, 0),
            ["queue"] = new SparklineSeriesSlice(new double?[] { 999 }, 0),
            ["serviceTimeMs"] = new SparklineSeriesSlice(new double?[] { 30_000 }, 0),
            ["flowLatencyMs"] = new SparklineSeriesSlice(new double?[] { 642_000 }, 0),
            ["arrivals"] = new SparklineSeriesSlice(new double?[] { 999 }, 0)
        };

        return NodeSparklineData.Create(
            values,
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            0,
            additionalSeries: additional);
    }

    private static TopologyNodeSemantics EmptySemantics() => new(
        Arrivals: null,
        Served: null,
        Errors: null,
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
}
