using FlowTime.Core.Expressions;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Execution;

public sealed class Graph
{
    private readonly Dictionary<NodeId, INode> nodes;
    private readonly IReadOnlyList<NodeId> cachedTopologicalOrder;

    /// <summary>
    /// Feedback subgraphs: groups of nodes that form cycles via SHIFT(lag>=1)
    /// and must be evaluated bin-by-bin. Each list is in within-subgraph topo order.
    /// The key is the first node in the subgraph (topo order) — used as a trigger
    /// during series-at-a-time evaluation.
    /// </summary>
    private readonly Dictionary<NodeId, List<INode>> feedbackSubgraphs;

    /// <summary>
    /// Set of all node IDs that belong to any feedback subgraph.
    /// </summary>
    private readonly HashSet<NodeId> feedbackNodeIds;

    public Graph(IEnumerable<INode> nodes)
    {
        this.nodes = nodes.ToDictionary(n => n.Id);
        (cachedTopologicalOrder, feedbackSubgraphs, feedbackNodeIds) = ComputeTopologicalOrderWithFeedback();
    }

    public IReadOnlyDictionary<NodeId, Series> Evaluate(TimeGrid grid) =>
        EvaluateInternal(grid, null);

    public IReadOnlyDictionary<NodeId, Series> EvaluateWithOverrides(
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> overrides) =>
        EvaluateInternal(grid, overrides);

