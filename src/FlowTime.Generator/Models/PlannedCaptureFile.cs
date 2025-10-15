namespace FlowTime.Generator.Models;

/// <summary>
/// Represents a single telemetry file that will be produced by the capture pipeline.
/// </summary>
public sealed record PlannedCaptureFile(
    string NodeId,
    TelemetryMetricKind Metric,
    string SourceSeriesId,
    string TargetFileName);
