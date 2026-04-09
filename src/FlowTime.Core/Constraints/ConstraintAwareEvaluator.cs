using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Routing;

namespace FlowTime.Core.Constraints;

/// <summary>
/// Composes Graph evaluation with constraint enforcement.
///
/// Pipeline:
///   1. RouterAwareGraphEvaluator.Evaluate → unconstrained (but router-adjusted) series.
///   2. For each topology constraint whose total demand exceeds capacity,
///      allocate proportionally via ConstraintAllocator and cap each assigned
///      node's served series.
///   3. Graph.EvaluateWithOverrides with the merged overrides (router + constraint)
///      so downstream nodes see the capped values.
///
/// The Graph itself stays pure — it knows nothing about constraints. This layer
/// composes Graph + ConstraintAllocator + EvaluateWithOverrides.
/// </summary>
public static class ConstraintAwareEvaluator
{
    public static ConstraintEvaluationResult Evaluate(
        ModelDefinition model,
        Graph graph,
        TimeGrid grid,
        Topology topology)
    {
        // Step 1: evaluate with router awareness (unconstrained)
        var routerResult = RouterAwareGraphEvaluator.Evaluate(model, graph, grid);
        var evaluation = routerResult.Evaluation;

        var emptyAllocations = new Dictionary<string, ConstraintAllocation>(StringComparer.OrdinalIgnoreCase);

        if (topology.Constraints.Count == 0)
        {
            return new ConstraintEvaluationResult(evaluation, routerResult.Context, ConstraintsApplied: false, Allocations: emptyAllocations);
        }

        // Step 2: compute constraint overrides and allocation metadata
        var (constraintOverrides, allocations) = ComputeConstraintOverrides(evaluation, topology, grid.Bins);

        if (constraintOverrides.Count == 0)
        {
            return new ConstraintEvaluationResult(evaluation, routerResult.Context, ConstraintsApplied: false, Allocations: allocations);
        }

        // Step 3: merge router overrides (if any) with constraint overrides.
        // Constraint overrides take precedence — a constrained node's served
        // gets capped regardless of whether the router also adjusted it.
        var mergedOverrides = new Dictionary<NodeId, double[]>();

        if (routerResult.OverridesApplied)
        {
            // Re-extract the router overrides by comparing routerResult.Evaluation
            // against an unconstrained evaluation. However, the router overrides
            // are already baked into routerResult.Evaluation. We just need to
            // layer constraint overrides on top.
            //
            // EvaluateWithOverrides will re-evaluate every node in topological
            // order, using override values where provided and normal evaluation
            // otherwise. So we just need to provide the constraint overrides —
            // the re-evaluation will naturally pick up the same router adjustments
            // as long as we also include the router overrides in the merged set.
            //
            // Simplification: re-compute router overrides from scratch so we have
            // the full override set. This is the same as what RouterAwareGraphEvaluator
            // does internally.
            var initialEvaluation = graph.Evaluate(grid);
            var initialContext = CopyContext(initialEvaluation);
            var routerOverrides = RouterFlowMaterializer.ComputeOverrides(model, grid, initialContext);

            foreach (var (nodeId, values) in routerOverrides)
            {
                mergedOverrides[nodeId] = values;
            }
        }

        foreach (var (nodeId, values) in constraintOverrides)
        {
            mergedOverrides[nodeId] = values; // constraint overrides take precedence
        }

        // Step 4: re-evaluate with merged overrides
        var constrained = graph.EvaluateWithOverrides(grid, mergedOverrides);
        var constrainedContext = CopyContext(constrained);

        return new ConstraintEvaluationResult(constrained, constrainedContext, ConstraintsApplied: true, Allocations: allocations);
    }

