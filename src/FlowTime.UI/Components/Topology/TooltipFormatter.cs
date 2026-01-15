using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlowTime.UI.Components.Topology;

internal static class TooltipFormatter
{
    public static TooltipContent Format(string nodeId, NodeBinMetrics metrics)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node identifier is required.", nameof(nodeId));
        }

        if (metrics is null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        var lines = new List<string>();

        var kindLabel = FormatKind(metrics.NodeKind);
        var profileName = TryGetMetadataValue(metrics.Metadata, "profile.name");
        var decoratedKind = string.IsNullOrWhiteSpace(profileName)
            ? kindLabel
            : $"{kindLabel} ({profileName})";

        if (metrics.PmfProbability.HasValue || metrics.PmfValue.HasValue)
        {
            lines.Add($"Kind: {decoratedKind}");
            if (metrics.PmfProbability is double probability)
            {
                lines.Add($"Probability {probability.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            if (metrics.CustomValue.HasValue)
            {
                var valueLabel = string.IsNullOrWhiteSpace(metrics.CustomLabel) ? "Value" : metrics.CustomLabel!;
                lines.Add($"{valueLabel} {metrics.CustomValue.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
            else if (metrics.PmfValue is double pmfValue)
            {
                lines.Add($"Value {pmfValue.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }
        else
        {
            lines.Add($"Kind: {decoratedKind}");

            var nodeKind = metrics.NodeKind;
            if (IsSinkKind(nodeKind))
            {
                var scheduleLabel = metrics.SuccessRate.HasValue
                    ? $"{metrics.SuccessRate.Value * 100:F1}%"
                    : "-";
                lines.Add($"Schedule SLA {scheduleLabel}");

                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs ?? TryGetRawMetric(metrics.RawMetrics, "flowLatencyMs"))}");
            }
            else if (IsServiceWithBufferKind(nodeKind))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add($"Queue latency {FormatMinutes(metrics.LatencyMinutes)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs)}");
            }
            else if (IsQueueKind(nodeKind))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Queue latency {FormatMinutes(metrics.LatencyMinutes)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs)}");
            }
            else if (IsServiceKind(nodeKind))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs)}");
            }
            else if (IsRouterKind(nodeKind))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs)}");
            }
            else
            {
                if (!string.Equals(metrics.CustomLabel, "bin(t)", StringComparison.Ordinal))
                {
                    var label = string.IsNullOrWhiteSpace(metrics.CustomLabel) ? "Value" : metrics.CustomLabel!;
                    if (metrics.CustomValue.HasValue)
                    {
                        lines.Add($"{label} {metrics.CustomValue.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
                    }
                    else if (!string.IsNullOrWhiteSpace(metrics.CustomLabel))
                    {
                        lines.Add($"{label} -");
                    }
                }

                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add($"Flow latency {FormatLatency(metrics.FlowLatencyMs)}");
                lines.Add($"Latency {FormatMinutes(metrics.LatencyMinutes)}");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("No metrics for selected bin");
        }

        AppendRawLine(metrics.RawMetrics, lines, "exhaustedFailures", "Exhausted");
        AppendRawLine(metrics.RawMetrics, lines, "retryBudgetRemaining", "Budget remaining");
        AppendRawLine(metrics.RawMetrics, lines, "maxAttempts", "Max attempts");

        var subtitle = metrics.Timestamp.HasValue
            ? metrics.Timestamp.Value.ToUniversalTime().ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : "Latest metrics unavailable";

        return new TooltipContent(nodeId, subtitle, lines.ToArray());
    }

    private static string FormatKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "Unknown";
        }

        return kind.ToLowerInvariant() switch
        {
            "expr" or "expression" => "Expression",
            "const" or "constant" => "Const",
            "pmf" => "PMF",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kind)
        };
    }

    private static string? TryGetMetadataValue(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static void AppendRawLine(IReadOnlyDictionary<string, double?>? raw, List<string> lines, string key, string label)
    {
        if (raw is null || !raw.TryGetValue(key, out var value) || !value.HasValue)
        {
            return;
        }

        var formatted = value.Value.ToString(Math.Abs(value.Value) >= 100 ? "0" : "0.###", CultureInfo.InvariantCulture);
        lines.Add($"{label} {formatted}");
    }

    private static bool IsSinkKind(string? kind)
    {
        return string.Equals(kind, "sink", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQueueKind(string? kind)
    {
        return string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "dlq", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServiceWithBufferKind(string? kind)
    {
        return string.Equals(kind, "servicewithbuffer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServiceKind(string? kind)
    {
        return string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRouterKind(string? kind)
    {
        return string.Equals(kind, "router", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatLatency(double? latencyMs)
    {
        if (!latencyMs.HasValue)
        {
            return "-";
        }

        var flowLatency = latencyMs.Value;
        if (flowLatency >= 10_000) // beyond 10s, show minutes
        {
            var minutes = flowLatency / 1000d / 60d;
            return $"{minutes.ToString("0.0", CultureInfo.InvariantCulture)} min";
        }

        if (flowLatency >= 1000)
        {
            return $"{flowLatency.ToString("0", CultureInfo.InvariantCulture)} ms";
        }

        return $"{flowLatency.ToString("0.0", CultureInfo.InvariantCulture)} ms";
    }

    private static string FormatPercent(double? value)
    {
        return value.HasValue ? $"{value.Value * 100:F1}%" : "-";
    }

    private static string FormatPercentRounded(double? value)
    {
        return value.HasValue
            ? $"{Math.Round(value.Value * 100, MidpointRounding.AwayFromZero):0}%"
            : "-";
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? $"{Math.Round(value.Value, MidpointRounding.AwayFromZero):0}"
            : "-";
    }

    private static string FormatMinutes(double? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} min"
            : "-";
    }

    private static string FormatServiceTime(double? value)
    {
        if (!value.HasValue)
        {
            return "-";
        }

        return $"{value.Value.ToString(value.Value >= 1000 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms";
    }

    private static double? TryGetRawMetric(IReadOnlyDictionary<string, double?>? rawMetrics, string key)
    {
        if (rawMetrics is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return rawMetrics.TryGetValue(key, out var value) ? value : null;
    }
}

public sealed record TooltipContent(string Title, string Subtitle, string[] Lines);
