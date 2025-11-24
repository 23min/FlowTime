using System.Collections.Generic;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;

namespace FlowTime.Core.Metrics;

public enum ClassCoverage
{
    Missing,
    Partial,
    Full
}

public sealed record ClassMetricsSnapshot(
    double? Arrivals,
    double? Served,
    double? Errors,
    double? Queue = null,
    double? Capacity = null,
    double? ProcessingTimeMsSum = null,
    double? ServedCount = null);

public sealed class ClassAggregationResult
{
    public required IReadOnlyDictionary<string, ClassMetricsSnapshot> ByClass { get; init; }
    public required ClassCoverage Coverage { get; init; }
    public required IReadOnlyList<ModeValidationWarning> Warnings { get; init; }
}

public static class ClassMetricsAggregator
{
    private const double ConservationTolerance = 1d;

    public static ClassAggregationResult Aggregate(NodeData data, int binIndex)
    {
        _ = data ?? throw new ArgumentNullException(nameof(data));
        if (binIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        }

        var warnings = new List<ModeValidationWarning>();
        var byClassSnapshots = BuildSnapshots(data, binIndex);
        var hasClassData = byClassSnapshots.Count > 0 && !(byClassSnapshots.Count == 1 && byClassSnapshots.ContainsKey("*"));

        if (hasClassData)
        {
            CheckConservation("arrivals", Sample(data.Arrivals, binIndex), byClassSnapshots, snapshot => snapshot.Arrivals, warnings);
            CheckConservation("served", Sample(data.Served, binIndex), byClassSnapshots, snapshot => snapshot.Served, warnings);
            CheckConservation("errors", Sample(data.Errors, binIndex), byClassSnapshots, snapshot => snapshot.Errors, warnings);
        }

        var coverage = ResolveCoverage(hasClassData, warnings);

        return new ClassAggregationResult
        {
            ByClass = byClassSnapshots,
            Coverage = coverage,
            Warnings = warnings
        };
    }

    private static IReadOnlyDictionary<string, ClassMetricsSnapshot> BuildSnapshots(NodeData data, int binIndex)
    {
        if (data.ByClass is null || data.ByClass.Count == 0)
        {
            var wildcard = new SortedDictionary<string, ClassMetricsSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["*"] = new ClassMetricsSnapshot(
                    Arrivals: Sample(data.Arrivals, binIndex),
                    Served: Sample(data.Served, binIndex),
                    Errors: Sample(data.Errors, binIndex),
                    Queue: Sample(data.QueueDepth, binIndex),
                    Capacity: Sample(data.Capacity, binIndex),
                    ProcessingTimeMsSum: Sample(data.ProcessingTimeMsSum, binIndex),
                    ServedCount: Sample(data.ServedCount, binIndex))
            };

            return wildcard;
        }

        var result = new SortedDictionary<string, ClassMetricsSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in data.ByClass)
        {
            var classId = string.IsNullOrWhiteSpace(kvp.Key) ? "*" : kvp.Key.Trim();
            var classData = kvp.Value ?? new NodeClassData();

            result[classId] = new ClassMetricsSnapshot(
                Arrivals: Sample(classData.Arrivals, binIndex),
                Served: Sample(classData.Served, binIndex),
                Errors: Sample(classData.Errors, binIndex),
                Queue: Sample(classData.QueueDepth, binIndex),
                Capacity: Sample(classData.Capacity, binIndex),
                ProcessingTimeMsSum: Sample(classData.ProcessingTimeMsSum, binIndex),
                ServedCount: Sample(classData.ServedCount, binIndex));
        }

        return result;
    }

    private static void CheckConservation(
        string metric,
        double? total,
        IReadOnlyDictionary<string, ClassMetricsSnapshot> byClass,
        Func<ClassMetricsSnapshot, double?> selector,
        List<ModeValidationWarning> warnings)
    {
        if (!total.HasValue || double.IsNaN(total.Value) || double.IsInfinity(total.Value))
        {
            return;
        }

        double sum = 0;
        var hasData = false;
        foreach (var snapshot in byClass.Values)
        {
            var value = selector(snapshot);
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                continue;
            }

            sum += value.Value;
            hasData = true;
        }

        if (!hasData)
        {
            return;
        }

        if (Math.Abs(total.Value - sum) > ConservationTolerance)
        {
            warnings.Add(new ModeValidationWarning
            {
                Code = "class_totals_mismatch",
                Message = $"Total {metric} {total.Value} differs from byClass sum {sum}.",
                NodeId = null
            });
        }
    }

    private static ClassCoverage ResolveCoverage(bool hasClassData, IReadOnlyList<ModeValidationWarning> warnings)
    {
        if (!hasClassData)
        {
            return ClassCoverage.Missing;
        }

        return warnings.Count == 0 ? ClassCoverage.Full : ClassCoverage.Partial;
    }

    private static double? Sample(double[]? series, int index)
    {
        if (series is null || index < 0 || index >= series.Length)
        {
            return null;
        }

        var value = series[index];
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        return value;
    }
}
