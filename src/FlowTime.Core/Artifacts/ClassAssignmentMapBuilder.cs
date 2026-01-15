using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Artifacts;

internal static class ClassAssignmentMapBuilder
{
    public static IReadOnlyDictionary<NodeId, string> Build(ModelDefinition model)
    {
        var map = new Dictionary<NodeId, string>(new NodeIdComparer());
        if (model.Traffic?.Arrivals is not { Count: > 0 })
        {
            return map;
        }

        foreach (var arrival in model.Traffic.Arrivals)
        {
            if (string.IsNullOrWhiteSpace(arrival.NodeId) || string.IsNullOrWhiteSpace(arrival.ClassId))
            {
                continue;
            }

            map[new NodeId(arrival.NodeId)] = arrival.ClassId.Trim();
        }

        return map;
    }

    private sealed class NodeIdComparer : IEqualityComparer<NodeId>
    {
        public bool Equals(NodeId x, NodeId y) =>
            string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(NodeId obj) =>
            obj.Value?.ToLowerInvariant().GetHashCode() ?? 0;
    }
}
