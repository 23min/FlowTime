using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowTime.UI.Services;

public record HealthResponse([property: JsonPropertyName("status")] string Status);

// Enhanced health models for detailed service information
public record DetailedHealthResponse(
    [property: JsonPropertyName("serviceName")] string? ServiceName,
    [property: JsonPropertyName("apiVersion")] string? ApiVersion,
    [property: JsonPropertyName("build")] BuildInfo? Build,
    [property: JsonPropertyName("capabilities")] CapabilitiesInfo? Capabilities,
    [property: JsonPropertyName("runtime")] RuntimeInfo? Runtime,
    [property: JsonPropertyName("health")] HealthInfo? Health);

public record BuildInfo(
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("commitHash")] string? CommitHash,
    [property: JsonPropertyName("buildTime")] string? BuildTime,
    [property: JsonPropertyName("environment")] string? Environment);

public record CapabilitiesInfo(
    [property: JsonPropertyName("supportedFormats")] string[]? SupportedFormats,
    [property: JsonPropertyName("features")] string[]? Features);

public record RuntimeInfo(
    [property: JsonPropertyName("startTime")] string? StartTime,
    [property: JsonPropertyName("uptime")] string? Uptime,
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("architecture")] string? Architecture,
    [property: JsonPropertyName("frameworkVersion")] string? FrameworkVersion);

public record HealthInfo(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("lastCheckTime")] string? LastCheckTime,
    [property: JsonPropertyName("details")] HealthDetails? Details);

public record HealthDetails(
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory,
    [property: JsonPropertyName("runsDirectory")] string? RunsDirectory,
    [property: JsonPropertyName("catalogsDirectory")] string? CatalogsDirectory);

// Alternative simple health format
public record SimpleHealthResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("service")] string? Service,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("uptime")] string? Uptime,
    [property: JsonPropertyName("environment")] string? Environment,
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory,
    [property: JsonPropertyName("availableEndpoints")] string[]? AvailableEndpoints);

// FlowTime-Sim specific detailed health response format
public record FlowTimeSimDetailedHealthResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("service")] string? Service,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("uptime")] string? Uptime,
    [property: JsonPropertyName("environment")] string? Environment,
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory,
    [property: JsonPropertyName("availableEndpoints")] string[]? AvailableEndpoints);

public record RunResponse(
    [property: JsonPropertyName("grid")] GridInfo Grid,
    [property: JsonPropertyName("order")] string[] Order,
    [property: JsonPropertyName("series")] Dictionary<string, double[]> Series,
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("artifactsPath")] string? ArtifactsPath);

public record GraphResponse(
    [property: JsonPropertyName("nodes")] string[] Nodes,
    [property: JsonPropertyName("order")] string[] Order,
    [property: JsonPropertyName("edges")] GraphEdge[] Edges);

public record GraphEdge(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("inputs")] string[] Inputs);

public record GridInfo(
    [property: JsonPropertyName("bins")] int Bins,
    [property: JsonPropertyName("binSize")] int BinSize,
    [property: JsonPropertyName("binUnit")] string BinUnit)
{
    // INTERNAL ONLY: Computed property for UI display convenience
    // NOT serialized to/from JSON (binMinutes removed from all external schemas)
    // NOTE: Engine validates binUnit at model parse time - invalid units should never reach UI
    [JsonIgnore]
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        "weeks" => BinSize * 10080,
        _ => throw new ArgumentException($"Invalid binUnit '{BinUnit}'. Engine should have validated this.")
    };
}

// Generic wrapper so the UI can surface HTTP status codes and error messages instead of silently returning null.
public sealed record ApiCallResult<T>(T? Value, bool Success, int StatusCode, string? Error)
{
    public static ApiCallResult<T> Ok(T value, int status) => new(value, true, status, null);
    public static ApiCallResult<T> Fail(int status, string? error) => new(default, false, status, error);
}

// Run listing models
public sealed record RunSummaryResponseDto(
    [property: JsonPropertyName("items")] IReadOnlyList<RunSummaryDto>? Items,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);

public sealed record RunSummaryDto(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("templateTitle")] string? TemplateTitle,
    [property: JsonPropertyName("templateVersion")] string? TemplateVersion,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("createdUtc")] DateTimeOffset? CreatedUtc,
    [property: JsonPropertyName("warningCount")] int WarningCount,
    [property: JsonPropertyName("telemetry")] RunTelemetrySummaryDto? Telemetry);

