namespace FlowTime.Core.Dispatching;

public static class DispatchScheduleProcessor
{
    public static double[] ApplySchedule(int periodBins, int phaseOffset, double[] available, double[]? capacityOverride)
    {
        ArgumentNullException.ThrowIfNull(available);

        var length = available.Length;
        var result = new double[length];

        if (capacityOverride is null)
        {
            Array.Copy(available, result, length);
            return result;
        }

        for (var i = 0; i < length; i++)
        {
            var capacity = i < capacityOverride.Length ? capacityOverride[i] : 0d;
            result[i] = Math.Min(available[i], capacity);
        }

        return result;
    }
}
