using System.Collections.Generic;
using System.Linq;
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
    public required IReadOnlyList<ClassEntry<ClassMetricsSnapshot>> ClassEntries { get; init; }
    public required ClassCoverage Coverage { get; init; }
    public required IReadOnlyList<ModeValidationWarning> Warnings { get; init; }
}

public static class ClassMetricsAggregator
{
    private const double conservationTolerance = 1d;

    public static ClassAggregationResult Aggregate(NodeData data, int binIndex)
    {
        _ = data ?? throw new ArgumentNullException(nameof(data));
        if (binIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        }

        var warnings = new List<ModeValidationWarning>();
        var classEntries = BuildSnapshotEntries(data, binIndex);
        var specificSnapshots = classEntries
            .Where(entry => entry.Kind == ClassEntryKind.Specific)
            .ToDictionary(
                entry => entry.ContractKey,
                entry => entry.Payload,
                StringComparer.OrdinalIgnoreCase);
        var hasClassData = specificSnapshots.Count > 0;

        if (hasClassData)
        {
            CheckConservation("arrivals", Sample(data.Arrivals, binIndex), specificSnapshots, snapshot => snapshot.Arrivals, warnings);
            CheckConservation("served", Sample(data.Served, binIndex), specificSnapshots, snapshot => snapshot.Served, warnings);
            CheckConservation("errors", Sample(data.Errors, binIndex), specificSnapshots, snapshot => snapshot.Errors, warnings);
        }

        var coverage = ResolveCoverage(hasClassData, warnings);

        return new ClassAggregationResult
        {
            ClassEntries = classEntries,
            Coverage = coverage,
            Warnings = warnings
        };
    }

    public static IReadOnlyList<ClassEntry<NodeClassData>> BuildClassEntries(NodeData data)
    {
        _ = data ?? throw new ArgumentNullException(nameof(data));

        if (data.ClassEntries is { Count: > 0 } explicitEntries)
        {
            return NormalizeEntries(explicitEntries);
        }

        if (data.ByClass is null || data.ByClass.Count == 0)
        {
            return Array.Empty<ClassEntry<NodeClassData>>();
        }

        return data.ByClass
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ClassEntry<NodeClassData>.Specific(entry.Key, entry.Value ?? new NodeClassData()))
            .ToArray();
    }

    private static IReadOnlyList<ClassEntry<ClassMetricsSnapshot>> BuildSnapshotEntries(NodeData data, int binIndex)
    {
        var snapshotEntries = new List<ClassEntry<ClassMetricsSnapshot>>();

        foreach (var entry in BuildClassEntries(data))
        {
            var classData = entry.Payload ?? new NodeClassData();
            var snapshot = new ClassMetricsSnapshot(
                Arrivals: Sample(classData.Arrivals, binIndex),
                Served: Sample(classData.Served, binIndex),
                Errors: Sample(classData.Errors, binIndex),
                Queue: Sample(classData.QueueDepth, binIndex),
                Capacity: Sample(classData.Capacity, binIndex),
                ProcessingTimeMsSum: Sample(classData.ProcessingTimeMsSum, binIndex),
                ServedCount: Sample(classData.ServedCount, binIndex));

            if (entry.Kind == ClassEntryKind.Fallback)
            {
                snapshotEntries.Add(ClassEntry<ClassMetricsSnapshot>.Fallback(snapshot));
                continue;
            }

            snapshotEntries.Add(ClassEntry<ClassMetricsSnapshot>.Specific(entry.ClassId!, snapshot));
        }

        return NormalizeEntries(snapshotEntries);
    }

    private static IReadOnlyList<ClassEntry<TPayload>> NormalizeEntries<TPayload>(IEnumerable<ClassEntry<TPayload>> entries)
    {
        var specificEntries = entries
            .Where(entry => entry.Kind == ClassEntryKind.Specific)
            .OrderBy(entry => entry.ContractKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fallbackEntry = entries.FirstOrDefault(entry => entry.Kind == ClassEntryKind.Fallback);

        if (fallbackEntry is null)
        {
            return specificEntries;
        }

        var normalized = new List<ClassEntry<TPayload>>(specificEntries.Count + 1);
        normalized.AddRange(specificEntries);
        normalized.Add(fallbackEntry);
        return normalized;
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

        if (Math.Abs(total.Value - sum) > conservationTolerance)
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
