using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

internal static class GraphMapper
{
    private const double HorizontalSpacing = 240d;
    private const double VerticalSpacing = 140d;

    public static TopologyGraph Map(GraphResponseModel response) => Map(response, true);

    public static TopologyGraph Map(GraphResponseModel response, bool respectUiPositions)
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

        var mappedNodes = nodeBuilders.Values
            .OrderBy(n => layerByNode.GetValueOrDefault(n.Id))
            .ThenBy(n => indexByNode.GetValueOrDefault(n.Id))
            .Select(builder =>
            {
                var layer = layerByNode.GetValueOrDefault(builder.Id);
                var index = indexByNode.GetValueOrDefault(builder.Id);

                var hasCustomPosition = respectUiPositions && builder.Ui?.X is not null && builder.Ui.Y is not null;
                // Top→Bottom orientation: y by layer, x by index
                var x = hasCustomPosition
                    ? builder.Ui!.X!.Value
                    : index * HorizontalSpacing;
                var y = hasCustomPosition
                    ? builder.Ui!.Y!.Value
                    : layer * VerticalSpacing;

                return new TopologyNode(
                    builder.Id,
                    builder.Kind,
                    builder.Inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
                    builder.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
                    layer,
                    index,
                    Math.Round(x, 3, MidpointRounding.AwayFromZero),
                    Math.Round(y, 3, MidpointRounding.AwayFromZero),
                    hasCustomPosition);
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

        public NodeBuilder(GraphNodeModel node, int order)
        {
            Id = node.Id ?? throw new ArgumentException("Graph node id is required.", nameof(node));
            Kind = node.Kind ?? "unknown";
            Ui = node.Ui;
            Order = order;
        }
    }
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
    bool IsPositionFixed);

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
    string? Series);

public sealed record GraphNodeUiModel(double? X, double? Y);

public sealed record GraphEdgeModel(
    string Id,
    string From,
    string To,
    double Weight,
    string? EdgeType,
    string? Field);
