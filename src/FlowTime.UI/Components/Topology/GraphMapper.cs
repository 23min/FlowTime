using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

internal static class GraphMapper
{
    private const double horizontalSpacing = 240d;
    private const double verticalSpacing = 140d;
    private const int columnsPerSide = 2;
    private const int gridColumns = columnsPerSide * 2 + 1;
    private static readonly double[] columnOffsetUnits = { -2, -1, 0, 1, 2 };

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
        var laneByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sideByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var columnIndexByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var layerPositionByNode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var categoryById = nodeBuilders.Values.ToDictionary(b => b.Id, b => Classify(b.Kind), StringComparer.OrdinalIgnoreCase);

        var laneUsage = new HashSet<(int Layer, int Lane)>();
        var laneSeedCursor = 0;
        var maxOperationalLayer = nodeBuilders.Values
            .Where(builder => categoryById.GetValueOrDefault(builder.Id, NodeCategory.Service) == NodeCategory.Service)
            .Select(builder => layerByNode.GetValueOrDefault(builder.Id))
            .DefaultIfEmpty(layerByNode.Values.DefaultIfEmpty(0).Max())
            .Max();

        var serviceNodes = nodeBuilders.Values
            .Where(builder => categoryById.GetValueOrDefault(builder.Id, NodeCategory.Service) == NodeCategory.Service)
            .OrderBy(builder => layerByNode.GetValueOrDefault(builder.Id))
            .ThenBy(builder => builder.Order)
            .ToList();

        foreach (var builder in serviceNodes)
        {
            var layer = layerByNode.GetValueOrDefault(builder.Id);
            var parentLanes = builder.Inputs
                .Where(id => categoryById.GetValueOrDefault(id, NodeCategory.Service) == NodeCategory.Service && laneByNode.ContainsKey(id))
                .Select(id => laneByNode[id])
                .ToList();

            var candidateLane = parentLanes.Count > 0
                ? (int)Math.Round(parentLanes.Average(), MidpointRounding.AwayFromZero)
                : NextLaneSeed(ref laneSeedCursor);

            var resolvedLane = ResolveLaneConflict(layer, candidateLane, laneUsage);
            laneByNode[builder.Id] = resolvedLane;
            sideByNode[builder.Id] = 0;
            columnIndexByNode[builder.Id] = columnsPerSide;
            layerPositionByNode[builder.Id] = layer;

            var x = resolvedLane * horizontalSpacing;
            var y = layer * verticalSpacing;
            var orderHint = resolvedLane * 10;
            positions[builder.Id] = new LayoutPoint(x, y, orderHint);
        }

        var serviceMeanX = serviceNodes.Count > 0
            ? serviceNodes.Select(builder => positions[builder.Id].X).Average()
            : 0d;
        var serviceLaneXs = serviceNodes
            .Select(builder => positions[builder.Id].X)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var computeNodes = nodeBuilders.Values
            .Where(builder => categoryById.GetValueOrDefault(builder.Id, NodeCategory.Service) != NodeCategory.Service)
            .OrderByDescending(builder => layerByNode.GetValueOrDefault(builder.Id))
            .ThenBy(builder => builder.Order)
            .ToList();

        var columnUsage = new Dictionary<(int Lane, int Side), int>();
        var columnRowUsage = new Dictionary<(int Lane, int ColumnIndex), HashSet<int>>();

