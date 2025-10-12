namespace FlowTime.Core.Metrics;

public static class UtilizationComputer
{
    public static double? Calculate(double served, double? capacity)
    {
        if (capacity is null || capacity <= 0)
        {
            return null;
        }

        return served / capacity.Value;
    }
}
