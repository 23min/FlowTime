using FlowTime.Core.Dispatching;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Core.Nodes;

/// <summary>
/// Stateful service-with-buffer node that owns its backlog/queue depth.
/// Q[t] = max(0, Q[t-1] + inflow[t] - outflow[t] - loss[t]) with seed from topology initial condition.
/// </summary>
public sealed class ServiceWithBufferNode : INode
{
    private readonly NodeId inflowId;
    private readonly NodeId outflowId;
    private readonly NodeId? lossId;
    private readonly double initialDepth;
    private readonly DispatchScheduleConfig? dispatchSchedule;

    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs
    {
        get
        {
            yield return inflowId;
            yield return outflowId;
            if (lossId.HasValue)
                yield return lossId.Value;
            if (dispatchSchedule?.CapacitySeriesId is NodeId capacityId)
                yield return capacityId;
        }
    }

    public ServiceWithBufferNode(
        string id,
        NodeId inflow,
        NodeId outflow,
        NodeId? loss,
        double initialDepth,
        DispatchScheduleConfig? dispatchSchedule)
    {
        Id = new NodeId(id);
        inflowId = inflow;
        outflowId = outflow;
        lossId = loss;
        this.initialDepth = initialDepth;
        this.dispatchSchedule = dispatchSchedule;
    }

    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var inflow = getInput(inflowId);
        var outflow = getInput(outflowId);
        Series? scheduleCapacity = null;
        if (dispatchSchedule?.CapacitySeriesId is NodeId capacityId)
        {
            scheduleCapacity = getInput(capacityId);
        }

        if (dispatchSchedule is not null)
        {
            // Clone outflow before dispatch mutation to avoid corrupting the
            // memoized series shared with other nodes (BUG-1 fix).
            outflow = new Series(outflow.ToArray());
            DispatchScheduleProcessor.ApplySchedule(
                dispatchSchedule.PeriodBins,
                dispatchSchedule.PhaseOffset,
                outflow,
                scheduleCapacity);
        }

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
