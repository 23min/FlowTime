namespace FlowTime.Core.Dispatching;

public static class DispatchScheduleProcessor
{
    public static double[] ApplySchedule(int periodBins, int phaseOffset, double[] target, double[]? capacityOverride)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (periodBins <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodBins));
        }

        var normalizedPhase = NormalizePhase(phaseOffset, periodBins);
        for (var i = 0; i < target.Length; i++)
        {
            if (!IsDispatchBin(i, periodBins, normalizedPhase))
            {
                target[i] = 0d;
                continue;
            }

            var allowed = capacityOverride is not null && i < capacityOverride.Length
                ? capacityOverride[i]
                : double.PositiveInfinity;

            if (double.IsFinite(allowed))
            {
                target[i] = Math.Min(target[i], allowed);
            }
        }

        return target;
    }

    public static bool IsDispatchBin(int binIndex, int periodBins, int normalizedPhase)
    {
        if (periodBins <= 0)
        {
            return true;
        }

        if (binIndex < normalizedPhase)
        {
            return false;
        }

        return ((binIndex - normalizedPhase) % periodBins) == 0;
    }

    public static int NormalizePhase(int phaseOffset, int periodBins)
    {
        if (periodBins <= 0)
        {
            return 0;
        }

        var normalized = phaseOffset % periodBins;
        return normalized < 0 ? normalized + periodBins : normalized;
    }
}
