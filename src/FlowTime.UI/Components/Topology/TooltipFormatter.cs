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

        if (metrics.PmfProbability.HasValue || metrics.PmfValue.HasValue)
        {
            lines.Add($"Kind: {kindLabel}");
            if (metrics.PmfProbability is double probability)
            {
                lines.Add($"Probability {probability.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            if (metrics.PmfValue is double pmfValue)
            {
                lines.Add($"Value {pmfValue.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            if (metrics.CustomValue.HasValue)
            {
                var expectationLabel = string.IsNullOrWhiteSpace(metrics.CustomLabel) ? "Expectation" : metrics.CustomLabel!;
                lines.Add($"{expectationLabel} {metrics.CustomValue.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }
        else
        {
            lines.Add($"Kind: {kindLabel}");
            if (metrics.CustomValue.HasValue && !string.Equals(metrics.CustomLabel, "bin(t)", StringComparison.Ordinal))
            {
                var label = string.IsNullOrWhiteSpace(metrics.CustomLabel) ? "Value" : metrics.CustomLabel!;
                lines.Add($"{label} {metrics.CustomValue.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
            else if (!string.IsNullOrWhiteSpace(metrics.CustomLabel) && !string.Equals(metrics.CustomLabel, "bin(t)", StringComparison.Ordinal))
            {
                lines.Add(metrics.CustomLabel!);
            }

            if (metrics.SuccessRate is double successRate)
            {
                lines.Add($"SLA {successRate * 100:F1}%");
            }

            if (metrics.Utilization is double utilization)
            {
                var rounded = Math.Round(utilization * 100, MidpointRounding.AwayFromZero);
                lines.Add($"Utilization {rounded:0}%");
            }

            if (metrics.ErrorRate is double errorRate)
            {
                lines.Add($"Errors {errorRate * 100:F1}%");
            }

            if (metrics.QueueDepth is double queueDepth)
            {
                lines.Add($"Queue {Math.Round(queueDepth, MidpointRounding.AwayFromZero):0}");
            }

            if (metrics.ServiceTimeMs is double serviceTime)
            {
                lines.Add($"Service time {serviceTime.ToString(serviceTime >= 1000 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms");
            }

            if (metrics.LatencyMinutes is double latencyMinutes)
            {
                lines.Add($"Latency {latencyMinutes:F1} min");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("No metrics for selected bin");
        }

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
}

public sealed record TooltipContent(string Title, string Subtitle, string[] Lines);
