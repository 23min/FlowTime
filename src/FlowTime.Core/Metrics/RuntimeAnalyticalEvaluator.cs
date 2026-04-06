using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public static class RuntimeAnalyticalEvaluator
{
    public static AnalyticalResult ComputeBin(
        RuntimeAnalyticalDescriptor descriptor,
        double? queueDepth,
        double? served,
        double? processingTimeMsSum,
        double? servedCount,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        double? queueTimeMs = null;
        double? serviceTimeMs = null;

        if (descriptor.HasQueueSemantics && queueDepth.HasValue && served.HasValue
            && IsFinite(queueDepth.Value) && IsFinite(served.Value))
        {
            queueTimeMs = Sanitize(CycleTimeComputer.CalculateQueueTime(queueDepth.Value, served.Value, binMs));
        }

        if (descriptor.HasServiceSemantics)
        {
            var rawProcTime = processingTimeMsSum.HasValue && IsFinite(processingTimeMsSum.Value)
                ? processingTimeMsSum
                : null;
            var rawServedCount = servedCount.HasValue && IsFinite(servedCount.Value)
                ? servedCount
                : null;
            serviceTimeMs = Sanitize(CycleTimeComputer.CalculateServiceTime(rawProcTime, rawServedCount));
        }

        var cycleTimeMs = (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateCycleTime(queueTimeMs, serviceTimeMs))
            : null;

        var flowEfficiency = (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs, cycleTimeMs))
            : null;

        double? latencyMinutes = null;
        if (descriptor.HasQueueSemantics && queueDepth.HasValue && served.HasValue
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

    public static AnalyticalWindowResult ComputeWindow(
        RuntimeAnalyticalDescriptor descriptor,
        NodeData data,
        int startBin,
        int count,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(data);

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

            var bin = ComputeBin(descriptor, qd, sv, pts, sc, binMs);
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

    public static AnalyticalResult ComputeClassBin(
        RuntimeAnalyticalDescriptor descriptor,
        ClassMetricsSnapshot snapshot,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return ComputeBin(descriptor, snapshot.Queue, snapshot.Served, snapshot.ProcessingTimeMsSum, snapshot.ServedCount, binMs);
    }

    public static AnalyticalWindowResult ComputeClassWindow(
        RuntimeAnalyticalDescriptor descriptor,
        NodeClassData classData,
        int startBin,
        int count,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(classData);

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

            var bin = ComputeBin(descriptor, qd, sv, pts, sc, binMs);
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

    public static IReadOnlyList<string> GetAdvertisedAnalyticalKeys(RuntimeAnalyticalDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var keys = new List<string>();
        if (descriptor.HasQueueSemantics)
        {
            keys.Add("queueTimeMs");
        }

        if (descriptor.HasServiceSemantics)
        {
            keys.Add("serviceTimeMs");
        }

        if (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
        {
            keys.Add("cycleTimeMs");
        }

        if (descriptor.HasServiceSemantics)
        {
            keys.Add("flowEfficiency");
        }

        return keys;
    }

    public static bool CheckStationarity(
        RuntimeAnalyticalDescriptor descriptor,
        double[] arrivals,
        double tolerance = 0.25)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(arrivals);

        if (!descriptor.StationarityWarningApplicable)
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

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static double? Sanitize(double? value) =>
        value.HasValue && IsFinite(value.Value) ? value : null;
}

public sealed record AnalyticalResult
{
    public double? QueueTimeMs { get; init; }
    public double? ServiceTimeMs { get; init; }
    public double? CycleTimeMs { get; init; }
    public double? FlowEfficiency { get; init; }
    public double? LatencyMinutes { get; init; }
}

public sealed record AnalyticalWindowResult
{
    public required double?[] QueueTimeMs { get; init; }
    public required double?[] ServiceTimeMs { get; init; }
    public required double?[] CycleTimeMs { get; init; }
    public required double?[] FlowEfficiency { get; init; }
    public required double?[] LatencyMinutes { get; init; }
}