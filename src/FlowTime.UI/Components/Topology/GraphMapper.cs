using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

internal static class GraphMapper
{
    private const double horizontalSpacing = 240d;
    private const double verticalSpacing = 140d;

    private const int farLeftLane = -2;
    private const int innerLeftLane = -1;
    private const int centerLane = 0;
    private const int innerRightLane = 1;
    private const int leafLane = 2;

    private static readonly int[] serviceLanePreference = { centerLane, innerRightLane, innerLeftLane };
    private static readonly int[] supportLanePreference = { farLeftLane, innerLeftLane };

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

                fromNode.Outputs.Add(toId);
                toNode.Inputs.Add(fromId);

                edges.Add(new TopologyEdge(edge.Id, fromId, toId, edge.Weight, edge.EdgeType, edge.Field, edge.Multiplier, edge.Lag));
            }
        }

        var layerByNode = ComputeLayers(nodeBuilders);
        NormalizeLayers(layerByNode);
        var indexByNode = ComputeLayerIndices(layerByNode, nodeBuilders);

        Dictionary<string, LayoutPoint>? layoutOverrides = null;
        if (!respectUiPositions && layout == LayoutMode.HappyPath)
        {
            layoutOverrides = BuildHappyPathLayout(nodeBuilders, layerByNode);
        }

        var mappedNodes = nodeBuilders.Values
            .OrderBy(n => layerByNode.GetValueOrDefault(n.Id))
            .ThenBy(n => indexByNode.GetValueOrDefault(n.Id))
            .Select(builder =>
            {
                var layer = layerByNode.GetValueOrDefault(builder.Id);
                LayoutPoint overridePoint = default;
                var hasOverride = layoutOverrides is not null && layoutOverrides.TryGetValue(builder.Id, out overridePoint);
                var index = hasOverride
                    ? overridePoint.OrderHint
                    : indexByNode.GetValueOrDefault(builder.Id);

                var hasCustomPosition = respectUiPositions && builder.Ui?.X is not null && builder.Ui.Y is not null;
                double x;
                double y;
                if (hasCustomPosition)
                {
                    x = builder.Ui!.X!.Value;
                    y = builder.Ui!.Y!.Value;
                }
                else if (hasOverride)
                {
                    x = overridePoint.X;
                    y = overridePoint.Y;
                }
                else
                {
                    var spacing = ResolvehorizontalSpacing(builder, layout);
                    x = index * spacing;
                    y = layer * verticalSpacing;
                }

                var laneValue = hasOverride && overridePoint.Lane != 0
                    ? overridePoint.Lane
                    : (int)Math.Round(x / horizontalSpacing);

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
                    builder.Semantics,
                    laneValue);
            })
            .ToImmutableArray();

        var normalizedEdges = edges
            .Select(edge => new TopologyEdge(
                edge.Id,
                edge.From,
                edge.To,
                edge.Weight,
                edge.EdgeType,
                edge.Field,
                edge.Multiplier,
                edge.Lag))
            .ToImmutableArray();

        return new TopologyGraph(mappedNodes, normalizedEdges);
    }

    private static double ResolvehorizontalSpacing(NodeBuilder builder, LayoutMode layout)
    {
        var category = Classify(builder.Kind);
        if (category == NodeCategory.Expression || category == NodeCategory.Constant)
        {
            return layout == LayoutMode.HappyPath
                ? horizontalSpacing * 0.55
                : horizontalSpacing * 0.7;
        }

        return horizontalSpacing;
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

    private static Dictionary<string, LayoutPoint> BuildHappyPathLayout(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, int> layerByNode)
    {
        var positions = new Dictionary<string, LayoutPoint>(StringComparer.OrdinalIgnoreCase);
        if (nodeBuilders.Count == 0)
        {
            return positions;
        }

        var categoryById = nodeBuilders.Values.ToDictionary(
            b => b.Id,
            b => Classify(b.Kind),
            StringComparer.OrdinalIgnoreCase);

        var laneByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rowByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var occupancy = new HashSet<(int Lane, int Row)>();

        AssignServiceNodes(nodeBuilders, categoryById, laneByNode, rowByNode, occupancy, layerByNode);
        AssignSupportingNodes(nodeBuilders, categoryById, laneByNode, rowByNode, occupancy);
        AssignLeafNodes(nodeBuilders, categoryById, laneByNode, rowByNode, occupancy);

        NormalizeRows(rowByNode);

        foreach (var builder in nodeBuilders.Values)
        {
            var lane = laneByNode.GetValueOrDefault(builder.Id, centerLane);
            var row = rowByNode.GetValueOrDefault(builder.Id, 0);
            var x = lane * horizontalSpacing;
            var y = row * verticalSpacing;
            var orderHint = (row * 100) + lane;
            positions[builder.Id] = new LayoutPoint(x, y, orderHint, lane);
        }

        return positions;
    }

    private static void AssignServiceNodes(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, NodeCategory> categoryById,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy,
        Dictionary<string, int> layerByNode)
    {
        var serviceNodes = nodeBuilders.Values
            .Where(b => categoryById.GetValueOrDefault(b.Id, NodeCategory.Service) == NodeCategory.Service)
            .ToList();

        if (serviceNodes.Count == 0)
        {
            return;
        }

        var serviceSet = new HashSet<string>(serviceNodes.Select(b => b.Id), StringComparer.OrdinalIgnoreCase);
        var inDegree = serviceNodes.ToDictionary(
            b => b.Id,
            b => b.Inputs.Count(id => serviceSet.Contains(id)),
            StringComparer.OrdinalIgnoreCase);

        var ready = new SortedSet<ReadyNode>(ReadyNodeComparer.Instance);
        foreach (var builder in serviceNodes.Where(b => inDegree[b.Id] == 0))
        {
            ready.Add(new ReadyNode(layerByNode.GetValueOrDefault(builder.Id), builder.Order, builder.Id));
        }

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (ready.Count > 0)
        {
            var current = ready.Min;
            ready.Remove(current);
            var builder = nodeBuilders[current.Id];
            var baselineRow = ResolveServiceBaseline(builder, rowByNode);
            var (lane, row) = ResolveServicePlacement(builder, baselineRow, laneByNode, rowByNode, occupancy);
            laneByNode[builder.Id] = lane;
            rowByNode[builder.Id] = row;
            occupancy.Add((lane, row));
            processed.Add(builder.Id);

            foreach (var childId in builder.Outputs.Where(serviceSet.Contains))
            {
                if (!inDegree.ContainsKey(childId))
                {
                    continue;
                }

                inDegree[childId]--;
                if (inDegree[childId] <= 0)
                {
                    ready.Add(new ReadyNode(layerByNode.GetValueOrDefault(childId), nodeBuilders[childId].Order, childId));
                }
            }
        }

        foreach (var builder in serviceNodes.Where(b => !processed.Contains(b.Id)).OrderBy(b => b.Order))
        {
            var baselineRow = ResolveServiceBaseline(builder, rowByNode);
            var (lane, row) = ResolveServicePlacement(builder, baselineRow, laneByNode, rowByNode, occupancy);
            laneByNode[builder.Id] = lane;
            rowByNode[builder.Id] = row;
            occupancy.Add((lane, row));
        }
    }

    private static void AssignSupportingNodes(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, NodeCategory> categoryById,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy)
    {
        var supportNodes = nodeBuilders.Values
            .Where(b => categoryById.GetValueOrDefault(b.Id, NodeCategory.Service) != NodeCategory.Service && b.Outputs.Count > 0)
            .ToList();

        if (supportNodes.Count == 0)
        {
            return;
        }

        var pending = new HashSet<string>(supportNodes.Select(b => b.Id), StringComparer.OrdinalIgnoreCase);

        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var id in pending.ToList())
            {
                var builder = nodeBuilders[id];
                if (builder.Outputs.All(rowByNode.ContainsKey))
                {
                    PlaceSupportNode(builder, laneByNode, rowByNode, occupancy);
                    pending.Remove(id);
                    progressed = true;
                }
            }

            if (!progressed)
            {
                var fallback = pending
                    .Select(id => nodeBuilders[id])
                    .OrderBy(b => ResolveSupportBaseline(b, rowByNode))
                    .ThenBy(b => b.Order)
                    .First();

                PlaceSupportNode(fallback, laneByNode, rowByNode, occupancy);
                pending.Remove(fallback.Id);
            }
        }
    }

    private static void AssignLeafNodes(
        Dictionary<string, NodeBuilder> nodeBuilders,
        Dictionary<string, NodeCategory> categoryById,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy)
    {
        var leaves = nodeBuilders.Values
            .Where(b => categoryById.GetValueOrDefault(b.Id, NodeCategory.Service) != NodeCategory.Service && b.Outputs.Count == 0)
            .OrderBy(b => ResolveLeafBaseline(b, rowByNode))
            .ThenBy(b => b.Order)
            .ToList();

        foreach (var builder in leaves)
        {
            PlaceLeafNode(builder, laneByNode, rowByNode, occupancy);
        }
    }

    private static int ResolveServiceBaseline(NodeBuilder builder, Dictionary<string, int> rowByNode)
    {
        var parentRows = builder.Inputs
            .Select(id => rowByNode.TryGetValue(id, out var row) ? row : (int?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        return parentRows.Count == 0 ? 0 : parentRows.Max() + 1;
    }

    private static (int Lane, int Row) ResolveServicePlacement(
        NodeBuilder builder,
        int baselineRow,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy)
    {
        var adjacency = GetAdjacentServiceLanes(builder, baselineRow, laneByNode, rowByNode);
        foreach (var lane in adjacency)
        {
            if (!occupancy.Contains((lane, baselineRow)))
            {
                return (lane, baselineRow);
            }
        }

        var blocked = new HashSet<int>(adjacency);
        var fallbacks = serviceLanePreference.Where(lane => !blocked.Contains(lane)).ToList();
        if (fallbacks.Count == 0)
        {
            fallbacks.Add(centerLane);
        }

        var bestLane = fallbacks[0];
        var bestRow = int.MaxValue;

        foreach (var lane in fallbacks)
        {
            var row = FindNextAvailableRow(lane, baselineRow, occupancy);
            if (row < bestRow || (row == bestRow && CompareLanePriority(lane, bestLane) < 0))
            {
                bestLane = lane;
                bestRow = row;
            }
        }

        if (bestRow == int.MaxValue)
        {
            bestRow = baselineRow;
        }

        return (bestLane, bestRow);
    }

    private static List<int> GetAdjacentServiceLanes(
        NodeBuilder builder,
        int baselineRow,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode)
    {
        var lanes = new List<int>();
        foreach (var parentId in builder.Inputs)
        {
            if (!laneByNode.TryGetValue(parentId, out var lane) ||
                !rowByNode.TryGetValue(parentId, out var parentRow))
            {
                continue;
            }

            if (baselineRow == parentRow + 1)
            {
                AppendUniqueLane(lanes, lane);
            }
        }

        return lanes;
    }

    private static void PlaceSupportNode(
        NodeBuilder builder,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy)
    {
        var targetRow = ResolveSupportBaseline(builder, rowByNode);
        var laneCandidates = ResolveSupportLaneCandidates(builder, laneByNode);
        var row = targetRow;

        while (true)
        {
            foreach (var lane in laneCandidates)
            {
                if (!occupancy.Contains((lane, row)))
                {
                    laneByNode[builder.Id] = lane;
                    rowByNode[builder.Id] = row;
                    occupancy.Add((lane, row));
                    return;
                }
            }

            row--;
        }
    }

    private static int ResolveSupportBaseline(
        NodeBuilder builder,
        Dictionary<string, int> rowByNode)
    {
        var childRows = builder.Outputs
            .Select(id => rowByNode.TryGetValue(id, out var row) ? row : (int?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (childRows.Count == 0)
        {
            return rowByNode.Values.DefaultIfEmpty(0).Min() - 1;
        }

        return childRows.Min() - 1;
    }

    private static IReadOnlyList<int> ResolveSupportLaneCandidates(
        NodeBuilder builder,
        Dictionary<string, int> laneByNode)
    {
        var childLanes = builder.Outputs
            .Select(id => laneByNode.TryGetValue(id, out var lane) ? lane : (int?)null)
            .Where(l => l.HasValue)
            .Select(l => l!.Value)
            .ToList();

        if (childLanes.Count == 0)
        {
            return supportLanePreference;
        }

        var anchorLane = childLanes.Min();
        var desired = Math.Max(farLeftLane, Math.Min(innerLeftLane, anchorLane - 1));
        if (anchorLane <= farLeftLane)
        {
            desired = farLeftLane;
        }

        if (desired == farLeftLane)
        {
            return supportLanePreference;
        }

        return new[] { desired, farLeftLane };
    }

    private static void PlaceLeafNode(
        NodeBuilder builder,
        Dictionary<string, int> laneByNode,
        Dictionary<string, int> rowByNode,
        HashSet<(int Lane, int Row)> occupancy)
    {
        var targetRow = ResolveLeafBaseline(builder, rowByNode);
        var row = targetRow;

        while (occupancy.Contains((leafLane, row)))
        {
            row++;
        }

        laneByNode[builder.Id] = leafLane;
        rowByNode[builder.Id] = row;
        occupancy.Add((leafLane, row));
    }

    private static int ResolveLeafBaseline(NodeBuilder builder, Dictionary<string, int> rowByNode)
    {
        var parentRows = builder.Inputs
            .Select(id => rowByNode.TryGetValue(id, out var row) ? row : (int?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (parentRows.Count == 0)
        {
            return rowByNode.Values.DefaultIfEmpty(0).Max() + 1;
        }

        return parentRows.Max() + 1;
    }

    private static void NormalizeRows(Dictionary<string, int> rowByNode)
    {
        if (rowByNode.Count == 0)
        {
            return;
        }

        var minRow = rowByNode.Values.Min();
        if (minRow >= 0)
        {
            return;
        }

        foreach (var key in rowByNode.Keys.ToList())
        {
            rowByNode[key] = rowByNode[key] - minRow;
        }
    }

    private static int FindNextAvailableRow(int lane, int startRow, HashSet<(int Lane, int Row)> occupancy)
    {
        var row = startRow;
        while (occupancy.Contains((lane, row)))
        {
            row++;
        }

        return row;
    }

    private static int CompareLanePriority(int left, int right)
    {
        int Rank(int lane)
        {
            for (var i = 0; i < serviceLanePreference.Length; i++)
            {
                if (serviceLanePreference[i] == lane)
                {
                    return i;
                }
            }

            return serviceLanePreference.Length;
        }

        return Rank(left).CompareTo(Rank(right));
    }

    private static void AppendUniqueLane(ICollection<int> lanes, int lane)
    {
        if (!lanes.Contains(lane))
        {
            lanes.Add(lane);
        }
    }

    private readonly record struct ReadyNode(int Layer, int Order, string Id);

    private sealed class ReadyNodeComparer : IComparer<ReadyNode>
    {
        public static ReadyNodeComparer Instance { get; } = new();

        public int Compare(ReadyNode x, ReadyNode y)
        {
            var layer = x.Layer.CompareTo(y.Layer);
            if (layer != 0)
            {
                return layer;
            }

            var order = x.Order.CompareTo(y.Order);
            if (order != 0)
            {
                return order;
            }

            return string.CompareOrdinal(x.Id, y.Id);
        }
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
                Semantics = new TopologyNodeSemantics(null, null, null, null, null, null, null, null, null, null, null, null);
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
                    semantics.Attempts,
                    semantics.Failures,
                    semantics.RetryEcho,
                    semantics.Queue,
                    semantics.Capacity,
                    semantics.Series,
                    semantics.Expression,
                    distribution,
                    semantics.InlineValues);
            }
        }
    }
}

internal readonly record struct LayoutPoint(double X, double Y, int OrderHint, int Lane);

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
    TopologyNodeSemantics Semantics,
    int Lane = 0);

public sealed record TopologyEdge(
    string Id,
    string From,
    string To,
    double Weight,
    string? EdgeType,
    string? Field,
    double? Multiplier = null,
    int? Lag = null);

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
    string? Attempts,
    string? Failures,
    string? RetryEcho,
    string? Queue,
    string? Capacity,
    string? Series,
    string? Expression,
    GraphDistributionModel? Distribution,
    IReadOnlyList<double>? InlineValues);

public sealed record GraphNodeUiModel(double? X, double? Y);

public sealed record GraphEdgeModel(
    string Id,
    string From,
    string To,
    double Weight,
    string? EdgeType,
    string? Field,
    double? Multiplier = null,
    int? Lag = null);

public sealed record TopologyNodeSemantics(
    string? Arrivals,
    string? Served,
    string? Errors,
    string? Attempts,
    string? Failures,
    string? RetryEcho,
    string? Queue,
    string? Capacity,
    string? Series,
    string? Expression,
    TopologyNodeDistribution? Distribution,
    IReadOnlyList<double>? InlineValues);

public sealed record GraphDistributionModel(
    IReadOnlyList<double> Values,
    IReadOnlyList<double> Probabilities);

public sealed record TopologyNodeDistribution(
    IReadOnlyList<double> Values,
    IReadOnlyList<double> Probabilities);
