using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FlowTime.UI.Components.Topology;

public abstract class TopologyCanvasBase : ComponentBase, IDisposable
{
    private const double NodeWidth = 54;
    private const double NodeHeight = 24;
    private const double QueueNodeWidth = NodeWidth;
    private const double NodeCornerRadius = 3;
    private const double LeafCircleScale = 1.25;
    private const double LeafCircleProxyPadding = 6;
    private const double ViewportPadding = 48;

    private readonly Dictionary<string, TopologyNode> nodeLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeInputs = new(StringComparer.OrdinalIgnoreCase);

    private CanvasRenderRequest? pendingRequest;
    private bool renderScheduled;
    private bool disposed;
    private string? focusedNodeId;
    private string? tooltipNodeId;
    private string? selectedNodeId;
    private TopologyGraph? filteredGraph;
    private DotNetObjectReference<TopologyCanvasBase>? dotNetRef;
    private CancellationTokenSource? tooltipDismissCts;
    private bool hasRendered;
    private TopologyGraph? lastSourceGraph;
    private ViewportSnapshot? pendingViewportSnapshot;
    private string? pendingViewportSignature;
    private bool preserveViewportHint;
    private ViewportSnapshot? lastViewportSnapshot;

    [Inject] protected IJSRuntime JS { get; set; } = default!;