        foreach (var builder in computeNodes)
        {
            var layer = layerByNode.GetValueOrDefault(builder.Id);
            var side = ResolveAuxiliarySide(builder, sideByNode, categoryById);
            var downstreamLanes = builder.Outputs
                .Where(id => laneByNode.ContainsKey(id))
                .Select(id => laneByNode[id])
                .ToList();

            var upstreamLanes = builder.Inputs
                .Where(id => laneByNode.ContainsKey(id))
                .Select(id => laneByNode[id])
                .ToList();

            var anchorLane = downstreamLanes.Count > 0
                ? (int)Math.Round(downstreamLanes.Average(), MidpointRounding.AwayFromZero)
                : upstreamLanes.Count > 0
                    ? (int)Math.Round(upstreamLanes.Average(), MidpointRounding.AwayFromZero)
                    : 0;

            var sibling = builder.Outputs
                .FirstOrDefault(id =>
                    columnIndexByNode.ContainsKey(id)
                    && categoryById.GetValueOrDefault(id, NodeCategory.Service) != NodeCategory.Service);

            int gridColumn;
            if (!string.IsNullOrEmpty(sibling) && columnIndexByNode.TryGetValue(sibling, out var siblingColumn))
            {
                gridColumn = siblingColumn;
                side = ResolveSideFromColumn(gridColumn);
            }
            else
            {
                gridColumn = AllocateGridColumn(anchorLane, ref side, columnUsage);
            }

            sideByNode[builder.Id] = side;
            laneByNode[builder.Id] = anchorLane;
            columnIndexByNode[builder.Id] = gridColumn;

            var layerPosition = ResolveAuxiliaryLayerPosition(
                builder,
                layerPositionByNode,
                layerByNode,
                categoryById,
                maxOperationalLayer,
                gridColumn);
            var desiredY = layerPosition * verticalSpacing;
            var dependentLimit = ResolveDependentLimit(builder, positions);
            var columnKey = (anchorLane, gridColumn);
            var snappedY = AllocateColumnRow(columnKey, desiredY, dependentLimit, columnRowUsage);
            layerPosition = snappedY / verticalSpacing;
            layerPositionByNode[builder.Id] = layerPosition;

            var x = (anchorLane + ResolveColumnOffset(gridColumn)) * horizontalSpacing;
            var y = snappedY;
            var orderHint = (anchorLane * 10) + (gridColumn - columnsPerSide);
            positions[builder.Id] = new LayoutPoint(x, y, orderHint);
        }

