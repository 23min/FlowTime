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

        var kindLabel = FormatKind(ResolveKindLabel(metrics));
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

            if (HasCategory(metrics, "dependency"))
            {
                lines.Add($"Arrivals {FormatNumber(TryGetRawMetric(metrics.RawMetrics, "arrivals"))}");
                lines.Add($"Served {FormatNumber(TryGetRawMetric(metrics.RawMetrics, "served"))}");
                lines.Add($"Errors {FormatNumber(TryGetRawMetric(metrics.RawMetrics, "errors"))}");
            }
            else if (HasCategory(metrics, "sink"))
            {
                var hasSchedule = HasRawMetric(metrics.RawMetrics, "scheduleAdherence");
                var slaLabel = FormatPercent(metrics.SuccessRate);
                lines.Add($"{(hasSchedule ? "Schedule SLA" : "SLA")} {slaLabel}");
                if (hasSchedule)
                {
                    AppendCompletionSlaLine(metrics, lines);
                }

                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add(FormatFlowLatencyLine(metrics));
            }
            else if (HasIdentity(metrics, "serviceWithBuffer"))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                AppendCompletionSlaLine(metrics, lines);
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add($"Queue latency {FormatMinutes(metrics.LatencyMinutes)}");
                lines.Add(FormatFlowLatencyLine(metrics));
            }
            else if (HasCategory(metrics, "queue") || HasCategory(metrics, "dlq"))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                AppendCompletionSlaLine(metrics, lines);
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Queue latency {FormatMinutes(metrics.LatencyMinutes)}");
                lines.Add(FormatFlowLatencyLine(metrics));
            }
            else if (metrics.HasServiceSemantics)
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                AppendCompletionSlaLine(metrics, lines);
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add(FormatFlowLatencyLine(metrics));
            }
            else if (HasCategory(metrics, "router"))
            {
                lines.Add($"SLA {FormatPercent(metrics.SuccessRate)}");
                AppendCompletionSlaLine(metrics, lines);
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add(FormatFlowLatencyLine(metrics));
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
                AppendCompletionSlaLine(metrics, lines);
                lines.Add($"Utilization {FormatPercentRounded(metrics.Utilization)}");
                lines.Add($"Errors {FormatPercent(metrics.ErrorRate)}");
                lines.Add($"Queue {FormatNumber(metrics.QueueDepth)}");
                lines.Add($"Service time {FormatServiceTime(metrics.ServiceTimeMs)}");
                lines.Add(FormatFlowLatencyLine(metrics));
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

    private static string? ResolveKindLabel(NodeBinMetrics metrics)
    {
        if (!string.IsNullOrWhiteSpace(metrics.AnalyticalIdentity))
        {
            return metrics.AnalyticalIdentity;
        }

        if (!string.IsNullOrWhiteSpace(metrics.NodeCategory))
        {
            return metrics.NodeCategory;
        }

        return metrics.NodeKind;
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

    private static void AppendCompletionSlaLine(NodeBinMetrics metrics, List<string> lines)
    {
        var completion = TryGetRawMetric(metrics.RawMetrics, "completionSla");
        if (completion.HasValue)
        {
            lines.Add($"Completion SLA {FormatPercent(completion)}");
        }
    }

    private static bool HasCategory(NodeBinMetrics metrics, string category)
    {
        return string.Equals(metrics.NodeCategory, category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasIdentity(NodeBinMetrics metrics, string identity)
    {
        return string.Equals(metrics.AnalyticalIdentity, identity, StringComparison.OrdinalIgnoreCase);
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

    private static string FormatFlowLatencyLine(NodeBinMetrics metrics)
    {
        var flowLatency = metrics.FlowLatencyMs ?? TryGetRawMetric(metrics.RawMetrics, "flowLatencyMs");
        if (flowLatency.HasValue)
        {
            return $"Flow latency {FormatLatency(flowLatency)}";
        }

        var isSink = HasCategory(metrics, "sink");
        var arrivals = TryGetRawMetric(metrics.RawMetrics, "arrivals");
        if (isSink && arrivals.HasValue && arrivals.Value <= 0)
        {
            return "Flow latency - (no arrivals in bin)";
        }

        var served = TryGetRawMetric(metrics.RawMetrics, "served");
        if (served.HasValue && served.Value <= 0)
        {
            return "Flow latency - (no completions in bin)";
        }

        return "Flow latency -";
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
        return FormatDurationMs(value);
    }

    private static string FormatDurationMs(double? value)
    {
        if (!value.HasValue)
        {
            return "-";
        }

        var sample = value.Value;
        if (sample >= 10_000)
        {
            var minutes = sample / 1000d / 60d;
            return $"{minutes.ToString("0.0", CultureInfo.InvariantCulture)} min";
        }

        var format = sample >= 1000 ? "0" : "0.0";
        return $"{sample.ToString(format, CultureInfo.InvariantCulture)} ms";
    }

    private static bool HasRawMetric(IReadOnlyDictionary<string, double?>? rawMetrics, string key)
    {
        if (rawMetrics is null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return rawMetrics.ContainsKey(key);
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
