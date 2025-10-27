using System;

namespace FlowTime.UI.Components.Topology;

internal static class ColorScale
{
    public const string SuccessColor = "#009E73";   // Okabe-Ito green
    public const string WarningColor = "#E69F00";   // Okabe-Ito orange
    public const string ErrorColor = "#D55E00";     // Okabe-Ito vermillion
    public const string NeutralColor = "#7A7A7A";   // Neutral gray
    public const string FocusStrokeColor = "#262626";

    private const double SuccessThreshold = 0.95;
    private const double WarningThreshold = 0.80;
    private const double UtilizationWarningThreshold = 0.90;
    private const double ErrorRateCritical = 0.05;

    public static string GetFill(NodeBinMetrics metrics)
    {
        if (metrics is null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        var hasAnyData = metrics.SuccessRate.HasValue || metrics.Utilization.HasValue ||
                         metrics.ErrorRate.HasValue || metrics.QueueDepth.HasValue ||
                         metrics.LatencyMinutes.HasValue;

        if (!hasAnyData)
        {
            return NeutralColor;
        }

        if (metrics.ErrorRate is double errorRate && errorRate >= ErrorRateCritical)
        {
            return ErrorColor;
        }

        if (metrics.SuccessRate is double successRate)
        {
            if (successRate >= SuccessThreshold)
            {
                if (metrics.Utilization is double utilization && utilization >= UtilizationWarningThreshold)
                {
                    return WarningColor;
                }

                return SuccessColor;
            }

            if (successRate >= WarningThreshold)
            {
                return WarningColor;
            }

            return ErrorColor;
        }

        if (metrics.Utilization is double utilValue)
        {
            if (utilValue >= UtilizationWarningThreshold)
            {
                return WarningColor;
            }

            return SuccessColor;
        }

        return NeutralColor;
    }

    public static string GetStroke(NodeBinMetrics metrics)
    {
        _ = metrics ?? throw new ArgumentNullException(nameof(metrics));
        return FocusStrokeColor;
    }
}

public sealed record NodeBinMetrics(
    double? SuccessRate,
    double? Utilization,
    double? ErrorRate,
    double? QueueDepth,
    double? LatencyMinutes,
    DateTimeOffset? Timestamp);
