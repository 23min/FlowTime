using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

/// <summary>
/// Immutable record capturing what analytical metrics a node can produce.
/// Resolved once per node from kind and logicalType.
/// This is the single source of truth for capability decisions —
/// the API adapter layer consumes this rather than making its own predicates.
/// </summary>
public sealed record AnalyticalCapabilities
{
    /// <summary>Whether this node can compute queue-based metrics (queueTimeMs, latencyMinutes) via Little's Law.</summary>
    public bool HasQueueSemantics { get; init; }

    /// <summary>Whether this node can compute service-based metrics (serviceTimeMs).</summary>
    public bool HasServiceSemantics { get; init; }

    /// <summary>Whether this node can decompose cycle time (queue + service → cycleTimeMs, flowEfficiency). Requires both queue and service.</summary>
    public bool HasCycleTimeDecomposition { get; init; }

    /// <summary>Whether stationarity warnings are applicable (Little's Law is in play).</summary>
    public bool StationarityWarningApplicable { get; init; }

    /// <summary>The normalized effective kind used for resolution (after logicalType override).</summary>
    public string EffectiveKind { get; init; } = "service";

    /// <summary>
    /// Resolve analytical capabilities from a node's kind and optional logicalType.
    /// LogicalType, when present and meaningful, overrides kind for capability determination.
    /// </summary>
    public static AnalyticalCapabilities Resolve(string? kind, string? logicalType = null)
    {
        var normalizedKind = NormalizeKind(kind);

        // logicalType overrides kind when it indicates a richer capability (e.g., serviceWithBuffer)
        var effectiveKind = !string.IsNullOrWhiteSpace(logicalType)
            ? NormalizeKind(logicalType)
            : normalizedKind;

        var isQueueLike = effectiveKind is "queue" or "dlq" or "servicewithbuffer";
        var isServiceLike = effectiveKind is "service" or "servicewithbuffer";

        return new AnalyticalCapabilities
        {
            HasQueueSemantics = isQueueLike,
            HasServiceSemantics = isServiceLike,
            HasCycleTimeDecomposition = isQueueLike && isServiceLike,
            StationarityWarningApplicable = isQueueLike,
            EffectiveKind = effectiveKind,
        };
    }

    /// <summary>
    /// Compute all analytical derived metrics for a single bin, gated by capabilities.
    /// Enforces finite-value safety: NaN/Infinity inputs produce null outputs.
    /// </summary>
    public AnalyticalResult ComputeBin(
        double? queueDepth,
        double? served,
        double? processingTimeMsSum,
        double? servedCount,
        double binMs)
    {
        double? queueTimeMs = null;
        double? serviceTimeMs = null;

        if (HasQueueSemantics && queueDepth.HasValue && served.HasValue
            && IsFinite(queueDepth.Value) && IsFinite(served.Value))
        {
            queueTimeMs = Sanitize(CycleTimeComputer.CalculateQueueTime(queueDepth.Value, served.Value, binMs));
        }

        if (HasServiceSemantics)
        {
            var rawProcTime = processingTimeMsSum.HasValue && IsFinite(processingTimeMsSum.Value)
                ? processingTimeMsSum
                : null;
            var rawServedCount = servedCount.HasValue && IsFinite(servedCount.Value)
                ? servedCount
                : null;
            serviceTimeMs = Sanitize(CycleTimeComputer.CalculateServiceTime(rawProcTime, rawServedCount));
        }

        var cycleTimeMs = (HasQueueSemantics || HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateCycleTime(queueTimeMs, serviceTimeMs))
            : null;

        var flowEfficiency = (HasQueueSemantics || HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs, cycleTimeMs))
            : null;

        double? latencyMinutes = null;
        if (HasQueueSemantics && queueDepth.HasValue && served.HasValue
            && IsFinite(queueDepth.Value) && IsFinite(served.Value))
        {
            var binMinutes = binMs / 60_000.0;
            latencyMinutes = Sanitize(LatencyComputer.Calculate(queueDepth.Value, served.Value, binMinutes));
        }

        return new AnalyticalResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    /// <summary>
    /// Compute analytical derived metrics for a window (multi-bin range) of node data.
    /// </summary>
    public AnalyticalWindowResult ComputeWindow(NodeData data, int startBin, int count, double binMs)
    {
        var queueTimeMs = new double?[count];
        var serviceTimeMs = new double?[count];
        var cycleTimeMs = new double?[count];
        var flowEfficiency = new double?[count];
        var latencyMinutes = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = startBin + i;
            var qd = SampleSeries(data.QueueDepth, idx);
            var sv = SampleSeries(data.Served, idx);
            var pts = SampleSeries(data.ProcessingTimeMsSum, idx);
            var sc = SampleSeries(data.ServedCount, idx);

            var bin = ComputeBin(qd, sv, pts, sc, binMs);
            queueTimeMs[i] = bin.QueueTimeMs;
            serviceTimeMs[i] = bin.ServiceTimeMs;
            cycleTimeMs[i] = bin.CycleTimeMs;
            flowEfficiency[i] = bin.FlowEfficiency;
            latencyMinutes[i] = bin.LatencyMinutes;
        }

