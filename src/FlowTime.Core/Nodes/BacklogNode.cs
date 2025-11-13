using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Core.Nodes;

/// <summary>
/// Stateful backlog/queue depth node.
/// Q[t] = max(0, Q[t-1] + inflow[t] - outflow[t] - loss[t]) with seed from topology initial condition.
/// </summary>
public sealed class BacklogNode : INode
{
    private readonly NodeId inflowId;
    private readonly NodeId outflowId;
    private readonly NodeId? lossId;
    private readonly double initialDepth;

    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs => lossId.HasValue
        ? new[] { inflowId, outflowId, lossId.Value }
        : new[] { inflowId, outflowId };

    public BacklogNode(string id, NodeId inflow, NodeId outflow, NodeId? loss, double initialDepth)
    {
        Id = new NodeId(id);
        inflowId = inflow;
        outflowId = outflow;
        lossId = loss;
        this.initialDepth = initialDepth;
    }

    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var inflow = getInput(inflowId);
        var outflow = getInput(outflowId);
        var loss = lossId.HasValue ? getInput(lossId.Value) : null;

        var result = new double[grid.Bins];
        double q = Math.Max(0, initialDepth + Safe(inflow[0]) - Safe(outflow[0]) - Safe(loss?[0]));
        result[0] = q;
        for (int t = 1; t < grid.Bins; t++)
        {
            q = Math.Max(0, q + Safe(inflow[t]) - Safe(outflow[t]) - Safe(loss?[t]));
            result[t] = q;
        }
        return new Series(result);
    }

    private static double Safe(double? v)
    {
        if (!v.HasValue)
        {
            return 0d;
        }

        return double.IsFinite(v.Value) ? v.Value : 0d;
    }
}
