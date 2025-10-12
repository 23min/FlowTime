namespace FlowTime.Core.Metrics;

public static class LatencyComputer
{
    public static double? Calculate(double queue, double served, double binMinutes)
    {
        if (served <= 0 || binMinutes <= 0)
        {
            return null;
        }

        return (queue / served) * binMinutes;
    }
}
