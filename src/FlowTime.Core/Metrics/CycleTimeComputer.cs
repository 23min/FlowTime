namespace FlowTime.Core.Metrics;

public static class CycleTimeComputer
{
    public static double? CalculateQueueTime(double queueDepth, double served, double binMs)
    {
        if (served <= 0 || binMs <= 0)
        {
            return null;
        }

        return (queueDepth / served) * binMs;
    }

    public static double? CalculateServiceTime(double? processingTimeMsSum, double? servedCount)
    {
        if (processingTimeMsSum is null || servedCount is null || servedCount <= 0)
        {
            return null;
        }

        return processingTimeMsSum.Value / servedCount.Value;
    }

    public static double? CalculateCycleTime(double? queueTimeMs, double? serviceTimeMs)
    {
        if (queueTimeMs is null && serviceTimeMs is null)
        {
            return null;
        }

        return (queueTimeMs ?? 0) + (serviceTimeMs ?? 0);
    }

    public static double? CalculateFlowEfficiency(double? serviceTimeMs, double? cycleTimeMs)
    {
        if (serviceTimeMs is null || cycleTimeMs is null || cycleTimeMs <= 0)
        {
            return null;
        }

        return serviceTimeMs.Value / cycleTimeMs.Value;
    }

    /// <summary>
    /// Checks whether arrival rates are non-stationary across a window,
    /// which makes Little's Law (Q/λ × binMs) potentially unreliable.
    /// Compares the average arrival rate in the first half of the window
    /// to the second half. Returns true if divergence exceeds tolerance.
    /// </summary>
    public static bool CheckNonStationary(double[] arrivals, double tolerance = 0.25)
    {
        if (arrivals.Length < 2)
        {
            return false;
        }

        var mid = arrivals.Length / 2;

        double sumFirst = 0;
        for (var i = 0; i < mid; i++)
        {
            sumFirst += arrivals[i];
        }

        double sumSecond = 0;
        for (var i = mid; i < arrivals.Length; i++)
        {
            sumSecond += arrivals[i];
        }

        var avgFirst = sumFirst / mid;
        var avgSecond = sumSecond / (arrivals.Length - mid);

        var baseline = Math.Max(avgFirst, avgSecond);
        if (baseline <= 0)
        {
            return false;
        }

        var divergence = Math.Abs(avgFirst - avgSecond) / baseline;
        return divergence > tolerance;
    }
}
