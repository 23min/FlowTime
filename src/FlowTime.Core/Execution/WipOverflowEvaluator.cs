using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Execution;

/// <summary>
/// Post-evaluation pass that routes WIP overflow from source queue nodes
/// to target queue nodes. Handles cascading (A→B→C) by iterating until
/// the overflow overrides converge.
///
/// Pipeline:
///   1. Graph.Evaluate → unconstrained series with WIP clamping.
///   2. For each ServiceWithBufferNode with a WipOverflowTarget,
///      read LastOverflow and inject it as additional inflow to the
///      target queue via Graph.EvaluateWithOverrides.
///   3. Iterate until overflow overrides stabilize (cascading convergence).
/// </summary>
public static class WipOverflowEvaluator
{
    private const int MaxIterations = 10;

    public static IReadOnlyDictionary<NodeId, Series> Evaluate(Graph graph, TimeGrid grid)
    {
        var overflowNodes = graph.NodesOfType<ServiceWithBufferNode>()
            .Where(n => IsRoutedOverflow(n.WipOverflowTarget))
            .ToList();

        if (overflowNodes.Count == 0)
        {
            return graph.Evaluate(grid);
        }

        // Initial evaluation — overflow nodes start with zero overflow routing
        var baseEvaluation = graph.Evaluate(grid);

        // Build overflow overrides iteratively until convergence
        var overrides = new Dictionary<NodeId, double[]>();

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            var evaluation = overrides.Count == 0
                ? baseEvaluation
                : graph.EvaluateWithOverrides(grid, overrides);

            var newOverrides = BuildOverflowOverrides(graph, overflowNodes, baseEvaluation);

            if (OverridesEqual(overrides, newOverrides))
            {
                return evaluation;
            }

            overrides = newOverrides;
        }

        // Final evaluation with converged overrides
        return graph.EvaluateWithOverrides(grid, overrides);
    }

    private static Dictionary<NodeId, double[]> BuildOverflowOverrides(
        Graph graph,
        List<ServiceWithBufferNode> overflowNodes,
        IReadOnlyDictionary<NodeId, Series> baseEvaluation)
    {
        var overrides = new Dictionary<NodeId, double[]>();

        foreach (var source in overflowNodes)
        {
            if (source.LastOverflow is not { } overflow)
                continue;

            var targetId = new NodeId(source.WipOverflowTarget!);
            if (graph.TryGetNode(targetId) is not ServiceWithBufferNode target)
                continue;

            var inflowId = target.InflowNodeId;

            if (!overrides.TryGetValue(inflowId, out var combined))
            {
                // Start from original inflow values
                if (!baseEvaluation.TryGetValue(inflowId, out var originalSeries))
                    continue;

                combined = originalSeries.ToArray();
                overrides[inflowId] = combined;
            }

            // Add overflow to the target's inflow
            var len = Math.Min(combined.Length, overflow.Length);
            for (int t = 0; t < len; t++)
            {
                combined[t] += overflow[t];
            }
        }

        return overrides;
    }

    private static bool OverridesEqual(
        Dictionary<NodeId, double[]> a,
        Dictionary<NodeId, double[]> b)
    {
        if (a.Count != b.Count) return false;

        foreach (var (key, aValues) in a)
        {
            if (!b.TryGetValue(key, out var bValues)) return false;
            if (aValues.Length != bValues.Length) return false;

            for (int i = 0; i < aValues.Length; i++)
            {
                if (aValues[i] != bValues[i]) return false;
            }
        }

        return true;
    }

    private static bool IsRoutedOverflow(string? target)
    {
        return !string.IsNullOrWhiteSpace(target)
            && !string.Equals(target, "loss", StringComparison.OrdinalIgnoreCase);
    }
}
