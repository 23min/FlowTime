using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;

namespace FlowTime.Generator.Orchestration;

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
    RunManifestMetadata ManifestMetadata,
    RunDocument RunDocument,
    bool TelemetrySourcesResolved,
    TelemetryManifest TelemetryManifest,
    int RngSeed);

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
}
