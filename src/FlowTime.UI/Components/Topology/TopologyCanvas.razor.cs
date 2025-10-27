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
    private const double NodeWidth = 56;
    private const double NodeHeight = 36;
    private const double NodeCornerRadius = 8;
    private const double ViewportPadding = 48;

    private readonly Dictionary<string, TopologyNode> nodeLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> nodeInputs = new(StringComparer.OrdinalIgnoreCase);

    private CanvasRenderRequest? pendingRequest;
    private bool renderScheduled;
    private bool disposed;
    private string? focusedNodeId;

    [Inject] protected IJSRuntime JS { get; set; } = default!;

    [Parameter] public TopologyGraph? Graph { get; set; }
    [Parameter] public IReadOnlyDictionary<string, NodeBinMetrics>? NodeMetrics { get; set; }
    [Parameter] public double CanvasWidth { get; set; } = 960;
    [Parameter] public double CanvasHeight { get; set; } = 640;

    protected ElementReference canvasRef;

    protected bool HasGraphData => Graph is { Nodes.Count: > 0 };

    protected IReadOnlyList<NodeProxyViewModel> NodeProxies { get; private set; } = Array.Empty<NodeProxyViewModel>();
    protected TooltipViewModel? ActiveTooltip { get; private set; }

    protected override void OnParametersSet()
    {
        if (!HasGraphData)
        {
            pendingRequest = null;
            NodeProxies = Array.Empty<NodeProxyViewModel>();
            nodeLookup.Clear();
            nodeInputs.Clear();
            nodeOutputs.Clear();
            focusedNodeId = null;
            ActiveTooltip = null;
            return;
        }

        BuildLookup(Graph!);

        if (focusedNodeId is not null && !nodeLookup.ContainsKey(focusedNodeId))
        {
            focusedNodeId = null;
            ActiveTooltip = null;
        }

        NodeProxies = BuildNodeProxies(Graph!, NodeMetrics, focusedNodeId);
        pendingRequest = BuildRenderRequest(Graph!, NodeMetrics, focusedNodeId);
        renderScheduled = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!renderScheduled || pendingRequest is null || disposed)
        {
            return;
        }

        renderScheduled = false;
        await JS.InvokeVoidAsync("FlowTime.TopologyCanvas.render", canvasRef, pendingRequest);
    }

    protected void FocusNode(string nodeId)
    {
        if (!HasGraphData || !nodeLookup.ContainsKey(nodeId))
        {
            return;
        }

        focusedNodeId = nodeId;
        ActiveTooltip = BuildTooltip(nodeId);
        NodeProxies = BuildNodeProxies(Graph!, NodeMetrics, focusedNodeId);
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
        NodeProxies = BuildNodeProxies(Graph!, NodeMetrics, focusedNodeId);
        ScheduleRedraw();
        StateHasChanged();
    }

    protected void OnNodeKeyDown(KeyboardEventArgs args, string nodeId)
    {
        if (!HasGraphData || !nodeLookup.TryGetValue(nodeId, out var current))
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
        if (!HasGraphData)
        {
            return;
        }

        pendingRequest = BuildRenderRequest(Graph!, NodeMetrics, focusedNodeId);
        renderScheduled = true;
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

        // Prefer deterministic order based on graph ordering.
        if (Graph is null)
        {
            return neighbors.First();
        }

        var ordered = Graph.Nodes
            .Where(n => neighbors.Contains(n.Id))
            .OrderBy(n => n.Layer)
            .ThenBy(n => n.Index)
            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(n => n.Id)
            .FirstOrDefault();

        return ordered ?? neighbors.First();
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
        string? selectedId)
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
        string? focusedNode)
    {
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

                var fill = ColorScale.GetFill(nodeMetrics);
                var stroke = ColorScale.GetStroke(nodeMetrics);
                var isFocused = !string.IsNullOrWhiteSpace(focusedNode) &&
                    node.Id.Equals(focusedNode, StringComparison.OrdinalIgnoreCase);

                return new NodeRenderInfo(
                    node.Id,
                    node.X,
                    node.Y,
                    NodeWidth,
                    NodeHeight,
                    NodeCornerRadius,
                    fill,
                    stroke,
                    isFocused);
            })
            .ToImmutableArray();

        var nodeLookup = nodeDtos.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        var edges = graph.Edges
            .Select(edge =>
            {
                if (!nodeLookup.TryGetValue(edge.From, out var fromNode) ||
                    !nodeLookup.TryGetValue(edge.To, out var toNode))
                {
                    return null;
                }

                return new EdgeRenderInfo(
                    edge.Id,
                    edge.From,
                    edge.To,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y);
            })
            .Where(edge => edge is not null)
            .Cast<EdgeRenderInfo>()
            .ToImmutableArray();

        var halfWidth = NodeWidth / 2d;
        var halfHeight = NodeHeight / 2d;

        var minX = graph.Nodes.Min(node => node.X - halfWidth);
        var maxX = graph.Nodes.Max(node => node.X + halfWidth);
        var minY = graph.Nodes.Min(node => node.Y - halfHeight);
        var maxY = graph.Nodes.Max(node => node.Y + halfHeight);

        var viewport = new CanvasViewport(minX, minY, maxX, maxY, ViewportPadding);
        return new CanvasRenderRequest(nodeDtos, edges, viewport);
    }

    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        _ = JS.InvokeVoidAsync("FlowTime.TopologyCanvas.dispose", canvasRef);
    }

    protected sealed record NodeProxyViewModel(string Id, string Style, string AriaLabel, bool IsFocused);
    protected sealed record TooltipViewModel(TooltipContent Content, string PositionStyle);
}
