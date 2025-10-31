using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

internal static class GraphMapper
{
    private const double HorizontalSpacing = 240d;
    private const double VerticalSpacing = 140d;
    private const int MaxComputeLaneIndex = 3;

    public static TopologyGraph Map(GraphResponseModel response) => Map(response, true, LayoutMode.Layered);

    public static TopologyGraph Map(GraphResponseModel response, bool respectUiPositions, LayoutMode layout)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.Nodes is null || response.Nodes.Count == 0)
        {
            return new TopologyGraph(Array.Empty<TopologyNode>(), Array.Empty<TopologyEdge>());
        }

        var nodes = response.Nodes;
        var nodeBuilders = nodes
            .Select((node, index) => new NodeBuilder(node, index))
            .ToDictionary(builder => builder.Id, StringComparer.OrdinalIgnoreCase);

        var edges = new List<TopologyEdge>(response.Edges?.Count ?? 0);

        if (response.Edges is not null)
        {
            foreach (var edge in response.Edges)
            {
                var fromId = ExtractNodeId(edge.From);
                var toId = ExtractNodeId(edge.To);

                if (!nodeBuilders.TryGetValue(fromId, out var fromNode) ||
                    !nodeBuilders.TryGetValue(toId, out var toNode))
                {
                    continue;
                }

                var isDependencyEdge = string.Equals(edge.EdgeType, "dependency", StringComparison.OrdinalIgnoreCase);

                if (!isDependencyEdge)
                {
                    fromNode.Outputs.Add(toId);
                    toNode.Inputs.Add(fromId);
                }

                edges.Add(new TopologyEdge(edge.Id, fromId, toId, edge.Weight, edge.EdgeType, edge.Field));
            }
        }

        var layerByNode = ComputeLayers(nodeBuilders);
        var indexByNode = ComputeLayerIndices(layerByNode, nodeBuilders);

        var laneOffsetByNode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var verticalOffsetByNode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Optional Happy Path index override (center main path)
        Dictionary<string, int>? happyIndex = null;
        if (!respectUiPositions && layout == LayoutMode.HappyPath)
        {
            happyIndex = ComputeHappyPathIndices(nodeBuilders, layerByNode, indexByNode);
            AdjustComputeNodesForHappyPath(nodeBuilders, layerByNode, indexByNode, happyIndex, edges, laneOffsetByNode, verticalOffsetByNode);
        }
        else
        {
            NormalizeLayers(layerByNode);
        }

        var mappedNodes = nodeBuilders.Values
            .OrderBy(n => layerByNode.GetValueOrDefault(n.Id))
            .ThenBy(n => indexByNode.GetValueOrDefault(n.Id))
            .Select(builder =>
            {
                var layer = layerByNode.GetValueOrDefault(builder.Id);
                var index = happyIndex?.GetValueOrDefault(builder.Id) ?? indexByNode.GetValueOrDefault(builder.Id);

                var hasCustomPosition = respectUiPositions && builder.Ui?.X is not null && builder.Ui.Y is not null;
                var spacing = ResolveHorizontalSpacing(builder, layout);
                var laneOffset = laneOffsetByNode.TryGetValue(builder.Id, out var lane) ? lane : 0d;
                var verticalOffset = verticalOffsetByNode.TryGetValue(builder.Id, out var vy) ? vy : 0d;
                // Top→Bottom orientation: y by layer, x by index
                var x = hasCustomPosition
                    ? builder.Ui!.X!.Value
                    : index * spacing + laneOffset;
                var y = hasCustomPosition
                    ? builder.Ui!.Y!.Value
                    : layer * VerticalSpacing + verticalOffset;

                return new TopologyNode(
                    builder.Id,
                    builder.Kind,
                    builder.Inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
                    builder.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
                    layer,
                    index,
                    Math.Round(x, 3, MidpointRounding.AwayFromZero),
                    Math.Round(y, 3, MidpointRounding.AwayFromZero),
                    hasCustomPosition,
                    builder.Semantics);
            })
            .ToImmutableArray();

        var normalizedEdges = edges
            .Select(edge => new TopologyEdge(
                edge.Id,
                edge.From,
                edge.To,
                edge.Weight,
                edge.EdgeType,
                edge.Field))
            .ToImmutableArray();

        return new TopologyGraph(mappedNodes, normalizedEdges);
    }

    private static void AdjustComputeNodesForHappyPath(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, int> layerByNode,
        Dictionary<string, int> indexByNode,
        Dictionary<string, int> happyIndex,
        IReadOnlyList<TopologyEdge> edges,
        IDictionary<string, double> laneOffsetByNode,
        IDictionary<string, double> verticalOffsetByNode)
    {
        if (happyIndex is null)
        {
            return;
        }

        var categoryById = nodeBuilders.Values.ToDictionary(builder => builder.Id, builder => Classify(builder.Kind), StringComparer.OrdinalIgnoreCase);

        var outgoing = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var incoming = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            if (!string.IsNullOrWhiteSpace(edge.From))
            {
                if (!outgoing.TryGetValue(edge.From, out var list))
                {
                    list = new List<string>();
                    outgoing[edge.From] = list;
                }

                if (!string.IsNullOrWhiteSpace(edge.To))
                {
                    list.Add(edge.To);
                }
            }

            if (!string.IsNullOrWhiteSpace(edge.To))
            {
                if (!incoming.TryGetValue(edge.To, out var list))
                {
                    list = new List<string>();
                    incoming[edge.To] = list;
                }

                if (!string.IsNullOrWhiteSpace(edge.From))
                {
                    list.Add(edge.From);
                }
            }
        }

        var computeIds = nodeBuilders.Keys
            .Where(id =>
            {
                var category = categoryById.GetValueOrDefault(id, NodeCategory.Service);
                return category is NodeCategory.Expression or NodeCategory.Constant;
            })
            .ToList();

        // Iteratively pull compute nodes toward their consumers to achieve top->bottom flow.
        const int maxIterations = 6;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var changed = false;
            foreach (var id in computeIds)
            {
                var current = layerByNode.GetValueOrDefault(id);

                var downLayers = outgoing.GetValueOrDefault(id)?.Select(target => layerByNode.GetValueOrDefault(target)).ToList()
                                ?? new List<int>();
                var upLayers = incoming.GetValueOrDefault(id)?.Select(source => layerByNode.GetValueOrDefault(source)).ToList()
                              ?? new List<int>();

                int? candidate = null;
                if (downLayers.Count > 0)
                {
                    var minDown = downLayers.Min();
                    candidate = minDown - 1;
                }
                else if (upLayers.Count > 0)
                {
                    var maxUp = upLayers.Max();
                    candidate = maxUp + 1;
                }

                if (candidate.HasValue && candidate.Value != current)
                {
                    layerByNode[id] = candidate.Value;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        NormalizeLayers(layerByNode);

        var slotByLayerAndSide = new Dictionary<(int Layer, int Side), int>();

        var orderedCompute = computeIds
            .OrderByDescending(id => layerByNode.GetValueOrDefault(id))
            .ThenBy(id => nodeBuilders[id].Order)
            .ToList();

        foreach (var id in orderedCompute)
        {
            var category = categoryById.GetValueOrDefault(id, NodeCategory.Service);
            var layer = layerByNode.GetValueOrDefault(id);
            var side = category == NodeCategory.Expression ? 1 : -1;

            var referenceIndices = new List<int>();

            if (outgoing.TryGetValue(id, out var outs))
            {
                foreach (var target in outs)
                {
                    if (happyIndex.TryGetValue(target, out var idx))
                    {
                        referenceIndices.Add(idx);
                    }
                    else if (indexByNode.TryGetValue(target, out var fallback))
                    {
                        referenceIndices.Add(fallback);
                    }
                }
            }

            if (referenceIndices.Count == 0 && incoming.TryGetValue(id, out var ins))
            {
                foreach (var source in ins)
                {
                    if (happyIndex.TryGetValue(source, out var idx))
                    {
                        referenceIndices.Add(idx);
                    }
                    else if (indexByNode.TryGetValue(source, out var fallback))
                    {
                        referenceIndices.Add(fallback);
                    }
                }
            }

            var reference = referenceIndices.Count == 0
                ? 0
                : side > 0
                    ? referenceIndices.Max()
                    : referenceIndices.Min();

            var key = (layer, side);
            var offset = slotByLayerAndSide.TryGetValue(key, out var value) ? value : 0;
            slotByLayerAndSide[key] = offset + 1;

            var assignedIndex = side > 0
                ? reference + offset + 1
                : reference - offset - 1;

            assignedIndex = Math.Max(-MaxComputeLaneIndex, Math.Min(MaxComputeLaneIndex, assignedIndex));

            happyIndex[id] = assignedIndex;
            indexByNode[id] = assignedIndex;

            var laneMagnitude = offset + 1;
            var horizontalStep = HorizontalSpacing * 0.32;
            var verticalStep = VerticalSpacing * 0.25;
            laneOffsetByNode[id] = side > 0 ? laneMagnitude * horizontalStep : -laneMagnitude * horizontalStep;
            verticalOffsetByNode[id] = side > 0 ? laneMagnitude * verticalStep : -laneMagnitude * verticalStep;
        }
    }

    private static double ResolveHorizontalSpacing(NodeBuilder builder, LayoutMode layout)
    {
        var category = Classify(builder.Kind);
        if (category == NodeCategory.Expression || category == NodeCategory.Constant)
        {
            return layout == LayoutMode.HappyPath
                ? HorizontalSpacing * 0.55
                : HorizontalSpacing * 0.7;
        }

        return HorizontalSpacing;
    }

    private static void NormalizeLayers(Dictionary<string, int> layerByNode)
    {
        if (layerByNode.Count == 0)
        {
            return;
        }

        var minLayer = layerByNode.Values.Min();
        if (minLayer >= 0)
        {
            return;
        }

        foreach (var key in layerByNode.Keys.ToList())
        {
            layerByNode[key] = layerByNode[key] - minLayer;
        }
    }

    private static Dictionary<string, int> ComputeLayers(Dictionary<string, NodeBuilder> nodeBuilders)
    {
        var inDegree = nodeBuilders.Values
            .ToDictionary(builder => builder.Id, builder => builder.Inputs.Distinct(StringComparer.OrdinalIgnoreCase).Count(), StringComparer.OrdinalIgnoreCase);

        var layerByNode = nodeBuilders.Values.ToDictionary(builder => builder.Id, _ => 0, StringComparer.OrdinalIgnoreCase);

        var startNodes = nodeBuilders.Values
            .Where(builder => inDegree[builder.Id] == 0)
            .OrderBy(builder => builder.Order)
            .Select(builder => builder.Id)
            .ToList();

        var queue = new Queue<string>(startNodes);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var currentLayer = layerByNode[currentId];
            var currentBuilder = nodeBuilders[currentId];

            foreach (var nextId in currentBuilder.Outputs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!inDegree.TryGetValue(nextId, out var degree))
                {
                    continue;
                }

                var updatedLayer = Math.Max(layerByNode[nextId], currentLayer + 1);
                layerByNode[nextId] = updatedLayer;

                var remaining = degree - 1;
                inDegree[nextId] = remaining;

                if (remaining == 0)
                {
                    queue.Enqueue(nextId);
                }
            }
        }

        // Fallback: for any nodes stuck due to cycles, preserve assigned layer order.
        foreach (var builder in nodeBuilders.Values.OrderBy(b => b.Order))
        {
            _ = layerByNode[builder.Id];
        }

        return layerByNode;
    }

    private static NodeCategory Classify(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return NodeCategory.Service;
        }

        return kind.Trim().ToLowerInvariant() switch
        {
            "expr" or "expression" => NodeCategory.Expression,
            "const" or "constant" or "pmf" => NodeCategory.Constant,
            _ => NodeCategory.Service
        };
    }

    private static Dictionary<string, int> ComputeLayerIndices(
        Dictionary<string, int> layerByNode,
        Dictionary<string, NodeBuilder> nodeBuilders)
    {
        var indexByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in layerByNode.GroupBy(kvp => kvp.Value).OrderBy(g => g.Key))
        {
            var orderedLayerNodes = group
                .Select(kvp => nodeBuilders[kvp.Key])
                .OrderBy(builder => builder.Order)
                .ToList();

            for (var i = 0; i < orderedLayerNodes.Count; i++)
            {
                indexByNode[orderedLayerNodes[i].Id] = i;
            }
        }

        return indexByNode;
    }

    private static Dictionary<string, int> ComputeHappyPathIndices(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, int> layerByNode,
        Dictionary<string, int> indexByNode)
    {
        // Simple longest-path reconstruction across layers
        var inputsByNode = nodeBuilders.Values.ToDictionary(b => b.Id, b => b.Inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);
        var outputsByNode = nodeBuilders.Values.ToDictionary(b => b.Id, b => b.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var nodesOrdered = nodeBuilders.Keys
            .OrderBy(id => layerByNode.GetValueOrDefault(id))
            .ThenBy(id => indexByNode.GetValueOrDefault(id))
            .ToList();

        var dist = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in nodesOrdered)
        {
            dist[id] = 0;
            prev[id] = null;
        }

        foreach (var id in nodesOrdered)
        {
            var outs = outputsByNode.GetValueOrDefault(id) ?? new List<string>();
            foreach (var to in outs)
            {
                var cand = dist[id] + 1;
                if (!dist.ContainsKey(to) || cand > dist[to])
                {
                    dist[to] = cand;
                    prev[to] = id;
                }
            }
        }

        var end = nodesOrdered.OrderByDescending(id => dist.GetValueOrDefault(id)).FirstOrDefault();
        var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cursor = end;
        while (!string.IsNullOrEmpty(cursor))
        {
            path.Add(cursor!);
            cursor = prev.GetValueOrDefault(cursor!);
        }

        var leftCount = new Dictionary<int, int>();
        var rightCount = new Dictionary<int, int>();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Precompute median index per layer for biasing
        var medianByLayer = layerByNode
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.Select(k => indexByNode.GetValueOrDefault(k.Key)).DefaultIfEmpty(0).Average());

        foreach (var id in nodesOrdered)
        {
            var layer = layerByNode.GetValueOrDefault(id);
            if (path.Contains(id))
            {
                result[id] = 0;
                continue;
            }

            var index = indexByNode.GetValueOrDefault(id);
            var median = medianByLayer.GetValueOrDefault(layer);
            if (index <= median)
            {
                var count = leftCount.GetValueOrDefault(layer);
                result[id] = -(count + 1);
                leftCount[layer] = count + 1;
            }
            else
            {
                var count = rightCount.GetValueOrDefault(layer);
                result[id] = (count + 1);
                rightCount[layer] = count + 1;
            }
        }

        return result;
    }

    private static string ExtractNodeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var colon = value.IndexOf(':');
        return colon < 0 ? value.Trim() : value[..colon].Trim();
    }

    private sealed class NodeBuilder
    {
        public string Id { get; }
        public string Kind { get; }
        public int Order { get; }
        public GraphNodeUiModel? Ui { get; }
        public List<string> Inputs { get; } = new();
        public List<string> Outputs { get; } = new();
        public TopologyNodeSemantics Semantics { get; }

        public NodeBuilder(GraphNodeModel node, int order)
        {
            Id = node.Id ?? throw new ArgumentException("Graph node id is required.", nameof(node));
            Kind = node.Kind ?? "unknown";
            Ui = node.Ui;
            Order = order;
            var semantics = node.Semantics;
            if (semantics is null)
            {
                Semantics = new TopologyNodeSemantics(null, null, null, null, null, null, null, null);
            }
            else
            {
                TopologyNodeDistribution? distribution = null;
                if (semantics.Distribution is not null)
                {
                    distribution = new TopologyNodeDistribution(
                        semantics.Distribution.Values,
                        semantics.Distribution.Probabilities);
                }

                Semantics = new TopologyNodeSemantics(
                    semantics.Arrivals,
                    semantics.Served,
                    semantics.Errors,
                    semantics.Queue,
                    semantics.Capacity,
                    semantics.Series,
                    distribution,
                    semantics.InlineValues);
            }
        }
    }
}