    [Parameter] public TopologyGraph? Graph { get; set; }
    [Parameter] public IReadOnlyDictionary<string, NodeBinMetrics>? NodeMetrics { get; set; }
    [Parameter] public TopologyOverlaySettings OverlaySettings { get; set; } = TopologyOverlaySettings.Default;
    [Parameter] public int ActiveBin { get; set; }
    [Parameter] public IReadOnlyDictionary<string, NodeSparklineData>? NodeSparklines { get; set; }
    [Parameter] public EventCallback<double> ZoomPercentChanged { get; set; }
    [Parameter] public EventCallback<ViewportSnapshot> ViewportChanged { get; set; }
    [Parameter] public EventCallback<string?> NodeFocused { get; set; }
    [Parameter] public ViewportSnapshot? RequestedViewport { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public EventCallback SettingsRequested { get; set; }
    [Parameter] public EventCallback ViewportRequestConsumed { get; set; }
    [Parameter] public bool InspectorVisible { get; set; }
    [Parameter] public EventCallback<string?> EdgeHovered { get; set; }
    protected ElementReference canvasRef;

    protected bool HasVisibleNodes => filteredGraph is { Nodes.Count: > 0 };
    protected bool HasSourceGraph => Graph is { Nodes.Count: > 0 };

    protected IReadOnlyList<NodeProxyViewModel> NodeProxies { get; private set; } = Array.Empty<NodeProxyViewModel>();
    protected string? SelectedNodeId => selectedNodeId;

    protected override void OnParametersSet()
    {
        CaptureRequestedViewport();

        if (!HasSourceGraph)
        {
            filteredGraph = null;
            pendingRequest = null;
            NodeProxies = Array.Empty<NodeProxyViewModel>();
            nodeLookup.Clear();
            nodeInputs.Clear();
            nodeOutputs.Clear();
            focusedNodeId = null;
            selectedNodeId = null;
            lastSourceGraph = null;
            hasRendered = false;
            preserveViewportHint = pendingViewportSnapshot is not null;
            lastViewportSnapshot = null;
            return;
        }

        filteredGraph = FilterGraph(Graph!, OverlaySettings);
        BuildLookup(filteredGraph);

        if (focusedNodeId is not null && !nodeLookup.ContainsKey(focusedNodeId))
        {
            focusedNodeId = null;
        }

        if (selectedNodeId is not null && !nodeLookup.ContainsKey(selectedNodeId))
        {
            selectedNodeId = null;
        }

        if (focusedNodeId is null && selectedNodeId is not null)
        {
            focusedNodeId = selectedNodeId;
        }

        NodeProxies = BuildNodeProxies(filteredGraph, NodeMetrics, focusedNodeId, OverlaySettings);
        var snapshotForRender = pendingViewportSnapshot ?? RequestedViewport;
        var preserveViewport = snapshotForRender is not null;
        if (preserveViewport)
        {
            preserveViewportHint = true;
        }
        pendingRequest = BuildRenderRequest(filteredGraph, NodeMetrics, NodeSparklines, focusedNodeId, tooltipNodeId, OverlaySettings, ActiveBin, snapshotForRender, preserveViewport, Title);
        lastSourceGraph = Graph;
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

        if (pendingViewportSnapshot is not null)
        {
            var snapshot = pendingViewportSnapshot;
            pendingViewportSnapshot = null;
            await JS.InvokeVoidAsync("FlowTime.TopologyCanvas.restoreViewport", canvasRef, snapshot);
            pendingViewportSignature = null;
            lastViewportSnapshot = snapshot;
            if (ViewportRequestConsumed.HasDelegate)
            {
                await ViewportRequestConsumed.InvokeAsync();
            }
        }

        await JS.InvokeVoidAsync("FlowTime.TopologyCanvas.render", canvasRef, pendingRequest);
        hasRendered = true;
    }

    protected void SelectNode(string nodeId)
    {
        FocusNodeInternal(nodeId, isSelection: true);
    }

    protected void HoverNode(string nodeId)
    {
        CancelPendingTooltipDismiss();
        FocusNodeInternal(nodeId, isSelection: false, showTooltip: true);
    }

    private void FocusNodeInternal(string nodeId, bool isSelection, bool? showTooltip = null)
    {
        if (!HasVisibleNodes || !nodeLookup.ContainsKey(nodeId))
        {
            return;
        }

        var previousSelection = selectedNodeId;

        focusedNodeId = nodeId;
        if (isSelection)
        {
            selectedNodeId = nodeId;
        }

        if (showTooltip.HasValue)
        {
            tooltipNodeId = showTooltip.Value ? nodeId : null;
        }
        var graph = filteredGraph ?? Graph!;
        NodeProxies = BuildNodeProxies(graph, NodeMetrics, focusedNodeId, OverlaySettings);
        ScheduleRedraw();
        StateHasChanged();

        if (isSelection &&
            InspectorVisible &&
            NodeFocused.HasDelegate &&
            !string.IsNullOrWhiteSpace(selectedNodeId) &&
            !string.Equals(previousSelection, selectedNodeId, StringComparison.OrdinalIgnoreCase))
        {
            _ = NodeFocused.InvokeAsync(selectedNodeId);
        }
    }

    protected void OpenInspector(string nodeId)
    {
        OpenInspectorForNode(nodeId);
    }

    protected void OnNodeBlur()
    {
        HandleNodeBlur(clearSelection: false, notify: false);
    }

    public void ClearFocus()
    {
        HandleNodeBlur(clearSelection: true, notify: true);
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
                HandleNodeBlur(clearSelection: true, notify: true);
                return;
            case "Enter":
                OpenInspectorForNode(nodeId);
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
            FocusNodeInternal(candidate, isSelection: true, showTooltip: false);
        }
    }

