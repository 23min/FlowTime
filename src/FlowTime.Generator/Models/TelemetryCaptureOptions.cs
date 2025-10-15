using FlowTime.Generator.Processing;

namespace FlowTime.Generator.Models;

/// <summary>
/// Configuration for the telemetry capture pipeline.
/// </summary>
public sealed class TelemetryCaptureOptions
{
    public required string RunDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    /// <summary>
    /// When true, the pipeline computes the work to perform but does not write any files.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gap &amp; anomaly handling strategy.
    /// </summary>
    public GapInjectorOptions GapOptions { get; init; } = GapInjectorOptions.Default;
}
