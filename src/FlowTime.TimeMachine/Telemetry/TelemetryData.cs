using FlowTime.Core.Models;

namespace FlowTime.TimeMachine.Telemetry;

/// <summary>
/// Deterministic telemetry snapshot returned by an <see cref="ITelemetrySource"/>.
/// Carries a time grid, a series dictionary (node-id → values), and optional
/// provenance metadata so the snapshot can be reproduced exactly.
/// </summary>
public sealed class TelemetryData
{
    /// <summary>Grid definition: bins, bin size, bin unit.</summary>
    public required TimeGrid Grid { get; init; }

    /// <summary>
    /// Series values keyed by node ID.
    /// Each value array has exactly <see cref="TimeGrid.Bins"/> elements.
    /// Class-specific series use the convention <c>nodeId@classId</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, double[]> Series { get; init; }

    /// <summary>
    /// Optional provenance: records where the snapshot came from and when it
    /// was captured so the run can be reproduced exactly.
    /// </summary>
    public TelemetryProvenance? Provenance { get; init; }
}

/// <summary>
/// Provenance metadata attached to a <see cref="TelemetryData"/> snapshot.
/// </summary>
public sealed class TelemetryProvenance
{
    /// <summary>Absolute or relative path to the source directory or file.</summary>
    public string? SourcePath { get; init; }

    /// <summary>UTC timestamp when the snapshot was taken.</summary>
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>SHA-256 or similar hash of the source content for integrity checks.</summary>
    public string? ContentHash { get; init; }
}
