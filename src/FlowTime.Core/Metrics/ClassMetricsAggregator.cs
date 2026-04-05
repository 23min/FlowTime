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

public enum ClassEntryKind
{
    Specific,
    Fallback
}

public sealed record ClassEntry<TPayload>
{
    public required ClassEntryKind Kind { get; init; }
    public string? ClassId { get; init; }
    public required string ContractKey { get; init; }
    public required TPayload Payload { get; init; }
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
        var byClassSnapshots = classEntries.ToDictionary(
            fact => fact.ContractKey,
            fact => fact.Payload,
            StringComparer.OrdinalIgnoreCase);
        var specificSnapshots = classEntries
            .Where(fact => fact.Kind == ClassEntryKind.Specific)
            .ToDictionary(
                fact => fact.ContractKey,
                fact => fact.Payload,
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
            ByClass = byClassSnapshots,
            ClassEntries = classEntries,
            Coverage = coverage,
            Warnings = warnings
        };
    }

    public static IReadOnlyList<ClassEntry<NodeClassData>> BuildClassEntries(NodeData data)
    {
        if (data.ByClass is null || data.ByClass.Count == 0)
        {
            return new[] { CreateFallbackSeriesEntry(data) };
        }

        var specificEntries = new List<ClassEntry<NodeClassData>>(data.ByClass.Count);
        ClassEntry<NodeClassData>? fallbackEntry = null;

        foreach (var kvp in data.ByClass)
        {
            var classData = kvp.Value ?? new NodeClassData();
            var entryKind = GetEntryKind(kvp.Key);

            if (entryKind == ClassEntryKind.Fallback)
            {
                fallbackEntry ??= new ClassEntry<NodeClassData>
                {
                    Kind = ClassEntryKind.Fallback,
                    ContractKey = "*",
                    Payload = classData
                };
                continue;
            }

            var classId = NormalizeClassId(kvp.Key);
            specificEntries.Add(new ClassEntry<NodeClassData>
            {
                Kind = ClassEntryKind.Specific,
                ClassId = classId,
                ContractKey = classId,
                Payload = classData
            });
        }

        specificEntries.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.ContractKey, right.ContractKey));

        var result = new List<ClassEntry<NodeClassData>>(specificEntries.Count + (fallbackEntry is null ? 0 : 1));
        result.AddRange(specificEntries);

        if (fallbackEntry is not null)
        {
            result.Add(fallbackEntry);
        }

        return result.Count == 0 ? new[] { CreateFallbackSeriesEntry(data) } : result;
    }

    public static bool IsFallbackClassId(string? value) => GetEntryKind(value) == ClassEntryKind.Fallback;

    private static IReadOnlyList<ClassEntry<ClassMetricsSnapshot>> BuildSnapshotEntries(NodeData data, int binIndex)
    {
        var result = new List<ClassEntry<ClassMetricsSnapshot>>();
        foreach (var fact in BuildClassEntries(data))
        {
            var classData = fact.Payload;
            result.Add(new ClassEntry<ClassMetricsSnapshot>
            {
                Kind = fact.Kind,
                ClassId = fact.ClassId,
                ContractKey = fact.ContractKey,
                Payload = new ClassMetricsSnapshot(
                    Arrivals: Sample(classData.Arrivals, binIndex),
                    Served: Sample(classData.Served, binIndex),
                    Errors: Sample(classData.Errors, binIndex),
                    Queue: Sample(classData.QueueDepth, binIndex),
                    Capacity: Sample(classData.Capacity, binIndex),
                    ProcessingTimeMsSum: Sample(classData.ProcessingTimeMsSum, binIndex),
                    ServedCount: Sample(classData.ServedCount, binIndex))
            });
        }

        return result;
    }

    private static ClassEntry<NodeClassData> CreateFallbackSeriesEntry(NodeData data)
    {
        return new ClassEntry<NodeClassData>
        {
            Kind = ClassEntryKind.Fallback,
            ContractKey = "*",
            Payload = new NodeClassData
            {
                Arrivals = data.Arrivals,
                Served = data.Served,
                Errors = data.Errors,
                QueueDepth = data.QueueDepth,
                Capacity = data.Capacity,
                ProcessingTimeMsSum = data.ProcessingTimeMsSum,
                ServedCount = data.ServedCount
            }
        };
    }

    private static ClassEntryKind GetEntryKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ClassEntryKind.Fallback;
        }

        var trimmed = value.Trim();
        if (trimmed == "*" ||
            string.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase))
        {
            return ClassEntryKind.Fallback;
        }

        return ClassEntryKind.Specific;
    }

    private static string NormalizeClassId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Class ids must be non-empty.");
        }

        return value.Trim();
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
