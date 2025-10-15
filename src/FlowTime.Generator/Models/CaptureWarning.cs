namespace FlowTime.Generator.Models;

/// <summary>
/// Describes a warning produced during telemetry capture (e.g., gap fills).
/// </summary>
public sealed record CaptureWarning(
    string Code,
    string Message,
    string? NodeId = null,
    IReadOnlyList<int>? Bins = null);
