using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowTime.Contracts.TimeTravel;

public sealed class StateSnapshotResponse
{
    public required StateMetadata Metadata { get; init; }
    public required BinDetail Bin { get; init; }
    public IReadOnlyList<NodeSnapshot> Nodes { get; init; } = Array.Empty<NodeSnapshot>();
    public IReadOnlyList<StateWarning> Warnings { get; init; } = Array.Empty<StateWarning>();
}

public sealed class StateWindowResponse
{
    public required StateMetadata Metadata { get; init; }
    public required WindowSlice Window { get; init; }
    public required IReadOnlyList<DateTimeOffset> TimestampsUtc { get; init; }
    public IReadOnlyList<NodeSeries> Nodes { get; init; } = Array.Empty<NodeSeries>();
    public IReadOnlyList<EdgeSeries> Edges { get; init; } = Array.Empty<EdgeSeries>();
    public IReadOnlyList<StateWarning> Warnings { get; init; } = Array.Empty<StateWarning>();
}

public sealed class StateMetadata
{
    public required string RunId { get; init; }
    public required string TemplateId { get; init; }
    public string? TemplateTitle { get; init; }
    public string? TemplateNarrative { get; init; }
    public string? TemplateVersion { get; init; }
    public required string Mode { get; init; }
    public string? ProvenanceHash { get; init; }
    public bool TelemetrySourcesResolved { get; init; }
    public required SchemaMetadata Schema { get; init; }
    public required StorageDescriptor Storage { get; init; }
    public RunRngOptions? Rng { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputHash { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClassCoverage { get; init; }
}

public sealed class SchemaMetadata
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Hash { get; init; }
}

public sealed class StorageDescriptor
{
    public required string ModelPath { get; init; }
    public string? MetadataPath { get; init; }
    public string? ProvenancePath { get; init; }
}

public sealed class BinDetail
{
    public required int Index { get; init; }
    public DateTimeOffset? StartUtc { get; init; }
    public DateTimeOffset? EndUtc { get; init; }
    public double DurationMinutes { get; init; }
}

public sealed class WindowSlice
{
    public required int StartBin { get; init; }
    public required int EndBin { get; init; }
    public required int BinCount { get; init; }
}

public sealed class NodeSnapshot
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    [JsonPropertyName("nodeLogicalType")]
    public string? LogicalType { get; init; }
    public NodeMetrics Metrics { get; init; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, SeriesSemanticsMetadata>? SeriesMetadata { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, ClassMetrics>? ByClass { get; init; }
    public NodeDerivedMetrics Derived { get; init; } = new();
    public NodeTelemetryInfo Telemetry { get; init; } = new();
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DispatchScheduleDescriptor? DispatchSchedule { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SlaMetricDescriptor>? Sla { get; init; }
}

public sealed class NodeSeries
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    [JsonPropertyName("nodeLogicalType")]
    public string? LogicalType { get; init; }
    public IDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, SeriesSemanticsMetadata>? SeriesMetadata { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, IDictionary<string, double?[]>>? ByClass { get; init; }
    public NodeTelemetryInfo Telemetry { get; init; } = new();
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DispatchScheduleDescriptor? DispatchSchedule { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueueLatencyStatusDescriptor?[]? QueueLatencyStatus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SlaSeriesDescriptor>? Sla { get; init; }
}

public sealed class SlaMetricDescriptor
{
    public required string Kind { get; init; }
    public string Status { get; init; } = "ok";
    public double? Threshold { get; init; }
    public double? Value { get; init; }
}

public sealed class SlaSeriesDescriptor
{
    public required string Kind { get; init; }
    public string Status { get; init; } = "ok";
    public double? Threshold { get; init; }
    public double?[] Values { get; init; } = Array.Empty<double?>();
}

public sealed class EdgeSeries
{
    public required string Id { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public string? EdgeType { get; init; }
    public string? Field { get; init; }
    public double? Multiplier { get; init; }
    public int? Lag { get; init; }
    public IDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
}

public sealed class NodeMetrics
{
    public double? Arrivals { get; init; }
    public double? Served { get; init; }
    public double? Errors { get; init; }
    public double? Attempts { get; init; }
    public double? Failures { get; init; }
    public double? ExhaustedFailures { get; init; }
    public double? RetryEcho { get; init; }
    public double? RetryBudgetRemaining { get; init; }
    public double? Queue { get; init; }
    public double? Capacity { get; init; }
    public double? Parallelism { get; init; }
    public double? ExternalDemand { get; init; }
    public double? MaxAttempts { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueueLatencyStatusDescriptor? QueueLatencyStatus { get; init; }
}

public sealed class ClassMetrics
{
    public double? Arrivals { get; init; }
    public double? Served { get; init; }
    public double? Errors { get; init; }
    public double? Queue { get; init; }
    public double? Capacity { get; init; }
    public double? ProcessingTimeMsSum { get; init; }
    public double? ServedCount { get; init; }
}

public sealed class NodeDerivedMetrics
{
    public double? Utilization { get; init; }
    public double? LatencyMinutes { get; init; }
    public double? ServiceTimeMs { get; init; }
    public double? FlowLatencyMs { get; init; }
    public double? ThroughputRatio { get; init; }
    public double? RetryTax { get; init; }
    public string? Color { get; init; }
}

public sealed class NodeTelemetryInfo
{
    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();
    public IReadOnlyList<NodeTelemetryWarning> Warnings { get; init; } = Array.Empty<NodeTelemetryWarning>();
}

public sealed class NodeTelemetryWarning
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Severity { get; init; }
}

public sealed class SeriesSemanticsMetadata
{
    public string? Aggregation { get; init; }
    public string? Origin { get; init; }
}

public sealed class StateWarning
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "warning";
    public string? NodeId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartBin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EndBin { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Signal { get; init; }
}
