using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FlowTime.UI.Components.Topology;

public abstract class TopologyCanvasBase : ComponentBase, IDisposable
{
    private const double NodeWidth = 36;
    private const double NodeHeight = 24;
    private const double NodeCornerRadius = 3;
    private const double ViewportPadding = 48;

    private readonly Dictionary<string, TopologyNode> nodeLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeInputs = new(StringComparer.OrdinalIgnoreCase);

    private CanvasRenderRequest? pendingRequest;
    private bool renderScheduled;
    private bool disposed;
    private string? focusedNodeId;
    private TopologyGraph? filteredGraph;
    private DotNetObjectReference<TopologyCanvasBase>? dotNetRef;

    [Inject] protected IJSRuntime JS { get; set; } = default!;

    [Parameter] public TopologyGraph? Graph { get; set; }
    [Parameter] public IReadOnlyDictionary<string, NodeBinMetrics>? NodeMetrics { get; set; }
    [Parameter] public TopologyOverlaySettings OverlaySettings { get; set; } = TopologyOverlaySettings.Default;
    [Parameter] public int ActiveBin { get; set; }
    [Parameter] public IReadOnlyDictionary<string, NodeSparklineData>? NodeSparklines { get; set; }
    [Parameter] public EventCallback<double> ZoomPercentChanged { get; set; }
    protected ElementReference canvasRef;

    protected bool HasVisibleNodes => filteredGraph is { Nodes.Count: > 0 };
    protected bool HasSourceGraph => Graph is { Nodes.Count: > 0 };

    protected IReadOnlyList<NodeProxyViewModel> NodeProxies { get; private set; } = Array.Empty<NodeProxyViewModel>();
    protected TooltipViewModel? ActiveTooltip { get; private set; }

    protected override void OnParametersSet()
    {
        if (!HasSourceGraph)
        {
            filteredGraph = null;
            pendingRequest = null;
            NodeProxies = Array.Empty<NodeProxyViewModel>();
            nodeLookup.Clear();
            nodeInputs.Clear();
            nodeOutputs.Clear();
            focusedNodeId = null;
            ActiveTooltip = null;
            return;
        }

        filteredGraph = FilterGraph(Graph!, OverlaySettings);
        BuildLookup(filteredGraph);

        if (focusedNodeId is not null && !nodeLookup.ContainsKey(focusedNodeId))
        {
            focusedNodeId = null;
            ActiveTooltip = null;
        }

        NodeProxies = BuildNodeProxies(filteredGraph, NodeMetrics, focusedNodeId, OverlaySettings);
        pendingRequest = BuildRenderRequest(filteredGraph, NodeMetrics, NodeSparklines, focusedNodeId, OverlaySettings, ActiveBin);
        renderScheduled = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!renderScheduled || pendingRequest is null || disposed)
        {
            return;
        }

        renderScheduled = false;

        if (!HasVisibleNodes)
        {
            return;
        }

        dotNetRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("FlowTime.TopologyCanvas.registerHandlers", canvasRef, dotNetRef);
        await JS.InvokeVoidAsync("FlowTime.TopologyCanvas.render", canvasRef, pendingRequest);
    }

    protected void FocusNode(string nodeId)
    {
        if (!HasVisibleNodes || !nodeLookup.ContainsKey(nodeId))
        {
            return;
        }

        focusedNodeId = nodeId;
        ActiveTooltip = BuildTooltip(nodeId);
        var graph = filteredGraph ?? Graph!;
        NodeProxies = BuildNodeProxies(graph, NodeMetrics, focusedNodeId, OverlaySettings);
        ScheduleRedraw();
        StateHasChanged();
    }

    protected void OnNodeBlur()
    {
        if (focusedNodeId is null)
        {
            return;
        }

        focusedNodeId = null;
        ActiveTooltip = null;
        var graph = filteredGraph ?? Graph!;
        NodeProxies = BuildNodeProxies(graph, NodeMetrics, focusedNodeId, OverlaySettings);
        ScheduleRedraw();
        StateHasChanged();
    }

    protected void OnNodeKeyDown(KeyboardEventArgs args, string nodeId)
    {
        if (!HasVisibleNodes || !nodeLookup.TryGetValue(nodeId, out var current))
        {
            return;
        }

        switch (args.Key)
        {
            case "Escape":
                OnNodeBlur();
                return;
            case "Enter":
            case " ":
                // Selection placeholder for later milestones
                return;
        }

        var candidate = args.Key switch
        {
            "ArrowRight" => ResolveDirectionalNeighbor(nodeId, nodeOutputs) ?? FindNearest(nodeId, n => n.X > current.X),
            "ArrowLeft" => ResolveDirectionalNeighbor(nodeId, nodeInputs) ?? FindNearest(nodeId, n => n.X < current.X),
            "ArrowDown" => FindNearest(nodeId, n => n.Y > current.Y),
            "ArrowUp" => FindNearest(nodeId, n => n.Y < current.Y),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            FocusNode(candidate);
        }
    }

    private void ScheduleRedraw()
    {
        if (filteredGraph is null)
        {
            return;
        }

        pendingRequest = BuildRenderRequest(filteredGraph, NodeMetrics, NodeSparklines, focusedNodeId, OverlaySettings, ActiveBin);
        renderScheduled = true;
    }

    private static TopologyGraph FilterGraph(TopologyGraph graph, TopologyOverlaySettings overlays)
    {
        var includeServiceNodes = overlays.IncludeServiceNodes;
        var includeExpressionNodes = overlays.EnableFullDag && overlays.IncludeExpressionNodes;
        var includeConstNodes = overlays.EnableFullDag && overlays.IncludeConstNodes;

        if (!includeServiceNodes && !includeExpressionNodes && !includeConstNodes)
        {
            return new TopologyGraph(Array.Empty<TopologyNode>(), Array.Empty<TopologyEdge>());
        }

        var includedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            var category = ClassifyNode(node.Kind);
            var include = category switch
            {
                NodeCategory.Service => includeServiceNodes,
                NodeCategory.Expression => includeExpressionNodes,
                NodeCategory.Constant => includeConstNodes,
                NodeCategory.Other => includeServiceNodes,
                _ => includeServiceNodes
            };

            if (include)
            {
                includedIds.Add(node.Id);
            }
        }

        if (includedIds.Count == 0)
        {
            return new TopologyGraph(Array.Empty<TopologyNode>(), Array.Empty<TopologyEdge>());
        }

        var filteredNodes = graph.Nodes
            .Where(node => includedIds.Contains(node.Id))
            .Select(node => new TopologyNode(
                node.Id,
                node.Kind,
                node.Inputs.Where(id => includedIds.Contains(id)).ToImmutableArray(),
                node.Outputs.Where(id => includedIds.Contains(id)).ToImmutableArray(),
                node.Layer,
                node.Index,
                node.X,
                node.Y,
                node.IsPositionFixed))
            .ToImmutableArray();

        var filteredEdges = graph.Edges
            .Where(edge => includedIds.Contains(edge.From) && includedIds.Contains(edge.To))
            .ToImmutableArray();

        return new TopologyGraph(filteredNodes, filteredEdges);
    }

    private static NodeCategory ClassifyNode(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return NodeCategory.Service;
        }

        var normalized = kind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "expr" or "expression" => NodeCategory.Expression,
            "const" or "constant" or "pmf" => NodeCategory.Constant,
            "service" or "queue" or "router" or "external" or "store" or "flow" => NodeCategory.Service,
            _ => NodeCategory.Other
        };
    }

    private enum NodeCategory
    {
        Service,
        Expression,
        Constant,
        Other
    }

    private void BuildLookup(TopologyGraph graph)
    {
        nodeLookup.Clear();
        nodeInputs.Clear();
        nodeOutputs.Clear();

        foreach (var node in graph.Nodes)
        {
            nodeLookup[node.Id] = node;
            nodeInputs[node.Id] = new HashSet<string>(node.Inputs, StringComparer.OrdinalIgnoreCase);
            nodeOutputs[node.Id] = new HashSet<string>(node.Outputs, StringComparer.OrdinalIgnoreCase);
        }
    }

    private TooltipViewModel? BuildTooltip(string nodeId)
    {
        if (!nodeLookup.TryGetValue(nodeId, out var node))
        {
            return null;
        }

        var metrics = GetMetrics(nodeId);
        var content = TooltipFormatter.Format(nodeId, metrics);
        var tooltipOffset = (NodeHeight / 2) + 72;
        var offsetY = Math.Max(node.Y - tooltipOffset, 12);
        var style = string.Create(CultureInfo.InvariantCulture, $"left: {node.X}px; top: {offsetY}px; transform: translate(-50%, -100%);");
        return new TooltipViewModel(content, style);
    }

    private NodeBinMetrics GetMetrics(string nodeId)
    {
        if (NodeMetrics is not null && NodeMetrics.TryGetValue(nodeId, out var metrics))
        {
            return metrics;
        }

        return new NodeBinMetrics(null, null, null, null, null, null);
    }

    private string? ResolveDirectionalNeighbor(string nodeId, Dictionary<string, HashSet<string>> lookup)
    {
        if (!lookup.TryGetValue(nodeId, out var neighbors) || neighbors.Count == 0)
        {
            return null;
        }

        var visibleNeighbors = neighbors
            .Where(id => nodeLookup.ContainsKey(id))
            .ToList();

        if (visibleNeighbors.Count == 0)
        {
            return null;
        }

        // Prefer deterministic order based on graph ordering.
        if (filteredGraph is null)
        {
            return visibleNeighbors[0];
        }

        var visibleSet = new HashSet<string>(visibleNeighbors, StringComparer.OrdinalIgnoreCase);

        var ordered = filteredGraph.Nodes
            .Where(n => visibleSet.Contains(n.Id))
            .OrderBy(n => n.Layer)
            .ThenBy(n => n.Index)
            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(n => n.Id)
            .FirstOrDefault();

        return ordered ?? visibleNeighbors[0];
    }

    private string? FindNearest(string nodeId, Func<TopologyNode, bool> predicate)
    {
        if (!nodeLookup.TryGetValue(nodeId, out var origin))
        {
            return null;
        }

        var best = default((string Id, double Distance)?);
        foreach (var node in nodeLookup.Values)
        {
            if (node.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!predicate(node))
            {
                continue;
            }

            var dx = node.X - origin.X;
            var dy = node.Y - origin.Y;
            var distance = (dx * dx) + (dy * dy);

            if (best is null || distance < best.Value.Distance)
            {
                best = (node.Id, distance);
            }
        }

        return best?.Id;
    }

    private IReadOnlyList<NodeProxyViewModel> BuildNodeProxies(
        TopologyGraph graph,
        IReadOnlyDictionary<string, NodeBinMetrics>? metrics,
        string? selectedId,
        TopologyOverlaySettings overlays)
    {
        var proxies = new List<NodeProxyViewModel>(graph.Nodes.Count);
        foreach (var node in graph.Nodes)
        {
            NodeBinMetrics nodeMetrics;
            if (metrics is not null && metrics.TryGetValue(node.Id, out var existing))
            {
                nodeMetrics = existing;
            }
            else
            {
                nodeMetrics = new NodeBinMetrics(null, null, null, null, null, null);
            }

            var tooltip = TooltipFormatter.Format(node.Id, nodeMetrics);
            var aria = $"{tooltip.Title}. {string.Join(", ", tooltip.Lines)}.";

            var style = string.Create(
                CultureInfo.InvariantCulture,
                $"left: {node.X}px; top: {node.Y}px; transform: translate(-50%, -50%); --topology-node-width: {NodeWidth}px; --topology-node-height: {NodeHeight}px;");

            var isFocused = !string.IsNullOrWhiteSpace(selectedId) &&
                            node.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase);

            proxies.Add(new NodeProxyViewModel(node.Id, style, aria, isFocused));
        }

        return proxies;
    }

    private static CanvasRenderRequest BuildRenderRequest(
        TopologyGraph graph,
        IReadOnlyDictionary<string, NodeBinMetrics>? metrics,
        IReadOnlyDictionary<string, NodeSparklineData>? sparklines,
        string? focusedNode,
        TopologyOverlaySettings overlays,
        int selectedBin)
    {
        var thresholds = ColorScale.ColorThresholds.FromOverlay(overlays);
        var nodeDtos = graph.Nodes
            .Select(node =>
            {
                NodeBinMetrics nodeMetrics;
                if (metrics is not null && metrics.TryGetValue(node.Id, out var existing))
                {
                    nodeMetrics = existing;
                }
                else
                {
                    nodeMetrics = new NodeBinMetrics(null, null, null, null, null, null);
                }

                var fill = ColorScale.GetFill(nodeMetrics, overlays.ColorBasis, thresholds);
                var stroke = ColorScale.GetStroke(nodeMetrics);
                var isFocused = !string.IsNullOrWhiteSpace(focusedNode) &&
                    node.Id.Equals(focusedNode, StringComparison.OrdinalIgnoreCase);

                NodeSparklineDto? sparklineDto = null;
                if (sparklines is not null && sparklines.TryGetValue(node.Id, out var sparklineData))
                {
                    sparklineDto = new NodeSparklineDto(
                        sparklineData.Values,
                        sparklineData.Utilization,
                        sparklineData.ErrorRate,
                        sparklineData.QueueDepth,
                        sparklineData.Min,
                        sparklineData.Max,
                        sparklineData.IsFlat,
                        sparklineData.StartIndex);
                }

                return new NodeRenderInfo(
                    node.Id,
                    node.Kind,
                    node.X,
                    node.Y,
                    NodeWidth,
                    NodeHeight,
                    NodeCornerRadius,
                    fill,
                    stroke,
                    isFocused,
                    sparklineDto);
            })
            .ToImmutableArray();

        var nodeLookup = nodeDtos.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        var outgoingTotals = graph.Edges
            .GroupBy(edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(edge => Math.Max(0d, edge.Weight)), StringComparer.OrdinalIgnoreCase);

        var edges = graph.Edges
            .Select(edge =>
            {
                if (!nodeLookup.TryGetValue(edge.From, out var fromNode) ||
                    !nodeLookup.TryGetValue(edge.To, out var toNode))
                {
                    return null;
                }

                double? share = null;
                if (outgoingTotals.TryGetValue(edge.From, out var total) && total > 0)
                {
                    share = Math.Clamp(edge.Weight / total, 0d, 1d);
                }

                return new EdgeRenderInfo(
                    edge.Id,
                    edge.From,
                    edge.To,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y,
                    share,
                    edge.EdgeType,
                    edge.Field);
            })
            .Where(edge => edge is not null)
            .Cast<EdgeRenderInfo>()
            .ToImmutableArray();

        CanvasViewport viewport;
        if (graph.Nodes.Count == 0)
        {
            viewport = new CanvasViewport(-ViewportPadding, -ViewportPadding, ViewportPadding, ViewportPadding, ViewportPadding);
        }
        else
        {
            var halfWidth = NodeWidth / 2d;
            var halfHeight = NodeHeight / 2d;

            var minX = graph.Nodes.Min(node => node.X - halfWidth);
            var maxX = graph.Nodes.Max(node => node.X + halfWidth);
            var minY = graph.Nodes.Min(node => node.Y - halfHeight);
            var maxY = graph.Nodes.Max(node => node.Y + halfHeight);

            viewport = new CanvasViewport(minX, minY, maxX, maxY, ViewportPadding);
        }

        var overlayPayload = new OverlaySettingsPayload(
            overlays.ShowLabels,
            overlays.ShowEdgeArrows,
            overlays.ShowEdgeShares,
            overlays.ShowSparklines,
            overlays.SparklineMode,
            overlays.EdgeStyle,
            overlays.AutoLod,
            overlays.ZoomLowThreshold,
            overlays.ZoomMidThreshold,
            overlays.ZoomPercent,
            overlays.ColorBasis,
            overlays.SlaWarningThreshold,
            overlays.UtilizationWarningThreshold,
            overlays.ErrorRateAlertThreshold,
            overlays.NeighborEmphasis,
            overlays.EnableFullDag,
            overlays.IncludeServiceNodes,
            overlays.IncludeExpressionNodes,
            overlays.IncludeConstNodes,
            selectedBin,
            thresholds.SlaSuccess,
            thresholds.SlaWarning,
            thresholds.UtilizationWarning,
            thresholds.UtilizationCritical,
            thresholds.ErrorWarning,
            thresholds.ErrorCritical,
            overlays.ShowArrivalsDependencies,
            overlays.ShowServedDependencies,
            overlays.ShowErrorsDependencies,
            overlays.ShowQueueDependencies,
            overlays.ShowCapacityDependencies,
            overlays.ShowExpressionDependencies);

        TooltipPayload? tooltip = null;
        if (!string.IsNullOrWhiteSpace(focusedNode))
        {
            var content = TooltipFormatter.Format(focusedNode!, metrics is not null && metrics.TryGetValue(focusedNode!, out var m) ? m : new NodeBinMetrics(null, null, null, null, null, null));
            tooltip = new TooltipPayload(content.Title, content.Subtitle, content.Lines);
        }

        return new CanvasRenderRequest(nodeDtos, edges, viewport, overlayPayload, tooltip);
    }

    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        _ = JS.InvokeVoidAsync("FlowTime.TopologyCanvas.dispose", canvasRef);
        dotNetRef?.Dispose();
        dotNetRef = null;
    }

    protected sealed record NodeProxyViewModel(string Id, string Style, string AriaLabel, bool IsFocused);
    protected sealed record TooltipViewModel(TooltipContent Content, string PositionStyle);

    [JSInvokable]
    public Task OnCanvasZoomChanged(double zoomPercent)
    {
        if (!ZoomPercentChanged.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return ZoomPercentChanged.InvokeAsync(zoomPercent);
    }
}