    private void OpenInspectorForNode(string nodeId)
    {
        if (!HasVisibleNodes || !nodeLookup.ContainsKey(nodeId))
        {
            return;
        }

        if (!string.Equals(selectedNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
        {
            FocusNodeInternal(nodeId, isSelection: true, showTooltip: false);
        }

        if (NodeFocused.HasDelegate)
        {
            _ = NodeFocused.InvokeAsync(nodeId);
        }
    }

    private void HandleNodeBlur(bool clearSelection, bool notify)
    {
        if (clearSelection)
        {
            CancelPendingTooltipDismiss();
            tooltipNodeId = null;
        }

        if (clearSelection)
        {
            focusedNodeId = null;
            selectedNodeId = null;
        }
        else if (!string.IsNullOrEmpty(selectedNodeId))
        {
            focusedNodeId = selectedNodeId;
        }

        var graph = filteredGraph ?? Graph;
        if (graph is not null)
        {
            NodeProxies = BuildNodeProxies(graph, NodeMetrics, focusedNodeId, OverlaySettings);
        }
        else
        {
            NodeProxies = Array.Empty<NodeProxyViewModel>();
        }

        ScheduleRedraw();
        StateHasChanged();

        if (notify && NodeFocused.HasDelegate)
        {
            _ = NodeFocused.InvokeAsync(clearSelection ? null : selectedNodeId);
        }
    }

    private void ScheduleRedraw()
    {
        if (filteredGraph is null)
        {
            return;
        }

        pendingRequest = BuildRenderRequest(filteredGraph, NodeMetrics, NodeSparklines, focusedNodeId, tooltipNodeId, OverlaySettings, ActiveBin, snapshot: pendingViewportSnapshot ?? RequestedViewport, preserveViewport: preserveViewportHint, title: Title);
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

        if (includedIds.Count == 0 && includeServiceNodes)
        {
            foreach (var node in graph.Nodes)
            {
                if (ClassifyNode(node.Kind) == NodeCategory.Service)
                {
                    includedIds.Add(node.Id);
                }
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
                node.IsPositionFixed,
                node.Semantics,
                node.Lane))
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
            "service" or "queue" or "router" or "external" or "flow" => NodeCategory.Service,
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
            nodeMetrics = new NodeBinMetrics(null, null, null, null, null, null, NodeKind: node.Kind);
            }

            var tooltip = TooltipFormatter.Format(node.Id, nodeMetrics);
            var aria = $"{tooltip.Title}. {string.Join(", ", tooltip.Lines)}.";

            var isLeafComputed = node.Outputs.Count == 0 && IsComputedKind(node.Kind);
            var proxyHeight = isLeafComputed
                ? (NodeHeight * LeafCircleScale) + LeafCircleProxyPadding
                : NodeHeight;
            var proxyWidth = string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase)
                ? QueueNodeWidth
                : NodeWidth;

            var style = string.Create(
                CultureInfo.InvariantCulture,
                $"left: {node.X}px; top: {node.Y}px; transform: translate(-50%, -50%); --topology-node-width: {proxyWidth}px; --topology-node-height: {proxyHeight}px;");

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
        string? tooltipNode,
        TopologyOverlaySettings overlays,
        int selectedBin,
        ViewportSnapshot? snapshot,
        bool preserveViewport,
        string? title)
    {
        var thresholds = ColorScale.ColorThresholds.FromOverlay(overlays);
        var outgoingGroups = graph.Edges
            .GroupBy(edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

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
                    nodeMetrics = new NodeBinMetrics(null, null, null, null, null, null, NodeKind: node.Kind);
                }

                NodeSparklineData? rawSparkline = null;
                NodeSparklineDto? sparklineDto = null;
                if (sparklines is not null && sparklines.TryGetValue(node.Id, out var sparklineData))
                {
                    rawSparkline = sparklineData;
                    sparklineDto = new NodeSparklineDto(
                        sparklineData.Values,
                        sparklineData.Utilization,
                        sparklineData.ErrorRate,
                        sparklineData.QueueDepth,
                        sparklineData.Min,
                        sparklineData.Max,
                        sparklineData.IsFlat,
                        sparklineData.StartIndex,
                        sparklineData.Series.ToDictionary(
                            pair => pair.Key,
                            pair => new SparklineSeriesSliceDto(pair.Value.Values, pair.Value.StartIndex),
                            StringComparer.OrdinalIgnoreCase));
                }

                var fill = DetermineFillColor(node, nodeMetrics, overlays, thresholds, rawSparkline, selectedBin);
                var stroke = ColorScale.GetStroke(nodeMetrics);
                var isFocused = !string.IsNullOrWhiteSpace(focusedNode) &&
                    node.Id.Equals(focusedNode, StringComparison.OrdinalIgnoreCase);

                var focusLabel = FormatFocusLabel(nodeMetrics, rawSparkline, overlays.ColorBasis, selectedBin);
                if (string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase))
                {
                    focusLabel = string.Empty;
                }

