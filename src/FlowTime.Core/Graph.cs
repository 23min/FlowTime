using System.Collections.Concurrent;
using FlowTime.Core.Nodes;

namespace FlowTime.Core;

public sealed class Graph
{
    private readonly Dictionary<NodeId, INode> nodes;

    public Graph(IEnumerable<INode> nodes)
    {
        this.nodes = nodes.ToDictionary(n => n.Id);
        ValidateAcyclic();
    }

    private void ValidateAcyclic()
    {
        // Kahn's algorithm to detect cycles
        var inDegree = new Dictionary<NodeId, int>();
    foreach (var id in nodes.Keys) inDegree[id] = 0;
    foreach (var n in nodes.Values)
        {
            foreach (var inp in n.Inputs)
            {
        if (nodes.ContainsKey(inp))
                {
                    // count prerequisite edges into n
                    inDegree[n.Id]++;
                }
            }
        }

        var queue = new Queue<NodeId>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        int visited = 0;
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            visited++;
            foreach (var m in nodes.Values)
            {
                if (m.Inputs.Contains(id))
                {
                    inDegree[m.Id]--;
                    if (inDegree[m.Id] == 0) queue.Enqueue(m.Id);
                }
            }
        }
        if (visited != nodes.Count) throw new InvalidOperationException("Graph has a cycle");
    }

    public IReadOnlyDictionary<NodeId, Series> Evaluate(TimeGrid grid)
    {
        var order = TopologicalOrder();
        var memo = new Dictionary<NodeId, Series>();
        foreach (var id in order)
        {
            var node = nodes[id];
            Series GetInput(NodeId n) => memo[n];
            memo[id] = node.Evaluate(grid, GetInput);
        }
        return memo;
    }

    public IReadOnlyList<NodeId> TopologicalOrder()
    {
        var inDegree = new Dictionary<NodeId, int>();
    foreach (var id in nodes.Keys) inDegree[id] = 0;
    foreach (var n in nodes.Values)
            foreach (var inp in n.Inputs)
        if (nodes.ContainsKey(inp)) inDegree[n.Id]++;

        var queue = new Queue<NodeId>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<NodeId>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var m in nodes.Values)
            {
                if (m.Inputs.Contains(id))
                {
                    inDegree[m.Id]--;
                    if (inDegree[m.Id] == 0) queue.Enqueue(m.Id);
                }
            }
        }
        if (order.Count != nodes.Count) throw new InvalidOperationException("Graph has a cycle");
        return order;
    }
}