    private IReadOnlyDictionary<NodeId, Series> EvaluateInternal(
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]>? overrides)
    {
        var order = cachedTopologicalOrder;
        var memo = new Dictionary<NodeId, Series>();
        foreach (var id in order)
        {
            if (overrides != null && overrides.TryGetValue(id, out var overrideValues))
            {
                memo[id] = new Series((double[])overrideValues.Clone());
                continue;
            }

            // Skip nodes that belong to a feedback subgraph — they're evaluated
            // as a group when we hit the subgraph's trigger node.
            if (feedbackNodeIds.Contains(id) && !feedbackSubgraphs.ContainsKey(id))
                continue;

            // If this is the trigger node for a feedback subgraph, evaluate
            // the entire subgraph bin-by-bin.
            if (feedbackSubgraphs.TryGetValue(id, out var subgraph))
            {
                EvaluateFeedbackSubgraph(subgraph, grid, memo);
                continue;
            }

            var node = nodes[id];
            Series GetInput(NodeId n) => memo[n];
            memo[id] = node.Evaluate(grid, GetInput);
        }
        return memo;
    }

    /// <summary>
    /// Evaluate a feedback subgraph bin-by-bin. Each node in the subgraph
    /// gets a mutable double[] column. At each bin, nodes are evaluated in
    /// topo order (same-bin edges), reading previous bins for lagged refs.
    /// This is structurally identical to how the matrix model evaluates
    /// sequential operations.
    /// </summary>
    private void EvaluateFeedbackSubgraph(
        List<INode> subgraph,
        TimeGrid grid,
        Dictionary<NodeId, Series> memo)
    {
        var bins = grid.Bins;

        // Allocate mutable columns for each node in the subgraph
        var columns = new Dictionary<NodeId, double[]>();
        foreach (var node in subgraph)
            columns[node.Id] = new double[bins];

        // Pre-allocate overflow arrays for ServiceWithBufferNodes with WIP limits
        foreach (var node in subgraph)
        {
            if (node is ServiceWithBufferNode { HasWipLimit: true } swb)
                swb.LastOverflow = new double[bins];
        }

        // Bin-by-bin evaluation
        for (int t = 0; t < bins; t++)
        {
            foreach (var node in subgraph)
            {
                double value = EvaluateNodeAtBin(node, t, grid, columns, memo);
                columns[node.Id][t] = value;
            }
        }

        // Write results to memo as Series
        foreach (var (id, data) in columns)
            memo[id] = new Series(data);
    }

    private static double EvaluateNodeAtBin(
        INode node,
        int t,
        TimeGrid grid,
        Dictionary<NodeId, double[]> columns,
        Dictionary<NodeId, Series> memo)
    {
        // Bin-value accessor: reads from feedback columns or from already-evaluated memo
        double getBinValue(NodeId id, int bin)
        {
            if (bin < 0) return 0.0;
            if (columns.TryGetValue(id, out var col))
                return bin < col.Length ? col[bin] : 0.0;
            if (memo.TryGetValue(id, out var series))
                return bin < series.Length ? series[bin] : 0.0;
            return 0.0;
        }

        return node switch
        {
            ExprNode expr => expr.EvaluateAtBin(t, grid, getBinValue),
            ServiceWithBufferNode swb => swb.EvaluateAtBin(
                t,
                t > 0 ? columns[swb.Id][t - 1] : swb.InitialDepth,
                getBinValue),
            _ => throw new InvalidOperationException(
                $"Unsupported node type in feedback subgraph: {node.GetType().Name} ({node.Id})")
        };
    }

    public IReadOnlyList<NodeId> TopologicalOrder() => cachedTopologicalOrder;

    public IEnumerable<T> NodesOfType<T>() where T : class, INode => nodes.Values.OfType<T>();

    public INode? TryGetNode(NodeId id) => nodes.TryGetValue(id, out var node) ? node : null;

    private (IReadOnlyList<NodeId> Order, Dictionary<NodeId, List<INode>> FeedbackSubgraphs, HashSet<NodeId> FeedbackNodeIds) ComputeTopologicalOrderWithFeedback()
    {
        // Step 1: Topo sort on same-bin edges only (ExprNode.Inputs already
        // excludes lagged refs thanks to the ExpressionCompiler change).
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
        if (order.Count != nodes.Count)
            throw new InvalidOperationException("Graph has a cycle (same-bin algebraic cycle, not resolvable by SHIFT)");

        // Step 2: Detect feedback subgraphs.
        // A feedback subgraph exists when an ExprNode has lagged references
        // to nodes that come AFTER it in the topo order. The subgraph is the
        // span of nodes from the ExprNode to the last lagged target (inclusive).
        var positionOf = new Dictionary<NodeId, int>();
        for (int i = 0; i < order.Count; i++)
            positionOf[order[i]] = i;

        var feedbackSubgraphs = new Dictionary<NodeId, List<INode>>();
        var feedbackNodeIds = new HashSet<NodeId>();
        var consumed = new HashSet<NodeId>();

        foreach (var id in order)
        {
            if (consumed.Contains(id)) continue;
            if (nodes[id] is not ExprNode { HasLaggedReferences: true } exprNode) continue;

            // Find all lagged target nodes (nodes referenced via SHIFT that come after this node)
            var (_, laggedRefs) = ExpressionCompiler.FindClassifiedReferences(exprNode.ast);

            // Actually, we need to find which nodes in the graph this ExprNode
            // reads via lagged SHIFT. Those nodes must come after this node in
            // the topo order (otherwise there's no feedback — just a forward SHIFT).
            var myPos = positionOf[id];
            var maxTargetPos = myPos;

            foreach (var laggedRef in laggedRefs)
            {
                var laggedId = new NodeId(laggedRef);
                if (positionOf.TryGetValue(laggedId, out var targetPos) && targetPos > myPos)
                {
                    maxTargetPos = Math.Max(maxTargetPos, targetPos);
                }
            }

            if (maxTargetPos == myPos)
                continue; // All lagged refs are to nodes before us — no feedback, just forward SHIFT

            // The feedback subgraph is all nodes from myPos to maxTargetPos (inclusive)
            var subgraph = new List<INode>();
            for (int i = myPos; i <= maxTargetPos; i++)
            {
                subgraph.Add(nodes[order[i]]);
                feedbackNodeIds.Add(order[i]);
                consumed.Add(order[i]);
            }

            feedbackSubgraphs[id] = subgraph;
        }

        return (order, feedbackSubgraphs, feedbackNodeIds);
    }

}
