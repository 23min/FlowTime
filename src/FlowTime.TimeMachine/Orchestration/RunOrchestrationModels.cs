using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.TimeTravel;
using FlowTime.TimeMachine.Artifacts;
using FlowTime.TimeMachine.Models;

namespace FlowTime.TimeMachine.Orchestration;

public sealed record RunOrchestrationRequest
{
    public required string TemplateId { get; init; }
    public string? Mode { get; init; }
    public string? CaptureDirectory { get; init; }
    public IReadOnlyDictionary<string, string>? TelemetryBindings { get; init; }
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
    public required string OutputRoot { get; init; }
    public bool DeterministicRunId { get; init; }
    public string? RunId { get; init; }
    public bool DryRun { get; init; }
    public bool OverwriteExisting { get; init; }
    public RunRngOptions? Rng { get; init; }
}

public sealed record RunOrchestrationOutcome(
    bool IsDryRun,
    RunOrchestrationResult? Result,
    RunOrchestrationPlan? Plan);

public sealed record RunOrchestrationResult(
    string RunDirectory,
    string RunId,
    string? InputHash,
    RunManifestMetadata ManifestMetadata,
    RunDocument RunDocument,
    bool TelemetrySourcesResolved,
    TelemetryManifest TelemetryManifest,
    int RngSeed,
    bool WasReused);

public sealed record RunOrchestrationPlan(
    string TemplateId,
    string Mode,
    string OutputRoot,
    string? CaptureDirectory,
    bool DeterministicRunId,
    string? RequestedRunId,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyDictionary<string, string> TelemetryBindings,
    TelemetryManifest TelemetryManifest);

public sealed record RunDocument
{
    public string? RunId { get; init; }
    public string? Source { get; init; }
    public string? Mode { get; init; }
    public string? TemplateId { get; init; }
    public string? CreatedUtc { get; init; }
    public string? InputHash { get; init; }
    public IReadOnlyList<RunWarningEntryDto> Warnings { get; init; } = Array.Empty<RunWarningEntryDto>();
}

public sealed record RunWarningEntryDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? NodeId { get; init; }
    public IReadOnlyList<int>? Bins { get; init; }
    public double? Value { get; init; }
    public string Severity { get; init; } = "warning";
}
