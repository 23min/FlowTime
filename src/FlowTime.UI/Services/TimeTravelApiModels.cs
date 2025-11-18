using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowTime.UI.Services;

public sealed record TimeTravelStateSnapshotDto
{
    [JsonPropertyName("metadata")]
    public required TimeTravelStateMetadataDto Metadata { get; init; }

    [JsonPropertyName("bin")]
    public required TimeTravelBinDetailDto Bin { get; init; }

    [JsonPropertyName("nodes")]
    public IReadOnlyList<TimeTravelNodeSnapshotDto> Nodes { get; init; } = Array.Empty<TimeTravelNodeSnapshotDto>();

    [JsonPropertyName("warnings")]
    public IReadOnlyList<TimeTravelStateWarningDto> Warnings { get; init; } = Array.Empty<TimeTravelStateWarningDto>();
}

public sealed record TimeTravelStateWindowDto
{
    [JsonPropertyName("metadata")]
    public required TimeTravelStateMetadataDto Metadata { get; init; }

    [JsonPropertyName("window")]
    public required TimeTravelWindowSliceDto Window { get; init; }

    [JsonPropertyName("timestampsUtc")]
    public IReadOnlyList<DateTimeOffset> TimestampsUtc { get; init; } = Array.Empty<DateTimeOffset>();

    [JsonPropertyName("nodes")]
    public IReadOnlyList<TimeTravelNodeSeriesDto> Nodes { get; init; } = Array.Empty<TimeTravelNodeSeriesDto>();

    [JsonPropertyName("warnings")]
    public IReadOnlyList<TimeTravelStateWarningDto> Warnings { get; init; } = Array.Empty<TimeTravelStateWarningDto>();
}

public sealed record TimeTravelStateMetadataDto
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("templateId")]
    public required string TemplateId { get; init; }

    [JsonPropertyName("templateTitle")]
    public string? TemplateTitle { get; init; }

    [JsonPropertyName("templateVersion")]
    public string? TemplateVersion { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("provenanceHash")]
    public string? ProvenanceHash { get; init; }

    [JsonPropertyName("telemetrySourcesResolved")]
    public bool TelemetrySourcesResolved { get; init; }

    [JsonPropertyName("schema")]
    public required TimeTravelSchemaMetadataDto Schema { get; init; }

    [JsonPropertyName("storage")]
    public required TimeTravelStorageDescriptorDto Storage { get; init; }
}

public sealed record TimeTravelSchemaMetadataDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("hash")]
    public required string Hash { get; init; }
}

public sealed record TimeTravelStorageDescriptorDto
{
    [JsonPropertyName("modelPath")]
    public string? ModelPath { get; init; }

    [JsonPropertyName("metadataPath")]
    public string? MetadataPath { get; init; }

    [JsonPropertyName("provenancePath")]
    public string? ProvenancePath { get; init; }
}

public sealed record TimeTravelBinDetailDto
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("startUtc")]
    public DateTimeOffset? StartUtc { get; init; }

    [JsonPropertyName("endUtc")]
    public DateTimeOffset? EndUtc { get; init; }

    [JsonPropertyName("durationMinutes")]
    public double DurationMinutes { get; init; }
}

public sealed record TimeTravelWindowSliceDto
{
    [JsonPropertyName("startBin")]
    public required int StartBin { get; init; }

    [JsonPropertyName("endBin")]
    public required int EndBin { get; init; }

    [JsonPropertyName("binCount")]
    public required int BinCount { get; init; }
}

public sealed record TimeTravelNodeSnapshotDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("metrics")]
    public TimeTravelNodeMetricsDto Metrics { get; init; } = new();

    [JsonPropertyName("derived")]
    public TimeTravelNodeDerivedMetricsDto Derived { get; init; } = new();

    [JsonPropertyName("telemetry")]
    public TimeTravelNodeTelemetryDto Telemetry { get; init; } = new();

    [JsonPropertyName("aliases")]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}

public sealed record TimeTravelNodeSeriesDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("series")]
    public IReadOnlyDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("telemetry")]
    public TimeTravelNodeTelemetryDto Telemetry { get; init; } = new();

    [JsonPropertyName("aliases")]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}

public sealed record TimeTravelNodeMetricsDto
{
    [JsonPropertyName("arrivals")]
    public double? Arrivals { get; init; }

    [JsonPropertyName("served")]
    public double? Served { get; init; }

    [JsonPropertyName("errors")]
    public double? Errors { get; init; }

    [JsonPropertyName("queue")]
    public double? Queue { get; init; }

    [JsonPropertyName("capacity")]
    public double? Capacity { get; init; }

    [JsonPropertyName("externalDemand")]
    public double? ExternalDemand { get; init; }
}

public sealed record TimeTravelNodeDerivedMetricsDto
{
    [JsonPropertyName("utilization")]
    public double? Utilization { get; init; }

    [JsonPropertyName("latencyMinutes")]
    public double? LatencyMinutes { get; init; }

    [JsonPropertyName("serviceTimeMs")]
    public double? ServiceTimeMs { get; init; }

    [JsonPropertyName("flowLatencyMs")]
    public double? FlowLatencyMs { get; init; }

    [JsonPropertyName("throughputRatio")]
    public double? ThroughputRatio { get; init; }

    [JsonPropertyName("retryTax")]
    public double? RetryTax { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }
}

public sealed record TimeTravelNodeTelemetryDto
{
    [JsonPropertyName("sources")]
    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();

    [JsonPropertyName("warnings")]
    public IReadOnlyList<TimeTravelNodeTelemetryWarningDto> Warnings { get; init; } = Array.Empty<TimeTravelNodeTelemetryWarningDto>();
}

public sealed record TimeTravelNodeTelemetryWarningDto
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }
}

public sealed record TimeTravelStateWarningDto
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    [JsonPropertyName("nodeId")]
    public string? NodeId { get; init; }
}

public sealed record TimeTravelMetricsResponseDto
{
    [JsonPropertyName("window")]
    public required TimeTravelMetricsWindowDto Window { get; init; }

    [JsonPropertyName("grid")]
    public required TimeTravelMetricsGridDto Grid { get; init; }

    [JsonPropertyName("services")]
    public IReadOnlyList<TimeTravelServiceMetricsDto> Services { get; init; } = Array.Empty<TimeTravelServiceMetricsDto>();
}

public sealed record TimeTravelMetricsWindowDto
{
    [JsonPropertyName("start")]
    public DateTimeOffset? Start { get; init; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }
}

public sealed record TimeTravelMetricsGridDto
{
    [JsonPropertyName("binMinutes")]
    public int BinMinutes { get; init; }

    [JsonPropertyName("bins")]
    public int Bins { get; init; }
}

public sealed record TimeTravelServiceMetricsDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("slaPct")]
    public double SlaPct { get; init; }

    [JsonPropertyName("binsMet")]
    public int BinsMet { get; init; }

    [JsonPropertyName("binsTotal")]
    public int BinsTotal { get; init; }

    [JsonPropertyName("mini")]
    public IReadOnlyList<double?> Mini { get; init; } = Array.Empty<double?>();
}
