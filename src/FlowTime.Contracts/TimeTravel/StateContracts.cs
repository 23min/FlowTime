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
    public IReadOnlyList<StateWarning> Warnings { get; init; } = Array.Empty<StateWarning>();
}

public sealed class StateMetadata
{
    public required string RunId { get; init; }
    public required string TemplateId { get; init; }
    public string? TemplateTitle { get; init; }
    public string? TemplateVersion { get; init; }
    public required string Mode { get; init; }
    public string? ProvenanceHash { get; init; }
    public bool TelemetrySourcesResolved { get; init; }
    public required SchemaMetadata Schema { get; init; }
    public required StorageDescriptor Storage { get; init; }
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
    public NodeMetrics Metrics { get; init; } = new();
    public NodeDerivedMetrics Derived { get; init; } = new();
    public NodeTelemetryInfo Telemetry { get; init; } = new();
}

public sealed class NodeSeries
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public IDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
    public NodeTelemetryInfo Telemetry { get; init; } = new();
}

public sealed class NodeMetrics
{
    public double? Arrivals { get; init; }
    public double? Served { get; init; }
    public double? Errors { get; init; }
    public double? Queue { get; init; }
    public double? Capacity { get; init; }
    public double? ExternalDemand { get; init; }
}

public sealed class NodeDerivedMetrics
{
    public double? Utilization { get; init; }
    public double? LatencyMinutes { get; init; }
    public double? ThroughputRatio { get; init; }
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

public sealed class StateWarning
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "warning";
    public string? NodeId { get; init; }
}
