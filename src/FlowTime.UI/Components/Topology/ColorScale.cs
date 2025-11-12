using System;

namespace FlowTime.UI.Components.Topology;

internal static class ColorScale
{
    public const string SuccessColor = "#009E73";   // Okabe-Ito green
    public const string WarningColor = "#E69F00";   // Okabe-Ito orange
    public const string ErrorColor = "#D55E00";     // Okabe-Ito vermillion
    public const string NeutralColor = "#E2E8F0";   // Leaf neutral fill
    public const string FocusStrokeColor = "#262626";

    private const double DefaultSlaWarningWindow = 0.15;
    private const double DefaultUtilizationCriticalOffset = 0.05;
    private const double DefaultErrorWarningRatio = 0.4;
    private const double DefaultServiceTimeWarningMs = 400;
    private const double DefaultServiceTimeCriticalMs = 700;

    public static string GetFill(NodeBinMetrics metrics) => GetFill(metrics, TopologyColorBasis.Sla, ColorThresholds.Default);

    public static string GetFill(NodeBinMetrics metrics, TopologyColorBasis basis) =>
        GetFill(metrics, basis, ColorThresholds.Default);

    public static string GetFill(NodeBinMetrics metrics, TopologyColorBasis basis, ColorThresholds thresholds)
    {
        if (metrics is null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        return basis switch
        {
            TopologyColorBasis.Utilization => EvaluateUtilization(metrics.Utilization, thresholds),
            TopologyColorBasis.Errors => EvaluateErrorRate(metrics.ErrorRate, thresholds),
            TopologyColorBasis.Queue => EvaluateQueue(metrics.QueueDepth),
            TopologyColorBasis.ServiceTime => EvaluateServiceTime(metrics.ServiceTimeMs, thresholds),
            _ => EvaluateSla(metrics.SuccessRate, metrics.Utilization, metrics.ErrorRate, thresholds)
        };
    }

    public static string GetStroke(NodeBinMetrics metrics)
    {
        _ = metrics ?? throw new ArgumentNullException(nameof(metrics));
        return FocusStrokeColor;
    }

    private static string EvaluateSla(double? successRate, double? utilization, double? errorRate, ColorThresholds thresholds)
    {
        if (!successRate.HasValue && !utilization.HasValue && !errorRate.HasValue)
        {
            return NeutralColor;
        }

        if (errorRate.HasValue && errorRate.Value >= thresholds.ErrorCritical)
        {
            return ErrorColor;
        }

        if (successRate.HasValue)
        {
            if (successRate.Value >= thresholds.SlaSuccess)
            {
                if (utilization.HasValue && utilization.Value >= thresholds.UtilizationWarning)
                {
                    return WarningColor;
                }

                return SuccessColor;
            }

            if (successRate.Value >= thresholds.SlaWarning)
            {
                return WarningColor;
            }

            return ErrorColor;
        }

        if (utilization.HasValue)
        {
            return utilization.Value >= thresholds.UtilizationWarning ? WarningColor : SuccessColor;
        }

        return NeutralColor;
    }

    private static string EvaluateUtilization(double? utilization, ColorThresholds thresholds)
    {
        if (!utilization.HasValue)
        {
            return NeutralColor;
        }

        if (utilization.Value >= thresholds.UtilizationCritical)
        {
            return ErrorColor;
        }

        if (utilization.Value >= thresholds.UtilizationWarning)
        {
            return WarningColor;
        }

        return SuccessColor;
    }

    private static string EvaluateServiceTime(double? serviceTimeMs, ColorThresholds thresholds)
    {
        if (!serviceTimeMs.HasValue)
        {
            return NeutralColor;
        }

        if (serviceTimeMs.Value >= thresholds.ServiceTimeCritical)
        {
            return ErrorColor;
        }

        if (serviceTimeMs.Value >= thresholds.ServiceTimeWarning)
        {
            return WarningColor;
        }

        return SuccessColor;
    }

    private static string EvaluateErrorRate(double? errorRate, ColorThresholds thresholds)
    {
        if (!errorRate.HasValue)
        {
            return NeutralColor;
        }

        if (errorRate.Value >= thresholds.ErrorCritical)
        {
            return ErrorColor;
        }

        if (errorRate.Value >= thresholds.ErrorWarning)
        {
            return WarningColor;
        }

        return SuccessColor;
    }

    private static string EvaluateQueue(double? queueDepth)
    {
        if (!queueDepth.HasValue)
        {
            return NeutralColor;
        }

        if (queueDepth.Value >= 0.8)
        {
            return ErrorColor;
        }

        if (queueDepth.Value >= 0.4)
        {
            return WarningColor;
        }

        return SuccessColor;
    }

    internal readonly struct ColorThresholds
    {
        public static ColorThresholds Default => new(
            slaSuccess: 0.95,
            slaWarning: 0.80,
            utilizationWarning: 0.90,
            utilizationCritical: 0.95,
            errorWarning: 0.02,
            errorCritical: 0.05,
            serviceTimeWarning: DefaultServiceTimeWarningMs,
            serviceTimeCritical: DefaultServiceTimeCriticalMs);

        public ColorThresholds(
            double slaSuccess,
            double slaWarning,
            double utilizationWarning,
            double utilizationCritical,
            double errorWarning,
            double errorCritical,
            double serviceTimeWarning,
            double serviceTimeCritical)
        {
            SlaSuccess = slaSuccess;
            SlaWarning = slaWarning;
            UtilizationWarning = utilizationWarning;
            UtilizationCritical = utilizationCritical;
            ErrorWarning = errorWarning;
            ErrorCritical = errorCritical;
            ServiceTimeWarning = serviceTimeWarning;
            ServiceTimeCritical = serviceTimeCritical;
        }

        public double SlaSuccess { get; }
        public double SlaWarning { get; }
        public double UtilizationWarning { get; }
        public double UtilizationCritical { get; }
        public double ErrorWarning { get; }
        public double ErrorCritical { get; }
        public double ServiceTimeWarning { get; }
        public double ServiceTimeCritical { get; }

        public static ColorThresholds FromOverlay(TopologyOverlaySettings settings)
        {
            var slaSuccess = Clamp01(settings.SlaWarningThreshold);
            var slaWarning = Math.Max(0, slaSuccess - DefaultSlaWarningWindow);

            var utilWarn = Clamp01(settings.UtilizationWarningThreshold);
            var utilCritical = Clamp01(Math.Max(utilWarn, utilWarn + DefaultUtilizationCriticalOffset));

            var errorCritical = Math.Clamp(settings.ErrorRateAlertThreshold, 0.0001, 1);
            var errorWarning = Math.Min(errorCritical * DefaultErrorWarningRatio, errorCritical);

            var serviceTimeWarning = settings.ServiceTimeWarningThresholdMs > 0
                ? settings.ServiceTimeWarningThresholdMs
                : DefaultServiceTimeWarningMs;
            var serviceTimeCriticalCandidate = settings.ServiceTimeCriticalThresholdMs > 0
                ? settings.ServiceTimeCriticalThresholdMs
                : DefaultServiceTimeCriticalMs;
            var serviceTimeCritical = Math.Max(serviceTimeWarning, serviceTimeCriticalCandidate);

            return new ColorThresholds(
                slaSuccess,
                slaWarning,
                utilWarn,
                utilCritical,
                errorWarning,
                errorCritical,
                serviceTimeWarning,
                serviceTimeCritical);
        }

        private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
    }
}

public sealed record NodeBinMetrics(
    double? SuccessRate,
    double? Utilization,
    double? ErrorRate,
    double? QueueDepth,
    double? LatencyMinutes,
    DateTimeOffset? Timestamp,
    double? CustomValue = null,
    string? CustomLabel = null,
    double? PmfProbability = null,
    double? PmfValue = null,
    string? NodeKind = null,
    double? ServiceTimeMs = null,
    double? RetryTax = null,
    IReadOnlyDictionary<string, double?>? RawMetrics = null);
