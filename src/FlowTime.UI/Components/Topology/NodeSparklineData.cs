using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

public sealed record NodeSparklineData(
    IReadOnlyList<double?> Values,
    IReadOnlyList<double?> Utilization,
    IReadOnlyList<double?> ErrorRate,
    IReadOnlyList<double?> QueueDepth,
    double Min,
    double Max,
    bool IsFlat,
    int StartIndex,
    IReadOnlyDictionary<string, SparklineSeriesSlice> Series)
{
    public static NodeSparklineData Create(
        IReadOnlyList<double?> values,
        IReadOnlyList<double?> utilization,
        IReadOnlyList<double?> errorRate,
        IReadOnlyList<double?> queueDepth,
        int startIndex,
        double? explicitMin = null,
        double? explicitMax = null,
        IReadOnlyDictionary<string, SparklineSeriesSlice>? additionalSeries = null)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("Values are required", nameof(values));
        }

        var (min, max, isFlat) = ComputeBounds(values, explicitMin, explicitMax);

        return new NodeSparklineData(
            values,
            utilization,
            errorRate,
            queueDepth,
            min,
            max,
            isFlat,
            startIndex,
            additionalSeries ?? new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase));
    }

    public static (double Min, double Max, bool IsFlat) ComputeBounds(
        IReadOnlyList<double?> values,
        double? explicitMin,
        double? explicitMax)
    {
        double min = explicitMin ?? double.PositiveInfinity;
        double max = explicitMax ?? double.NegativeInfinity;
        var hasValue = false;

        foreach (var value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }

            hasValue = true;
            var sample = value.Value;
            if (sample < min)
            {
                min = sample;
            }

            if (sample > max)
            {
                max = sample;
            }
        }

        if (!hasValue)
        {
            min = explicitMin ?? 0d;
            max = explicitMax ?? min;
        }

        if (!double.IsFinite(min))
        {
            min = explicitMin ?? 0d;
        }

        if (!double.IsFinite(max))
        {
            max = explicitMax ?? min;
        }

        var isFlat = Math.Abs(max - min) < 1e-6;
        if (isFlat)
        {
            max = min + 0.001d;
        }

        return (min, max, isFlat);
    }
}

public sealed record SparklineSeriesSlice(
    IReadOnlyList<double?> Values,
    int StartIndex);