    private static (Dictionary<NodeId, double[]> Overrides, IReadOnlyDictionary<string, ConstraintAllocation> Allocations) ComputeConstraintOverrides(
        IReadOnlyDictionary<NodeId, Series> evaluation,
        Topology topology,
        int bins)
    {
        var overrides = new Dictionary<NodeId, double[]>();
        var allAllocations = new Dictionary<string, ConstraintAllocation>(StringComparer.OrdinalIgnoreCase);

        // Build constraint assignments: constraintId → list of topology node IDs
        var assignments = new Dictionary<string, List<(string TopologyNodeId, CompiledSeriesReference ArrivalsRef, CompiledSeriesReference ServedRef)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var node in topology.Nodes)
        {
            if (node.Constraints is null || node.Constraints.Count == 0) continue;

            foreach (var constraintId in node.Constraints)
            {
                if (string.IsNullOrWhiteSpace(constraintId)) continue;

                if (!assignments.TryGetValue(constraintId, out var list))
                {
                    list = new List<(string, CompiledSeriesReference, CompiledSeriesReference)>();
                    assignments[constraintId] = list;
                }

                list.Add((node.Id, node.Semantics.Arrivals, node.Semantics.Served));
            }
        }

        // For each constraint, compute allocations and build overrides if needed
        foreach (var constraint in topology.Constraints)
        {
            if (!assignments.TryGetValue(constraint.Id, out var assignedNodes))
                continue;

            // Get the capacity series (the constraint's Served reference)
            var capacityNodeId = new NodeId(constraint.Semantics.Served.LookupKey);
            if (!evaluation.TryGetValue(capacityNodeId, out var capacitySeries))
                continue;

            // Initialize per-node allocation arrays
            var nodeAllocations = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            var limited = new bool[bins];
            foreach (var (topoNodeId, _, _) in assignedNodes)
            {
                nodeAllocations[topoNodeId] = new double[bins];
            }

            for (int t = 0; t < bins; t++)
            {
                var capacity = t < capacitySeries.Length ? capacitySeries[t] : 0.0;
                if (!double.IsFinite(capacity) || capacity < 0) capacity = 0.0;

                // Collect demands from each assigned node's arrivals series
                var demands = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                double totalDemand = 0;

                foreach (var (topoNodeId, arrivalsRef, _) in assignedNodes)
                {
                    var arrivalsNodeId = new NodeId(arrivalsRef.LookupKey);
                    double demand = 0;
                    if (evaluation.TryGetValue(arrivalsNodeId, out var arrivalsSeries) && t < arrivalsSeries.Length)
                    {
                        demand = arrivalsSeries[t];
                        if (!double.IsFinite(demand) || demand < 0) demand = 0;
                    }
                    demands[topoNodeId] = demand;
                    totalDemand += demand;
                }

                // Always compute allocation (even when demand <= capacity, for metadata completeness)
                var allocations = ConstraintAllocator.AllocateProportional(demands, capacity);

                foreach (var (topoNodeId, _, _) in assignedNodes)
                {
                    nodeAllocations[topoNodeId][t] = allocations.TryGetValue(topoNodeId, out var a) ? a : 0.0;
                }

                // Only enforce (cap) if demand exceeds capacity
                if (totalDemand <= capacity) continue;

                limited[t] = true;

                foreach (var (topoNodeId, _, servedRef) in assignedNodes)
                {
                    var servedNodeId = new NodeId(servedRef.LookupKey);
                    if (!evaluation.TryGetValue(servedNodeId, out var servedSeries))
                        continue;

                    var allocation = allocations.TryGetValue(topoNodeId, out var alloc) ? alloc : 0.0;
                    var currentServed = t < servedSeries.Length ? servedSeries[t] : 0.0;

                    if (currentServed > allocation)
                    {
                        if (!overrides.TryGetValue(servedNodeId, out var overrideValues))
                        {
                            overrideValues = servedSeries.ToArray();
                            overrides[servedNodeId] = overrideValues;
                        }

                        overrideValues[t] = allocation;
                    }
                }
            }

            allAllocations[constraint.Id] = new ConstraintAllocation(nodeAllocations, limited);
        }

        return (overrides, allAllocations);
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

    public sealed record ConstraintEvaluationResult(
        IReadOnlyDictionary<NodeId, Series> Evaluation,
        IReadOnlyDictionary<NodeId, double[]> Context,
        bool ConstraintsApplied,
        IReadOnlyDictionary<string, ConstraintAllocation> Allocations);

    /// <summary>
    /// Per-constraint allocation result produced during evaluation.
    /// Contains the per-node, per-bin allocation and a per-bin flag
    /// indicating whether the constraint was limiting (demand > capacity).
    /// </summary>
    public sealed record ConstraintAllocation(
        IReadOnlyDictionary<string, double[]> AllocationByNode,
        bool[] Limited);
}
