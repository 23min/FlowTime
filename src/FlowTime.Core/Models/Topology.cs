using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowTime.Core.Models;

public sealed record Topology
{
    public required IReadOnlyList<Node> Nodes { get; init; }
    public required IReadOnlyList<Edge> Edges { get; init; }

    public Node GetNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var node = Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
        if (node == null)
            throw new InvalidOperationException($"Topology does not contain node '{nodeId}'.");
        return node;
    }

    public IEnumerable<Edge> GetOutgoingEdges(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        return Edges.Where(edge => string.Equals(edge.Source, nodeId, StringComparison.Ordinal));
    }

    public IEnumerable<Edge> GetIncomingEdges(string nodeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        return Edges.Where(edge => string.Equals(edge.Target, nodeId, StringComparison.Ordinal));
    }
}