        EnforceSupportingNodeElevations(computeNodes, positions, layerPositionByNode, layerByNode);
        AlignLeafNodes(computeNodes, positions, serviceMeanX, serviceLaneXs);
        ResolveSupportingCollisions(computeNodes, positions);
        return positions;
    }

    private static int ResolveAuxiliarySide(
        NodeBuilder builder,
        Dictionary<string, int> sideByNode,
        Dictionary<string, NodeCategory> categoryById)
    {
        static int MajoritySide(List<int> sides) => sides
            .GroupBy(side => side)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => Math.Abs(group.Key))
            .Select(group => group.Key)
            .First();

        var downstreamSides = builder.Outputs
            .Where(id => sideByNode.TryGetValue(id, out var side) && side != 0)
            .Select(id => sideByNode[id])
            .ToList();

        if (downstreamSides.Count > 0)
        {
            return MajoritySide(downstreamSides);
        }

        var upstreamSides = builder.Inputs
            .Where(id => sideByNode.TryGetValue(id, out var side) && side != 0)
            .Select(id => sideByNode[id])
            .ToList();

        if (upstreamSides.Count > 0)
        {
            return MajoritySide(upstreamSides);
        }

        var hasServiceOutputs = builder.Outputs.Any(id =>
            categoryById.GetValueOrDefault(id, NodeCategory.Service) == NodeCategory.Service);

        if (hasServiceOutputs)
        {
            return -1;
        }

        var hasServiceInputs = builder.Inputs.Any(id =>
            categoryById.GetValueOrDefault(id, NodeCategory.Service) == NodeCategory.Service);

        if (hasServiceInputs)
        {
            return 1;
        }

        return -1;
    }

    private static int ResolveLaneConflict(int layer, int candidateLane, HashSet<(int Layer, int Lane)> usage)
    {
        if (usage.Add((layer, candidateLane)))
        {
            return candidateLane;
        }

        const int searchRadius = 8;
        for (var offset = 1; offset <= searchRadius; offset++)
        {
            var leftLane = candidateLane - offset;
            if (usage.Add((layer, leftLane)))
            {
                return leftLane;
            }

            var rightLane = candidateLane + offset;
            if (usage.Add((layer, rightLane)))
            {
                return rightLane;
            }
        }

        return candidateLane;
    }

    private static int AllocateGridColumn(
        int lane,
        ref int side,
        Dictionary<(int Lane, int Side), int> usage)
    {
        if (side == 0)
        {
            var left = usage.GetValueOrDefault((lane, -1));
            var right = usage.GetValueOrDefault((lane, 1));
            side = left <= right ? -1 : 1;
        }

        var sideKey = (lane, side);
        var sideCount = usage.GetValueOrDefault(sideKey);
        var oppositeKey = (lane, -side);
        var oppositeCount = usage.GetValueOrDefault(oppositeKey);

        if (sideCount >= columnsPerSide && oppositeCount < columnsPerSide)
        {
            side = -side;
            sideKey = (lane, side);
            sideCount = usage.GetValueOrDefault(sideKey);
        }

        var slotWithinSide = Math.Min(sideCount, columnsPerSide - 1);
        usage[sideKey] = sideCount + 1;

        return ToGridColumn(side, slotWithinSide);
    }

    private static int ToGridColumn(int side, int slotWithinSide)
    {
        slotWithinSide = Math.Max(0, Math.Min(columnsPerSide - 1, slotWithinSide));
        return side switch
        {
            < 0 => Math.Max(0, columnsPerSide - 1 - slotWithinSide),
            > 0 => Math.Min(gridColumns - 1, columnsPerSide + 1 + slotWithinSide),
            _ => columnsPerSide
        };
    }

    private static int ResolveSideFromColumn(int columnIndex)
    {
        if (columnIndex < columnsPerSide)
        {
            return -1;
        }

        if (columnIndex > columnsPerSide)
        {
            return 1;
        }

        return 0;
    }

    private static double ResolveColumnOffset(int columnIndex)
    {
        if (columnIndex >= 0 && columnIndex < columnOffsetUnits.Length)
        {
            return columnOffsetUnits[columnIndex];
        }

        var clamped = Math.Max(0, Math.Min(gridColumns - 1, columnIndex));
        return columnOffsetUnits[clamped];
    }

    private static double AllocateColumnRow(
        (int Lane, int ColumnIndex) key,
        double desiredY,
        double dependentLimitY,
        Dictionary<(int Lane, int ColumnIndex), HashSet<int>> usage)
    {
        var rowHeight = verticalSpacing * 0.55;
        var preferredRow = (int)Math.Round(desiredY / rowHeight);
        preferredRow = Math.Max(0, preferredRow);

        var maxAllowedRow = double.IsPositiveInfinity(dependentLimitY)
            ? int.MaxValue
            : Math.Max(0, (int)Math.Floor((dependentLimitY - (verticalSpacing * 0.35)) / rowHeight));

        if (!usage.TryGetValue(key, out var rows))
        {
            rows = new HashSet<int>();
            usage[key] = rows;
        }

        var row = Math.Min(preferredRow, maxAllowedRow);
        while (row >= 0 && rows.Contains(row))
        {
            row--;
        }

        if (row < 0)
        {
            row = Math.Min(preferredRow, maxAllowedRow);
            while (rows.Contains(row) && row < maxAllowedRow)
            {
                row++;
            }
        }

        if (rows.Contains(row))
        {
            row = maxAllowedRow + 1;
            while (rows.Contains(row))
            {
                row++;
            }
        }

        rows.Add(row);
        return row * rowHeight;
    }

    private static double ResolveDependentLimit(
        NodeBuilder builder,
        Dictionary<string, LayoutPoint> positions)
    {
        var dependentYs = builder.Outputs
            .Select(id => positions.TryGetValue(id, out var point) ? point.Y : (double?)null)
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .ToList();

        if (dependentYs.Count == 0)
        {
            return double.PositiveInfinity;
        }

        return dependentYs.Min();
    }

    private static void EnforceSupportingNodeElevations(
        IEnumerable<NodeBuilder> computeNodes,
        Dictionary<string, LayoutPoint> positions,
        Dictionary<string, double> layerPositionByNode,
        Dictionary<string, int> layerByNode)
    {
        const double ClearanceFactor = 0.35;

        var ordered = computeNodes
            .OrderByDescending(builder => layerByNode.GetValueOrDefault(builder.Id))
            .ThenBy(builder => builder.Order)
            .ToList();

        foreach (var builder in ordered)
        {
            if (builder.Outputs.Count == 0)
            {
                // Allow true leaves to sit below the operational backbone
                continue;
            }

            if (!positions.TryGetValue(builder.Id, out var point))
            {
                continue;
            }

            var dependentYs = builder.Outputs
                .Select(id => positions.TryGetValue(id, out var dep) ? dep.Y : (double?)null)
                .Where(y => y.HasValue)
                .Select(y => y!.Value)
                .ToList();

            if (dependentYs.Count == 0)
            {
                continue;
            }

            var clearance = verticalSpacing * ClearanceFactor;
            var limit = Math.Max(0, dependentYs.Min() - clearance);
            var updatedY = Math.Min(point.Y, limit);
            if (Math.Abs(updatedY - point.Y) > 0.5)
            {
                positions[builder.Id] = point with { Y = updatedY };
                layerPositionByNode[builder.Id] = updatedY / verticalSpacing;
            }
        }
    }

    private static void AlignLeafNodes(
        IEnumerable<NodeBuilder> computeNodes,
        Dictionary<string, LayoutPoint> positions,
        double serviceMeanX,
        IReadOnlyList<double> serviceLaneXs)
    {
        var leaves = computeNodes
            .Where(builder => builder.Outputs.Count == 0)
            .ToList();

        if (leaves.Count == 0)
        {
            return;
        }

        var laneXs = serviceLaneXs is not null && serviceLaneXs.Count > 0
            ? serviceLaneXs
            : new[] { serviceMeanX };

        var laneOrder = laneXs
            .OrderBy(x => Math.Abs(x - serviceMeanX))
            .ThenBy(x => x)
            .ToList();

        for (var i = 0; i < leaves.Count; i++)
        {
            var builder = leaves[i];
            if (!positions.TryGetValue(builder.Id, out var point))
            {
                continue;
            }

            var laneIndex = i % laneOrder.Count;
            var x = laneOrder[laneIndex];
            positions[builder.Id] = point with { X = x };
        }
    }

    private static void ResolveSupportingCollisions(
        IEnumerable<NodeBuilder> computeNodes,
        Dictionary<string, LayoutPoint> positions)
    {
        var groups = computeNodes
            .Where(builder => builder.Outputs.Count > 0)
            .Select(builder =>
            {
                var hasPoint = positions.TryGetValue(builder.Id, out var point);
                return (Builder: builder, HasPoint: hasPoint, Point: point);
            })
            .Where(tuple => tuple.HasPoint)
            .GroupBy(tuple => (int)Math.Round(tuple.Point.X / horizontalSpacing))
            .ToList();

        foreach (var group in groups)
        {
            var usedRows = new HashSet<int>();
            var ordered = group
                .OrderByDescending(tuple => tuple.Point.Y)
                .ToList();

            foreach (var entry in ordered)
            {
                var builder = entry.Builder;
                var point = entry.Point;
                var desiredRow = (int)Math.Floor(point.Y / verticalSpacing);

                while (usedRows.Contains(desiredRow))
                {
                    desiredRow--;
                }

                usedRows.Add(desiredRow);
                var newY = desiredRow * verticalSpacing;
                positions[builder.Id] = point with { Y = newY };
            }
        }
    }

    private static int NextLaneSeed(ref int cursor)
    {
        if (cursor == 0)
        {
            cursor++;
            return 0;
        }

        var magnitude = (cursor + 1) / 2;
        var sign = cursor % 2 == 1 ? 1 : -1;
        cursor++;
        return magnitude * sign;
    }

    private static double ResolveAuxiliaryLayerPosition(
        NodeBuilder builder,
        Dictionary<string, double> layerPositionByNode,
        Dictionary<string, int> layerByNode,
        Dictionary<string, NodeCategory> categoryById,
        int maxOperationalLayer,
        int gridColumn)
    {
        static IEnumerable<double> ResolveNeighborLayers(
            IEnumerable<string> ids,
            Dictionary<string, double> layerPositionByNode,
            Dictionary<string, int> layerByNode)
        {
            foreach (var id in ids)
            {
                if (layerPositionByNode.TryGetValue(id, out var position))
                {
                    yield return position;
                }
                else if (layerByNode.TryGetValue(id, out var layer))
                {
                    yield return layer;
                }
            }
        }

        var columnDistance = Math.Max(0, Math.Abs(gridColumn - columnsPerSide));
        var downstreamLayers = ResolveNeighborLayers(builder.Outputs, layerPositionByNode, layerByNode).ToList();
        if (downstreamLayers.Count > 0)
        {
            var target = downstreamLayers.Min();
            var offset = 0.45 + columnDistance * 0.06;
            return Math.Max(0, target - offset);
        }

        var upstreamLayers = ResolveNeighborLayers(builder.Inputs, layerPositionByNode, layerByNode).ToList();
        if (upstreamLayers.Count > 0)
        {
            var target = upstreamLayers.Max();
            var offset = 0.35 + columnDistance * 0.05;
            return target + offset;
        }

        if (builder.Outputs.Count == 0 && builder.Inputs.Count == 0)
        {
            return maxOperationalLayer + 0.8 + columnDistance * 0.05;
        }

        var fallbackLayer = layerByNode.GetValueOrDefault(builder.Id);
        if (categoryById.GetValueOrDefault(builder.Id, NodeCategory.Service) == NodeCategory.Service)
        {
            return fallbackLayer;
        }

        return maxOperationalLayer + 0.5 + columnDistance * 0.05;
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

internal readonly record struct LayoutPoint(double X, double Y, int OrderHint);

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
