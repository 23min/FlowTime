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
    [property: JsonPropertyName("features")] string[]? Features,
    [property: JsonPropertyName("limits")] LimitsInfo? Limits);

public record LimitsInfo(
    [property: JsonPropertyName("maxBins")] int? MaxBins,
    [property: JsonPropertyName("maxSeed")] int? MaxSeed,
    [property: JsonPropertyName("supportedArrivalKinds")] string[]? SupportedArrivalKinds,
    [property: JsonPropertyName("supportedRngTypes")] string[]? SupportedRngTypes);

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
    [property: JsonPropertyName("system")] SystemInfo? System,
    [property: JsonPropertyName("availableEndpoints")] string[]? AvailableEndpoints);

public record SystemInfo(
    [property: JsonPropertyName("workingSetMB")] double? WorkingSetMB,
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("architecture")] string? Architecture);

// FlowTime-Sim specific detailed health response format
public record FlowTimeSimDetailedHealthResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("service")] string? Service,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("uptime")] string? Uptime,
    [property: JsonPropertyName("environment")] string? Environment,
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory,
    [property: JsonPropertyName("system")] SystemInfo? System,
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
    [property: JsonPropertyName("binMinutes")] int BinMinutes);

// Generic wrapper so the UI can surface HTTP status codes and error messages instead of silently returning null.
public sealed record ApiCallResult<T>(T? Value, bool Success, int StatusCode, string? Error)
{
    public static ApiCallResult<T> Ok(T value, int status) => new(value, true, status, null);
    public static ApiCallResult<T> Fail(int status, string? error) => new(default, false, status, error);
}

// Basic health info for FlowTime API
public record SimpleHealthInfo
{
    public string? ServiceName { get; init; }
    public string? ApiVersion { get; init; }
    public string? Status { get; init; }
    public TimeSpan? Uptime { get; init; }
}
