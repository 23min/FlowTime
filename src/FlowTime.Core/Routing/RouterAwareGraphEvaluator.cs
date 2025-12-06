using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Routing;

public static class RouterAwareGraphEvaluator
{
    public static RouterEvaluationResult Evaluate(ModelDefinition model, Graph graph, TimeGrid grid)
    {
        var initialEvaluation = graph.Evaluate(grid);
        var initialContext = CopyContext(initialEvaluation);
        var overrides = RouterFlowMaterializer.ComputeOverrides(model, grid, initialContext);

        if (overrides.Count == 0)
        {
            return new RouterEvaluationResult(initialEvaluation, initialContext, false);
        }

        var reevaluated = graph.EvaluateWithOverrides(grid, overrides);
        var reevaluatedContext = CopyContext(reevaluated);
        return new RouterEvaluationResult(reevaluated, reevaluatedContext, true);
    }

    private static Dictionary<NodeId, double[]> CopyContext(IReadOnlyDictionary<NodeId, Series> evaluation)
    {
        var context = new Dictionary<NodeId, double[]>(evaluation.Count);
        foreach (var (nodeId, series) in evaluation)
        {
            context[nodeId] = series.ToArray();
        }

        return context;
    }

    public sealed record RouterEvaluationResult(
        IReadOnlyDictionary<NodeId, Series> Evaluation,
        IReadOnlyDictionary<NodeId, double[]> Context,
        bool OverridesApplied);
}
