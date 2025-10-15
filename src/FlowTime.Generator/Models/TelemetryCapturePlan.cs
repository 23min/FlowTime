namespace FlowTime.Generator.Models;

/// <summary>
/// Summary of the work the capture pipeline will perform. Used for dry-run output and downstream reporting.
/// </summary>
public sealed record TelemetryCapturePlan(
    string RunId,
    string OutputDirectory,
    IReadOnlyList<PlannedCaptureFile> Files,
    IReadOnlyList<CaptureWarning> Warnings);