internal enum NodeCategory
{
    Service,
    Expression,
    Constant
}

public sealed record TopologyGraph(
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyEdge> Edges);

public sealed record TopologyNode(
    string Id,
    string Kind,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    int Layer,
    int Index,
    double X,
    double Y,
    bool IsPositionFixed,
    TopologyNodeSemantics Semantics);

public sealed record TopologyEdge(
    string Id,
    string From,
    string To,
    double Weight,
    string? EdgeType,
    string? Field);

public sealed record GraphResponseModel(
    IReadOnlyList<GraphNodeModel> Nodes,
    IReadOnlyList<GraphEdgeModel> Edges);

public sealed record GraphNodeModel(
    string Id,
    string Kind,
    GraphNodeSemanticsModel Semantics,
    GraphNodeUiModel? Ui);

public sealed record GraphNodeSemanticsModel(
    string Arrivals,
    string Served,
    string Errors,
    string? Queue,
    string? Capacity,
    string? Series,
    GraphDistributionModel? Distribution,
    IReadOnlyList<double>? InlineValues);

public sealed record GraphNodeUiModel(double? X, double? Y);

public sealed record GraphEdgeModel(
    string Id,
    string From,
    string To,
    double Weight,
    string? EdgeType,
    string? Field);

public sealed record TopologyNodeSemantics(
    string? Arrivals,
    string? Served,
    string? Errors,
    string? Queue,
    string? Capacity,
    string? Series,
    TopologyNodeDistribution? Distribution,
    IReadOnlyList<double>? InlineValues);

public sealed record GraphDistributionModel(
    IReadOnlyList<double> Values,
    IReadOnlyList<double> Probabilities);

public sealed record TopologyNodeDistribution(
    IReadOnlyList<double> Values,
    IReadOnlyList<double> Probabilities);