public sealed record RunCreateResponseDto(
    [property: JsonPropertyName("isDryRun")] bool IsDryRun,
    [property: JsonPropertyName("metadata")] RunMetadataDto? Metadata,
    [property: JsonPropertyName("plan")] RunCreatePlanDto? Plan,
    [property: JsonPropertyName("warnings")] IReadOnlyList<StateWarningDto>? Warnings,
    [property: JsonPropertyName("canReplay")] bool? CanReplay,
    [property: JsonPropertyName("telemetry")] RunTelemetrySummaryDto? Telemetry);

public sealed record RunMetadataDto(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("templateTitle")] string? TemplateTitle,
    [property: JsonPropertyName("templateVersion")] string? TemplateVersion,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("provenanceHash")] string? ProvenanceHash,
    [property: JsonPropertyName("telemetrySourcesResolved")] bool TelemetrySourcesResolved,
    [property: JsonPropertyName("schema")] SchemaMetadataDto Schema,
    [property: JsonPropertyName("storage")] StorageDescriptorDto Storage);

public sealed record SchemaMetadataDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("hash")] string Hash);

public sealed record StorageDescriptorDto(
    [property: JsonPropertyName("modelPath")] string? ModelPath,
    [property: JsonPropertyName("metadataPath")] string? MetadataPath,
    [property: JsonPropertyName("provenancePath")] string? ProvenancePath);

public sealed record StateWarningDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("nodeId")] string? NodeId);

public sealed record RunCreatePlanDto(
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("outputRoot")] string OutputRoot,
    [property: JsonPropertyName("captureDirectory")] string? CaptureDirectory,
    [property: JsonPropertyName("deterministicRunId")] bool DeterministicRunId,
    [property: JsonPropertyName("requestedRunId")] string? RequestedRunId,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, object?> Parameters,
    [property: JsonPropertyName("telemetryBindings")] IReadOnlyDictionary<string, string> TelemetryBindings,
    [property: JsonPropertyName("files")] IReadOnlyList<RunCreatePlanFileDto> Files,
    [property: JsonPropertyName("warnings")] IReadOnlyList<RunCreatePlanWarningDto> Warnings);

public sealed record RunCreatePlanFileDto(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("path")] string Path);

public sealed record RunCreatePlanWarningDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("nodeId")] string? NodeId,
    [property: JsonPropertyName("bins")] IReadOnlyList<int>? Bins);

public sealed record RunCreateRequestDto(
    [property: JsonPropertyName("templateId")] string TemplateId,
    [property: JsonPropertyName("mode")] string? Mode,
    [property: JsonPropertyName("parameters")] Dictionary<string, JsonElement>? Parameters,
    [property: JsonPropertyName("telemetry")] RunTelemetryOptionsDto? Telemetry,
    [property: JsonPropertyName("options")] RunCreationOptionsDto? Options);

public sealed record RunTelemetryOptionsDto(
    [property: JsonPropertyName("captureDirectory")] string? CaptureDirectory,
    [property: JsonPropertyName("bindings")] Dictionary<string, string>? Bindings);

public sealed record RunCreationOptionsDto(
    [property: JsonPropertyName("deterministicRunId")] bool DeterministicRunId,
    [property: JsonPropertyName("runId")] string? RunId,
    [property: JsonPropertyName("dryRun")] bool DryRun,
    [property: JsonPropertyName("overwriteExisting")] bool OverwriteExisting);

public sealed record RunTelemetrySummaryDto(
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("generatedAtUtc")] string? GeneratedAtUtc,
    [property: JsonPropertyName("warningCount")] int WarningCount,
    [property: JsonPropertyName("sourceRunId")] string? SourceRunId);

public sealed record TelemetryCaptureRequestDto(
    [property: JsonPropertyName("source")] TelemetryCaptureSourceDto Source,
    [property: JsonPropertyName("output")] TelemetryCaptureOutputDto Output);

public sealed record TelemetryCaptureSourceDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("runId")] string? RunId);

public sealed record TelemetryCaptureOutputDto(
    [property: JsonPropertyName("captureKey")] string? CaptureKey,
    [property: JsonPropertyName("directory")] string? Directory,
    [property: JsonPropertyName("overwrite")] bool Overwrite);

public sealed record TelemetryCaptureResponseDto(
    [property: JsonPropertyName("capture")] TelemetryCaptureSummaryDto Capture);

public sealed record TelemetryCaptureSummaryDto(
    [property: JsonPropertyName("generated")] bool Generated,
    [property: JsonPropertyName("alreadyExists")] bool AlreadyExists,
    [property: JsonPropertyName("generatedAtUtc")] string? GeneratedAtUtc,
    [property: JsonPropertyName("sourceRunId")] string? SourceRunId,
    [property: JsonPropertyName("warnings")] IReadOnlyList<StateWarningDto>? Warnings);
