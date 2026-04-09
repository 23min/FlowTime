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
    private readonly string? wipOverflowTarget;
    private readonly NodeId? wipLimitSeriesId;

    public NodeId Id { get; }

    /// <summary>
    /// The NodeId of the inflow series used by this node.
    /// Exposed so that WipOverflowEvaluator can inject overflow as additional inflow.
    /// </summary>
    public NodeId InflowNodeId => inflowId;

    /// <summary>
    /// The resolved queue node ID that receives this node's WIP overflow,
    /// or null/"loss" when overflow is simply tracked/lost.
    /// </summary>
    public string? WipOverflowTarget => wipOverflowTarget;

    /// <summary>
    /// Per-bin overflow from WIP limit enforcement. Populated after Evaluate
    /// if wipLimit is set and any bin's queue exceeded the limit. Null if no
    /// wipLimit is configured (neither scalar nor series).
    /// </summary>
    public double[]? LastOverflow { get; internal set; }

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
            if (wipLimitSeriesId.HasValue)
                yield return wipLimitSeriesId.Value;
        }
    }

    public ServiceWithBufferNode(
        string id,
        NodeId inflow,
        NodeId outflow,
        NodeId? loss,
        double initialDepth,
        DispatchScheduleConfig? dispatchSchedule,
        double? wipLimit = null,
        string? wipOverflowTarget = null,
        NodeId? wipLimitSeriesId = null)
    {
        Id = new NodeId(id);
        inflowId = inflow;
        outflowId = outflow;
        lossId = loss;
        this.initialDepth = initialDepth;
        this.dispatchSchedule = dispatchSchedule;
        this.wipLimit = wipLimit;
        this.wipOverflowTarget = wipOverflowTarget;
        this.wipLimitSeriesId = wipLimitSeriesId;
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

        // Resolve the effective WIP limit: series reference takes precedence over scalar
        Series? wipLimitSeries = wipLimitSeriesId.HasValue ? getInput(wipLimitSeriesId.Value) : null;
        bool hasWipLimit = wipLimit.HasValue || wipLimitSeries is not null;
        var overflow = hasWipLimit ? new double[grid.Bins] : null;

        var result = new double[grid.Bins];
        double q = Math.Max(0, initialDepth + Safe(inflow[0]) - Safe(outflow[0]) - Safe(loss?[0]));

        if (hasWipLimit)
        {
            var limit = GetWipLimit(wipLimitSeries, 0);
            if (q > limit)
            {
                overflow![0] = q - limit;
                q = limit;
            }
        }

        result[0] = q;
        for (int t = 1; t < grid.Bins; t++)
        {
            q = Math.Max(0, q + Safe(inflow[t]) - Safe(outflow[t]) - Safe(loss?[t]));

            if (hasWipLimit)
            {
                var limit = GetWipLimit(wipLimitSeries, t);
                if (q > limit)
                {
                    overflow![t] = q - limit;
                    q = limit;
                }
            }

            result[t] = q;
        }

        LastOverflow = overflow;
        return new Series(result);
    }

    private double GetWipLimit(Series? limitSeries, int t)
    {
        if (limitSeries is not null && t < limitSeries.Length)
        {
            var v = limitSeries[t];
            return double.IsFinite(v) ? Math.Max(0, v) : 0d;
        }

        return wipLimit ?? 0d;
    }

    /// <summary>
    /// The initial queue depth seed (from topology initial condition).
    /// Exposed for per-bin evaluation in feedback subgraphs.
    /// </summary>
    public double InitialDepth => initialDepth;

    /// <summary>True if this node has a WIP limit (scalar or series).</summary>
    public bool HasWipLimit => wipLimit.HasValue || wipLimitSeriesId.HasValue;

    /// <summary>
    /// Evaluate a single bin of the queue recurrence. Used by the feedback
    /// subgraph evaluator for bin-by-bin evaluation.
    /// </summary>
    /// <param name="t">The bin index.</param>
    /// <param name="previousQueueDepth">Queue depth from the previous bin (or initial depth for t=0).</param>
    /// <param name="getBinValue">Function to read a node's value at a specific bin.</param>
    /// <returns>The queue depth at bin t (after WIP limit clamping). Overflow stored in LastOverflow if applicable.</returns>
    public double EvaluateAtBin(int t, double previousQueueDepth, Func<NodeId, int, double> getBinValue)
    {
        var inf = SafeDouble(getBinValue(inflowId, t));
        var outf = SafeDouble(getBinValue(outflowId, t));
        var loss = lossId.HasValue ? SafeDouble(getBinValue(lossId.Value, t)) : 0d;

        // Dispatch schedule gating (simplified per-bin version)
        if (dispatchSchedule is not null)
        {
            var delta = t - dispatchSchedule.PhaseOffset;
            if (delta < 0 || delta % dispatchSchedule.PeriodBins != 0)
            {
                outf = 0d;
            }
            else if (dispatchSchedule.CapacitySeriesId is NodeId capId)
            {
                outf = Math.Min(outf, SafeDouble(getBinValue(capId, t)));
            }
        }

        var q = Math.Max(0, previousQueueDepth + inf - outf - loss);

        // WIP limit (scalar or series)
        bool hasLimit = wipLimit.HasValue || wipLimitSeriesId.HasValue;
        if (hasLimit)
        {
            double limit;
            if (wipLimitSeriesId.HasValue)
            {
                var v = getBinValue(wipLimitSeriesId.Value, t);
                limit = double.IsFinite(v) ? Math.Max(0, v) : 0d;
            }
            else
            {
                limit = wipLimit ?? 0d;
            }

            if (q > limit)
            {
                if (LastOverflow is not null)
                    LastOverflow[t] = q - limit;
                q = limit;
            }
        }

        return q;
    }

    private static double SafeDouble(double v)
    {
        return double.IsFinite(v) ? v : 0d;
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
