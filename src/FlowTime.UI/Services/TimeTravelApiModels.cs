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

    [JsonPropertyName("edges")]
    public IReadOnlyList<TimeTravelEdgeSeriesDto>? Edges { get; init; } = Array.Empty<TimeTravelEdgeSeriesDto>();

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

    [JsonPropertyName("templateNarrative")]
    public string? TemplateNarrative { get; init; }

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

    [JsonPropertyName("classCoverage")]
    public string? ClassCoverage { get; init; }
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

    [JsonPropertyName("nodeLogicalType")]
    public string? LogicalType { get; init; }

    [JsonPropertyName("metrics")]
    public TimeTravelNodeMetricsDto Metrics { get; init; } = new();

    [JsonPropertyName("byClass")]
    public IReadOnlyDictionary<string, TimeTravelClassMetricsDto> ByClass { get; init; } =
        new Dictionary<string, TimeTravelClassMetricsDto>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("derived")]
    public TimeTravelNodeDerivedMetricsDto Derived { get; init; } = new();

    [JsonPropertyName("telemetry")]
    public TimeTravelNodeTelemetryDto Telemetry { get; init; } = new();

    [JsonPropertyName("aliases")]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }

    [JsonPropertyName("dispatchSchedule")]
    public TimeTravelDispatchScheduleDto? DispatchSchedule { get; init; }

    [JsonPropertyName("queueLatencyStatus")]
    public TimeTravelQueueLatencyStatusDto? QueueLatencyStatus { get; init; }

    [JsonPropertyName("sla")]
    public IReadOnlyList<TimeTravelSlaMetricDto>? Sla { get; init; }
}

public sealed record TimeTravelNodeSeriesDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("nodeLogicalType")]
    public string? LogicalType { get; init; }

    [JsonPropertyName("series")]
    public IReadOnlyDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("byClass")]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double?[]>> ByClass { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("telemetry")]
    public TimeTravelNodeTelemetryDto Telemetry { get; init; } = new();

    [JsonPropertyName("aliases")]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }

    [JsonPropertyName("dispatchSchedule")]
    public TimeTravelDispatchScheduleDto? DispatchSchedule { get; init; }

    [JsonPropertyName("queueLatencyStatus")]
    public TimeTravelQueueLatencyStatusDto?[]? QueueLatencyStatus { get; init; }

    [JsonPropertyName("sla")]
    public IReadOnlyList<TimeTravelSlaSeriesDto>? Sla { get; init; }
}

public sealed record TimeTravelSlaMetricDto
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("threshold")]
    public double? Threshold { get; init; }

    [JsonPropertyName("value")]
    public double? Value { get; init; }
}

public sealed record TimeTravelSlaSeriesDto
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("threshold")]
    public double? Threshold { get; init; }

    [JsonPropertyName("values")]
    public double?[] Values { get; init; } = Array.Empty<double?>();
}

public sealed record TimeTravelEdgeSeriesDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("to")]
    public required string To { get; init; }

    [JsonPropertyName("edgeType")]
    public string? EdgeType { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("multiplier")]
    public double? Multiplier { get; init; }

    [JsonPropertyName("lag")]
    public int? Lag { get; init; }

    [JsonPropertyName("series")]
    public IReadOnlyDictionary<string, double?[]> Series { get; init; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
}

public sealed record TimeTravelNodeMetricsDto
{
    [JsonPropertyName("arrivals")]
    public double? Arrivals { get; init; }

    [JsonPropertyName("served")]
    public double? Served { get; init; }

    [JsonPropertyName("errors")]
    public double? Errors { get; init; }

    [JsonPropertyName("attempts")]
    public double? Attempts { get; init; }

    [JsonPropertyName("failures")]
    public double? Failures { get; init; }

    [JsonPropertyName("exhaustedFailures")]
    public double? ExhaustedFailures { get; init; }

    [JsonPropertyName("retryEcho")]
    public double? RetryEcho { get; init; }

    [JsonPropertyName("retryBudgetRemaining")]
    public double? RetryBudgetRemaining { get; init; }

    [JsonPropertyName("queue")]
    public double? Queue { get; init; }

    [JsonPropertyName("capacity")]
    public double? Capacity { get; init; }

    [JsonPropertyName("externalDemand")]
    public double? ExternalDemand { get; init; }

    [JsonPropertyName("maxAttempts")]
    public double? MaxAttempts { get; init; }

    [JsonPropertyName("queueLatencyStatus")]
    public TimeTravelQueueLatencyStatusDto? QueueLatencyStatus { get; init; }
}

public sealed record TimeTravelClassMetricsDto
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

    [JsonPropertyName("processingTimeMsSum")]
    public double? ProcessingTimeMsSum { get; init; }

    [JsonPropertyName("servedCount")]
    public double? ServedCount { get; init; }
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

public sealed record TimeTravelQueueLatencyStatusDto
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; init; }
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

    [JsonPropertyName("startBin")]
    public int? StartBin { get; init; }

    [JsonPropertyName("endBin")]
    public int? EndBin { get; init; }

    [JsonPropertyName("signal")]
    public string? Signal { get; init; }
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

public sealed record TimeTravelDispatchScheduleDto
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "time-based";

    [JsonPropertyName("periodBins")]
    public int PeriodBins { get; init; }

    [JsonPropertyName("phaseOffset")]
    public int PhaseOffset { get; init; }

    [JsonPropertyName("capacitySeries")]
    public string? CapacitySeries { get; init; }
}