        return new AnalyticalWindowResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    /// <summary>
    /// Compute analytical derived metrics for a single-bin class snapshot.
    /// </summary>
    public AnalyticalResult ComputeClassBin(ClassMetricsSnapshot snapshot, double binMs)
    {
        return ComputeBin(snapshot.Queue, snapshot.Served, snapshot.ProcessingTimeMsSum, snapshot.ServedCount, binMs);
    }

    /// <summary>
    /// Compute analytical derived metrics for a window of per-class data.
    /// </summary>
    public AnalyticalWindowResult ComputeClassWindow(NodeClassData classData, int startBin, int count, double binMs)
    {
        var queueTimeMs = new double?[count];
        var serviceTimeMs = new double?[count];
        var cycleTimeMs = new double?[count];
        var flowEfficiency = new double?[count];
        var latencyMinutes = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = startBin + i;
            var qd = SampleSeries(classData.QueueDepth, idx);
            var sv = SampleSeries(classData.Served, idx);
            var pts = SampleSeries(classData.ProcessingTimeMsSum, idx);
            var sc = SampleSeries(classData.ServedCount, idx);

            var bin = ComputeBin(qd, sv, pts, sc, binMs);
            queueTimeMs[i] = bin.QueueTimeMs;
            serviceTimeMs[i] = bin.ServiceTimeMs;
            cycleTimeMs[i] = bin.CycleTimeMs;
            flowEfficiency[i] = bin.FlowEfficiency;
            latencyMinutes[i] = bin.LatencyMinutes;
        }

        return new AnalyticalWindowResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    /// <summary>
    /// Returns the set of analytical derived keys this node's capabilities allow it to produce.
    /// Used to build honest seriesMetadata — only advertise what is actually emitted.
    /// </summary>
    public IReadOnlyList<string> GetAdvertisedAnalyticalKeys()
    {
        var keys = new List<string>();
        if (HasQueueSemantics)
        {
            keys.Add("queueTimeMs");
        }
        if (HasServiceSemantics)
        {
            keys.Add("serviceTimeMs");
        }
        if (HasQueueSemantics || HasServiceSemantics)
        {
            keys.Add("cycleTimeMs");
        }
        if (HasServiceSemantics) // flowEfficiency requires service time
        {
            keys.Add("flowEfficiency");
        }
        return keys;
    }

    /// <summary>
    /// Check whether arrival rates are non-stationary across a window.
    /// Returns false if this node's capabilities don't include queue semantics
    /// (Little's Law not in play), if there are too few bins, or if the node
    /// lacks the actual queue data needed to compute queueTimeMs.
    /// </summary>
    public bool CheckStationarity(double[] arrivals, double tolerance = 0.25)
    {
        if (!StationarityWarningApplicable)
        {
            return false;
        }

        return CycleTimeComputer.CheckNonStationary(arrivals, tolerance);
    }

    private static double? SampleSeries(double[]? series, int index)
    {
        if (series is null || index < 0 || index >= series.Length)
        {
            return null;
        }

        var value = series[index];
        return IsFinite(value) ? value : null;
    }

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "service";
        }

        return kind.Trim().ToLowerInvariant();
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static double? Sanitize(double? value) =>
        value.HasValue && IsFinite(value.Value) ? value : null;
}

/// <summary>
/// The result of computing analytical derived metrics for a single bin or observation.
/// All values are nullable — null means the metric is not computable for this node/bin.
/// </summary>
public sealed record AnalyticalResult
{
    public double? QueueTimeMs { get; init; }
    public double? ServiceTimeMs { get; init; }
    public double? CycleTimeMs { get; init; }
    public double? FlowEfficiency { get; init; }
    public double? LatencyMinutes { get; init; }
}

/// <summary>
/// The result of computing analytical derived metrics for a window (multi-bin range).
/// Each array has one entry per bin in the window.
/// </summary>
public sealed record AnalyticalWindowResult
{
    public required double?[] QueueTimeMs { get; init; }
    public required double?[] ServiceTimeMs { get; init; }
    public required double?[] CycleTimeMs { get; init; }
    public required double?[] FlowEfficiency { get; init; }
    public required double?[] LatencyMinutes { get; init; }
}
