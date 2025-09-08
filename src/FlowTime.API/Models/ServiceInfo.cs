using System.Text.Json.Serialization;

namespace FlowTime.API.Models;

/// <summary>
/// Comprehensive service information for health endpoints and service discovery
/// </summary>
public record ServiceInfo
{
    [JsonPropertyName("serviceName")]
    public required string ServiceName { get; init; }
    
    [JsonPropertyName("apiVersion")]
    public required string ApiVersion { get; init; }
    
    [JsonPropertyName("build")]
    public required BuildInfo Build { get; init; }
    
    [JsonPropertyName("capabilities")]
    public required CapabilitiesInfo Capabilities { get; init; }
    
    [JsonPropertyName("dependencies")]
    public required DependenciesInfo Dependencies { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("startTime")]
    public required DateTime StartTime { get; init; }
    
    [JsonPropertyName("uptime")]
    public required TimeSpan Uptime { get; init; }
    
    [JsonPropertyName("health")]
    public HealthInfo? Health { get; init; }
}

/// <summary>
/// Build and version metadata
/// </summary>
public record BuildInfo
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [JsonPropertyName("commitHash")]
    public string? CommitHash { get; init; }
    
    [JsonPropertyName("buildTime")]
    public DateTime? BuildTime { get; init; }
    
    [JsonPropertyName("environment")]
    public required string Environment { get; init; }
}

/// <summary>
/// Service capabilities and feature flags
/// </summary>
public record CapabilitiesInfo
{
    [JsonPropertyName("supportedFormats")]
    public required string[] SupportedFormats { get; init; }
    
    [JsonPropertyName("features")]
    public required string[] Features { get; init; }
    
    [JsonPropertyName("limits")]
    public required Dictionary<string, object> Limits { get; init; }
}

/// <summary>
/// Runtime and dependency information
/// </summary>
public record DependenciesInfo
{
    [JsonPropertyName("dotNetVersion")]
    public required string DotNetVersion { get; init; }
    
    [JsonPropertyName("runtimeIdentifier")]
    public required string RuntimeIdentifier { get; init; }
    
    [JsonPropertyName("packages")]
    public required Dictionary<string, string> Packages { get; init; }
}

/// <summary>
/// Health check information including storage locations
/// </summary>
public record HealthInfo
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("lastCheckTime")]
    public required DateTime LastCheckTime { get; init; }
    
    [JsonPropertyName("details")]
    public HealthDetails? Details { get; init; }
}

/// <summary>
/// Storage location details for health information
/// </summary>
public record HealthDetails
{
    [JsonPropertyName("dataDirectory")]
    public string? DataDirectory { get; init; }
    
    [JsonPropertyName("runsDirectory")]
    public string? RunsDirectory { get; init; }
}
