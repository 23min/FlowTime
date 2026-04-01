using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowTime.Core.Models;

public sealed record Topology
{
    private Dictionary<string, List<Edge>>? outgoingIndex;
    private Dictionary<string, List<Edge>>? incomingIndex;

    public required IReadOnlyList<Node> Nodes { get; init; }
    public required IReadOnlyList<Edge> Edges { get; init; }
    public IReadOnlyList<Constraint> Constraints { get; init; } = Array.Empty<Constraint>();

    public Node GetNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var node = Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
        if (node == null)
            throw new InvalidOperationException($"Topology does not contain node '{nodeId}'.");
        return node;
    }

    public IReadOnlyList<Edge> GetOutgoingEdges(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        var index = outgoingIndex ??= BuildIndex(e => e.Source);
        return index.TryGetValue(nodeId, out var edges) ? edges : [];
    }

    public IReadOnlyList<Edge> GetIncomingEdges(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        var index = incomingIndex ??= BuildIndex(e => e.Target);
        return index.TryGetValue(nodeId, out var edges) ? edges : [];
    }

    private Dictionary<string, List<Edge>> BuildIndex(Func<Edge, string> keySelector)
    {
        var index = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var edge in Edges)
        {
            var key = keySelector(edge);
            if (!index.TryGetValue(key, out var list))
            {
                list = [];
                index[key] = list;
            }
            list.Add(edge);
        }
        return index;
    }
}
