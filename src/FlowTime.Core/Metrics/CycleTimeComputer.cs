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
}
