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

        if (metrics.CustomValue is double customValue)
        {
            var label = string.IsNullOrWhiteSpace(metrics.CustomLabel) ? "Value" : metrics.CustomLabel!;
            lines.Add($"{label} {customValue.ToString("0.###", CultureInfo.InvariantCulture)}");
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

        if (metrics.LatencyMinutes is double latencyMinutes)
        {
            lines.Add($"Latency {latencyMinutes:F1} min");
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
}

public sealed record TooltipContent(string Title, string Subtitle, string[] Lines);
