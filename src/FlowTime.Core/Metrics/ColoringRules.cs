namespace FlowTime.Core.Metrics;

public static class ColoringRules
{
    public static string PickServiceColor(double? utilization)
    {
        if (utilization is null)
        {
            return "gray";
        }

        var value = utilization.Value;

        if (value < 0.7)
        {
            return "green";
        }

        if (value < 0.9)
        {
            return "yellow";
        }

        return "red";
    }

    public static string PickQueueColor(double? latencyMinutes, double? slaMinutes)
    {
        if (latencyMinutes is null || slaMinutes is null || slaMinutes <= 0)
        {
            return "gray";
        }

        var latency = latencyMinutes.Value;
        var sla = slaMinutes.Value;

        if (latency <= sla)
        {
            return "green";
        }

        if (latency <= sla * 1.5)
        {
            return "yellow";
        }

        return "red";
    }
}
