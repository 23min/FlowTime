using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;

namespace FlowTime.Generator;

/// <summary>
/// Options controlling telemetry bundle assembly.
/// </summary>
public sealed class TelemetryBundleOptions
{
    /// <summary>
    /// Directory produced by telemetry capture (contains manifest.json and CSV files).
    /// </summary>
    public required string CaptureDirectory { get; init; }

    /// <summary>
    /// Path to the FlowTime-Sim generated model.yaml.
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Optional FlowTime-Sim provenance JSON accompanying the model.
    /// </summary>
    public string? ProvenancePath { get; init; }

    /// <summary>
    /// Root output directory where the canonical run should be written (e.g., data/runs).
    /// </summary>
    public required string OutputRoot { get; init; }

    /// <summary>
    /// When supplied, forces the resulting run directory name; otherwise a deterministic id is generated.
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// When true, the builder will overwrite an existing run directory with the same name.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Indicates whether the canonical run id should be derived deterministically from the model spec.
    /// </summary>
    public bool DeterministicRunId { get; init; } = true;
}

/// <summary>
/// Result returned by the telemetry bundle builder.
/// </summary>
public sealed record TelemetryBundleResult(
    string RunDirectory,
    string RunId,
    TelemetryManifest TelemetryManifest);
