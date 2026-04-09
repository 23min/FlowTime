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
    private readonly double? wipLimit;

    public NodeId Id { get; }

    /// <summary>
    /// Per-bin overflow from WIP limit enforcement. Populated after Evaluate
    /// if wipLimit is set and any bin's queue exceeded the limit. Null if no
    /// wipLimit is configured.
    /// </summary>
    public double[]? LastOverflow { get; private set; }

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
        DispatchScheduleConfig? dispatchSchedule,
        double? wipLimit = null)
    {
        Id = new NodeId(id);
        inflowId = inflow;
        outflowId = outflow;
        lossId = loss;
        this.initialDepth = initialDepth;
        this.dispatchSchedule = dispatchSchedule;
        this.wipLimit = wipLimit;
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
            // Series is immutable — get a mutable copy for dispatch schedule application.
            var outflowData = outflow.ToArray();
            DispatchScheduleProcessor.ApplySchedule(
                dispatchSchedule.PeriodBins,
                dispatchSchedule.PhaseOffset,
                outflowData,
                scheduleCapacity?.ToArray());
            outflow = new Series(outflowData);
        }

        var loss = lossId.HasValue ? getInput(lossId.Value) : null;
        var overflow = wipLimit.HasValue ? new double[grid.Bins] : null;

        var result = new double[grid.Bins];
        double q = Math.Max(0, initialDepth + Safe(inflow[0]) - Safe(outflow[0]) - Safe(loss?[0]));

        if (wipLimit.HasValue && q > wipLimit.Value)
        {
            overflow![0] = q - wipLimit.Value;
            q = wipLimit.Value;
        }

        result[0] = q;
        for (int t = 1; t < grid.Bins; t++)
        {
            q = Math.Max(0, q + Safe(inflow[t]) - Safe(outflow[t]) - Safe(loss?[t]));

            if (wipLimit.HasValue && q > wipLimit.Value)
            {
                overflow![t] = q - wipLimit.Value;
                q = wipLimit.Value;
            }

            result[t] = q;
        }

        LastOverflow = overflow;
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