                var semantics = node.Semantics;
                NodeSemanticsDto? semanticsDto = null;
                if (semantics is not null)
                {
                    NodeDistributionDto? distributionDto = null;
                    if (semantics.Distribution is not null && semantics.Distribution.Values.Count > 0)
                    {
                        distributionDto = new NodeDistributionDto(semantics.Distribution.Values, semantics.Distribution.Probabilities);
                    }

                    semanticsDto = new NodeSemanticsDto(
                        semantics.Arrivals,
                        semantics.Served,
                        semantics.Errors,
                        semantics.Attempts,
                        semantics.Failures,
                        semantics.RetryEcho,
                        semantics.Queue,
                        semantics.Capacity,
                        semantics.Series,
                        semantics.Expression,
                        distributionDto,
                        semantics.InlineValues,
                        semantics.Aliases);

                    if (distributionDto is null &&
                        string.IsNullOrWhiteSpace(semanticsDto.Arrivals) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Served) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Errors) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Queue) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Capacity) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Series) &&
                        string.IsNullOrWhiteSpace(semanticsDto.Expression) &&
                        (semanticsDto.InlineValues is null || semanticsDto.InlineValues.Count == 0))
                    {
                        semanticsDto = null;
                    }
                }

                var isVisible = true;
                var isLeaf = !outgoingGroups.ContainsKey(node.Id);

                var metricsDto = new NodeMetricSnapshotDto(
                    nodeMetrics.SuccessRate,
                    nodeMetrics.Utilization,
                    nodeMetrics.ErrorRate,
                    nodeMetrics.QueueDepth,
                    nodeMetrics.LatencyMinutes,
                    nodeMetrics.ServiceTimeMs,
                    nodeMetrics.RetryTax,
                    nodeMetrics.RawMetrics);

                var nodeWidth = string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase)
                    ? QueueNodeWidth
                    : NodeWidth;

                return new NodeRenderInfo(
                    node.Id,
                    node.Kind,
                    node.X,
                    node.Y,
                    nodeWidth,
                    NodeHeight,
                    NodeCornerRadius,
                    fill,
                    stroke,
                    isFocused,
                    isVisible,
                    sparklineDto,
                    focusLabel,
                    isLeaf,
                    semanticsDto,
                    metricsDto,
                    node.Lane);
            })
            .ToImmutableArray();

        var nodeLookup = nodeDtos.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        var outgoingTotals = outgoingGroups.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Sum(edge => Math.Max(0d, edge.Weight)),
            StringComparer.OrdinalIgnoreCase);

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
                    edge.Field,
                    edge.Multiplier,
                    edge.Lag);
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
            var halfHeight = NodeHeight / 2d;
            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var node in graph.Nodes)
            {
                var width = string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase) ? QueueNodeWidth : NodeWidth;
                var halfWidth = width / 2d;
                var left = node.X - halfWidth;
                var right = node.X + halfWidth;
                var top = node.Y - halfHeight;
                var bottom = node.Y + halfHeight;

                if (left < minX) minX = left;
                if (right > maxX) maxX = right;
                if (top < minY) minY = top;
                if (bottom > maxY) maxY = bottom;
            }

            viewport = new CanvasViewport(minX, minY, maxX, maxY, ViewportPadding);
        }

        var overlayPayload = new OverlaySettingsPayload(
            overlays.ShowLabels,
            overlays.ShowEdgeArrows,
            overlays.ShowEdgeShares,
            overlays.ShowSparklines,
            overlays.SparklineMode,
            overlays.EdgeStyle,
            overlays.EdgeOverlay,
            overlays.ShowEdgeOverlayLabels,
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
            thresholds.ServiceTimeWarning,
            thresholds.ServiceTimeCritical,
            overlays.ShowArrivalsDependencies,
            overlays.ShowServedDependencies,
            overlays.ShowErrorsDependencies,
            overlays.ShowQueueDependencies,
            overlays.ShowCapacityDependencies,
            overlays.ShowExpressionDependencies,
            overlays.ShowRetryMetrics,
            overlays.ShowEdgeMultipliers);

        TooltipPayload? tooltip = null;
        if (!string.IsNullOrWhiteSpace(tooltipNode))
        {
            var content = TooltipFormatter.Format(tooltipNode!, metrics is not null && metrics.TryGetValue(tooltipNode!, out var m) ? m : new NodeBinMetrics(null, null, null, null, null, null));
            tooltip = new TooltipPayload(content.Title, content.Subtitle, content.Lines);
        }

        var snapshotPayload = preserveViewport ? CreateSnapshotPayload(snapshot) : null;

        return new CanvasRenderRequest(title, nodeDtos, edges, viewport, overlayPayload, tooltip, snapshotPayload, preserveViewport);
    }

    private static bool IsComputedKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return string.Equals(kind, "expr", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "expression", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "const", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "constant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "pmf", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineFillColor(
        TopologyNode node,
        NodeBinMetrics metrics,
        TopologyOverlaySettings overlays,
        ColorScale.ColorThresholds thresholds,
        NodeSparklineData? sparkline,
        int selectedBin)
    {
        var fill = ColorScale.GetFill(metrics, overlays.ColorBasis, thresholds);

        var sampled = SampleSparklineValue(sparkline, overlays.ColorBasis, selectedBin);
        if (sampled.HasValue)
        {
            var sampledMetrics = overlays.ColorBasis switch
            {
                TopologyColorBasis.Utilization => new NodeBinMetrics(null, sampled, null, null, null, null, NodeKind: node.Kind),
                TopologyColorBasis.Errors => new NodeBinMetrics(null, null, sampled, null, null, null, NodeKind: node.Kind),
                TopologyColorBasis.Queue => new NodeBinMetrics(null, null, null, sampled, null, null, NodeKind: node.Kind),
                TopologyColorBasis.ServiceTime => new NodeBinMetrics(null, null, null, null, null, null, NodeKind: node.Kind, ServiceTimeMs: sampled),
                _ => new NodeBinMetrics(sampled, null, null, null, null, null, NodeKind: node.Kind)
            };

            fill = ColorScale.GetFill(sampledMetrics, overlays.ColorBasis, thresholds);
        }

        var kindNormalized = node.Kind?.ToLowerInvariant();
        if (kindNormalized is "const" or "constant" or "pmf")
        {
            return "#60A5FA"; // lighter blue for inputs
        }

        if (kindNormalized is "expr" or "expression")
        {
            return "#E2E8F0";
        }

        if (node.Inputs.Count == 0 && kindNormalized is "service" or "queue" or "router" or "external" or null)
        {
            return ColorScale.SuccessColor;
        }

        return fill;
    }

    private static double? SampleSparklineValue(
        NodeSparklineData? sparkline,
        TopologyColorBasis basis,
        int selectedBin)
    {
        if (sparkline is null || selectedBin < 0)
        {
            return null;
        }

        double? FromPrimarySeries(IReadOnlyList<double?>? series, int startIndex)
        {
            if (series is null)
            {
                return null;
            }

            var offset = selectedBin - startIndex;
            if (offset < 0 || offset >= series.Count)
            {
                return null;
            }

            var raw = series[offset];
            if (!raw.HasValue)
            {
                return null;
            }

            var value = raw.Value;
            return double.IsFinite(value) ? value : null;
        }

        double? FromSlice(SparklineSeriesSlice slice)
        {
            var offset = selectedBin - slice.StartIndex;
            if (offset < 0 || offset >= slice.Values.Count)
            {
                return null;
            }

            var raw = slice.Values[offset];
            if (!raw.HasValue)
            {
                return null;
            }

            var value = raw.Value;
            return double.IsFinite(value) ? value : null;
        }

        double? FromSeries(params string[] keys)
        {
            if (sparkline.Series is null || sparkline.Series.Count == 0)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (sparkline.Series.TryGetValue(key, out var slice))
                {
                    var sample = FromSlice(slice);
                    if (sample.HasValue)
                    {
                        return sample;
                    }
                }
            }

            return null;
        }

        return basis switch
        {
            TopologyColorBasis.Utilization =>
                FromPrimarySeries(sparkline.Utilization, sparkline.StartIndex) ??
                FromSeries("utilization"),
            TopologyColorBasis.Errors =>
                FromPrimarySeries(sparkline.ErrorRate, sparkline.StartIndex) ??
                FromSeries("errorRate", "errors"),
            TopologyColorBasis.Queue =>
                FromPrimarySeries(sparkline.QueueDepth, sparkline.StartIndex) ??
                FromSeries("queue", "queueDepth"),
            TopologyColorBasis.ServiceTime =>
                FromSeries("serviceTimeMs", "service_time_ms", "serviceTime"),
            _ =>
                FromSeries("successRate", "expectation", "values", "output") ??
                FromPrimarySeries(sparkline.Values, sparkline.StartIndex)
        };
    }

    private void CaptureRequestedViewport()
    {
        if (RequestedViewport is null)
        {
            if (pendingViewportSnapshot is null)
            {
                pendingViewportSignature = null;
            }
            return;
        }

        var signature = BuildViewportSignature(RequestedViewport);
        if (pendingViewportSignature == signature)
        {
            return;
        }

        pendingViewportSnapshot = CloneViewport(RequestedViewport);
        pendingViewportSignature = signature;
        preserveViewportHint = true;
    }

    private static ViewportSnapshot CloneViewport(ViewportSnapshot snapshot)
    {
        return new ViewportSnapshot
        {
            Scale = snapshot.Scale,
            OffsetX = snapshot.OffsetX,
            OffsetY = snapshot.OffsetY,
            WorldCenterX = snapshot.WorldCenterX,
            WorldCenterY = snapshot.WorldCenterY,
            OverlayScale = snapshot.OverlayScale,
            BaseScale = snapshot.BaseScale
        };
    }

    private static string BuildViewportSignature(ViewportSnapshot snapshot)
    {
        return FormattableString.Invariant(
            $"{snapshot.Scale:0.####}|{snapshot.OffsetX:0.##}|{snapshot.OffsetY:0.##}|{snapshot.WorldCenterX:0.####}|{snapshot.WorldCenterY:0.####}|{snapshot.OverlayScale:0.####}|{snapshot.BaseScale:0.####}");
    }

    private static string? TryGetSparklineColor(
        NodeSparklineData sparkline,
        TopologyColorBasis basis,
        int selectedBin,
        ColorScale.ColorThresholds thresholds)
    {
        var value = SampleSparklineValue(sparkline, basis, selectedBin);

        if (!value.HasValue)
        {
            return null;
        }

        var metrics = basis switch
        {
            TopologyColorBasis.Utilization => new NodeBinMetrics(null, value, null, null, null, null),
            TopologyColorBasis.Errors => new NodeBinMetrics(null, null, value, null, null, null),
            TopologyColorBasis.Queue => new NodeBinMetrics(null, null, null, value, null, null),
            TopologyColorBasis.ServiceTime => new NodeBinMetrics(null, null, null, null, null, null, ServiceTimeMs: value),
            _ => new NodeBinMetrics(value, null, null, null, null, null)
        };

        return ColorScale.GetFill(metrics, basis, thresholds);
    }

    private static string? FormatFocusLabel(
        NodeBinMetrics metrics,
        NodeSparklineData? sparkline,
        TopologyColorBasis basis,
        int selectedBin)
    {
        var invariant = CultureInfo.InvariantCulture;

        var nodeCategory = ClassifyNode(metrics.NodeKind);
        var isOperationalNode = nodeCategory == NodeCategory.Service;

        if (metrics.CustomValue.HasValue && !isOperationalNode)
        {
            return FormatFocusNumber(metrics.CustomValue.Value, invariant);
        }

        double? sample = SampleSparklineValue(sparkline, basis, selectedBin);
        if (!sample.HasValue)
        {
            sample = basis switch
            {
                TopologyColorBasis.Utilization => metrics.Utilization,
                TopologyColorBasis.Errors => metrics.ErrorRate,
                TopologyColorBasis.Queue => metrics.QueueDepth,
                TopologyColorBasis.ServiceTime => metrics.ServiceTimeMs,
                _ => metrics.SuccessRate
            };
        }

        if (!sample.HasValue)
        {
            if (sparkline is not null && sparkline.Series.TryGetValue("values", out var valuesSlice))
            {
                var valuesSample = SampleSliceValue(valuesSlice, selectedBin);
                if (valuesSample.HasValue)
                {
                    return FormatFocusNumber(valuesSample.Value, invariant);
                }
            }

            return null;
        }

        return basis switch
        {
            TopologyColorBasis.Queue => FormatFocusNumber(sample.Value, invariant),
            TopologyColorBasis.Utilization => FormatFocusPercent(sample.Value, invariant),
            TopologyColorBasis.Errors => FormatFocusPercent(sample.Value, invariant, allowFractional: sample.Value < 0.1),
            TopologyColorBasis.Sla => FormatFocusPercent(sample.Value, invariant),
            TopologyColorBasis.ServiceTime => FormatFocusMilliseconds(sample.Value, invariant),
            _ => FormatFocusNumber(sample.Value, invariant)
        };
    }

    private static string FormatFocusNumber(double value, CultureInfo culture)
    {
        return value.ToString("0.0", culture);
    }

    private static string FormatFocusMilliseconds(double value, CultureInfo culture)
    {
        var format = value >= 1000 ? "0" : value >= 100 ? "0.0" : "0.00";
        return value.ToString(format, culture) + " ms";
    }

    private static string FormatFocusPercent(double value, CultureInfo culture, bool allowFractional = false)
    {
        var percent = value * 100d;
        var format = allowFractional ? "0.0#" : "0";
        return percent.ToString(format, culture) + "%";
    }

    private static double? SampleSliceValue(SparklineSeriesSlice slice, int selectedBin) =>
        SampleSliceValueInternal(slice, selectedBin);

    private static double? SampleSliceValueInternal(SparklineSeriesSlice slice, int selectedBin)
    {
        if (slice.Values is null)
        {
            return null;
        }

        var index = selectedBin - slice.StartIndex;
        if (index < 0 || index >= slice.Values.Count)
        {
            return null;
        }

        var sample = slice.Values[index];
        if (!sample.HasValue)
        {
            return null;
        }

        var value = sample.Value;
        return double.IsFinite(value) ? value : null;
    }

    private static ViewportSnapshotPayload? CreateSnapshotPayload(ViewportSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new ViewportSnapshotPayload(
            snapshot.Scale,
            snapshot.OffsetX,
            snapshot.OffsetY,
            snapshot.WorldCenterX,
            snapshot.WorldCenterY,
            snapshot.OverlayScale,
            snapshot.BaseScale);
    }

    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelPendingTooltipDismiss();
        _ = JS.InvokeVoidAsync("FlowTime.TopologyCanvas.dispose", canvasRef);
        dotNetRef?.Dispose();
        dotNetRef = null;
    }

    protected sealed record NodeProxyViewModel(string Id, string Style, string AriaLabel, bool IsFocused);

    [JSInvokable]
    public Task OnCanvasZoomChanged(double zoomPercent)
    {
        if (!ZoomPercentChanged.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return ZoomPercentChanged.InvokeAsync(zoomPercent);
    }

    [JSInvokable]
    public Task OnViewportChanged(ViewportSnapshot snapshot)
    {
        if (!ViewportChanged.HasDelegate || snapshot is null)
        {
            return Task.CompletedTask;
        }

        lastViewportSnapshot = snapshot;
        preserveViewportHint = true;
        return ViewportChanged.InvokeAsync(snapshot);
    }

    public ValueTask RestoreViewportAsync(ViewportSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return ValueTask.CompletedTask;
        }

        return JS.InvokeVoidAsync("FlowTime.TopologyCanvas.restoreViewport", canvasRef, snapshot);
    }

    public ValueTask<double> FitToViewportAsync()
    {
        return JS.InvokeAsync<double>("FlowTime.TopologyCanvas.fitToViewport", canvasRef);
    }

    public ValueTask ResetViewportAsync()
    {
        pendingViewportSnapshot = null;
        pendingViewportSignature = null;
        preserveViewportHint = false;
        lastViewportSnapshot = null;

        if (!hasRendered)
        {
            return ValueTask.CompletedTask;
        }

        return JS.InvokeVoidAsync("FlowTime.TopologyCanvas.resetViewportState", canvasRef);
    }

    public ValueTask SetInspectorEdgeHoverAsync(string? edgeId)
    {
        if (!hasRendered)
        {
            return ValueTask.CompletedTask;
        }

        return JS.InvokeVoidAsync("FlowTime.TopologyCanvas.setInspectorEdgeHover", canvasRef, edgeId);
    }

    public ValueTask FocusEdgeAsync(string? edgeId, bool centerOnEdge)
    {
        if (!hasRendered)
        {
            return ValueTask.CompletedTask;
        }

        return JS.InvokeVoidAsync("FlowTime.TopologyCanvas.focusEdge", canvasRef, edgeId, centerOnEdge);
    }

    [JSInvokable]
    public Task OnSettingsRequestedFromCanvas()
    {
        if (!SettingsRequested.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return SettingsRequested.InvokeAsync();
    }

    [JSInvokable]
    public Task OnCanvasBackgroundClicked()
    {
        if (HasVisibleNodes)
        {
            ClearFocus();
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnEdgeHoverChanged(string? edgeId)
    {
        if (!EdgeHovered.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return EdgeHovered.InvokeAsync(edgeId);
    }

    protected Task OnNodePointerLeave()
    {
        ScheduleTooltipDismiss();
        return Task.CompletedTask;
    }

    protected void OnNodeHoverLeave()
    {
        ScheduleTooltipDismiss();

        var targetFocus = selectedNodeId;
        if (string.Equals(focusedNodeId, targetFocus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        focusedNodeId = targetFocus;

        var graph = filteredGraph ?? Graph!;
        NodeProxies = BuildNodeProxies(graph, NodeMetrics, focusedNodeId, OverlaySettings);
        ScheduleRedraw();
        StateHasChanged();
    }

    private void ScheduleTooltipDismiss()
    {
        CancelPendingTooltipDismiss();

        tooltipDismissCts = new CancellationTokenSource();
        var token = tooltipDismissCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await InvokeAsync(() =>
                {
                    if (tooltipDismissCts?.IsCancellationRequested == false)
                    {
                        tooltipDismissCts?.Dispose();
                        tooltipDismissCts = null;
                        tooltipNodeId = null;
                        ScheduleRedraw();
                        StateHasChanged();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // expected when pointer re-enters
            }
        }, token);
    }

    private void CancelPendingTooltipDismiss()
    {
        if (tooltipDismissCts is null)
        {
            return;
        }

        if (!tooltipDismissCts.IsCancellationRequested)
        {
            tooltipDismissCts.Cancel();
        }

        tooltipDismissCts.Dispose();
        tooltipDismissCts = null;
    }
}
