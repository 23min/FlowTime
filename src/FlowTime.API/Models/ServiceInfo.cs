namespace FlowTime.API.Models;

/// <summary>
/// Comprehensive service information for health endpoints and service discovery
/// </summary>
public record ServiceInfo
{
    public required string ServiceName { get; init; }
    public required string ApiVersion { get; init; }
    public required BuildInfo Build { get; init; }
    public required CapabilitiesInfo Capabilities { get; init; }
    public required DependenciesInfo Dependencies { get; init; }
    public required string Status { get; init; }
    public required DateTime StartTime { get; init; }
    public required TimeSpan Uptime { get; init; }
}

/// <summary>
/// Build and version metadata
/// </summary>
public record BuildInfo
{
    public required string Version { get; init; }
    public string? CommitHash { get; init; }
    public DateTime? BuildTime { get; init; }
    public required string Environment { get; init; }
}

/// <summary>
/// Service capabilities and feature flags
/// </summary>
public record CapabilitiesInfo
{
    public required string[] SupportedFormats { get; init; }
    public required string[] Features { get; init; }
    public required Dictionary<string, object> Limits { get; init; }
}

/// <summary>
/// Runtime and dependency information
/// </summary>
public record DependenciesInfo
{
    public required string DotNetVersion { get; init; }
    public required string RuntimeIdentifier { get; init; }
    public required Dictionary<string, string> Packages { get; init; }
}
