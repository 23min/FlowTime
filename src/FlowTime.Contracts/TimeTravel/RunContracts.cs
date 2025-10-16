using System.Collections.Generic;
using System.Text.Json;

namespace FlowTime.Contracts.TimeTravel;

public sealed class RunCreateRequest
{
    public string TemplateId { get; init; } = string.Empty;
    public string Mode { get; init; } = "telemetry";
    public Dictionary<string, JsonElement>? Parameters { get; init; }
    public RunTelemetryOptions? Telemetry { get; init; }
    public RunCreationOptions? Options { get; init; }
}

public sealed class RunTelemetryOptions
{
    public string CaptureDirectory { get; init; } = string.Empty;
    public Dictionary<string, string> Bindings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RunCreationOptions
{
    public bool DeterministicRunId { get; init; }
    public string? RunId { get; init; }
    public bool DryRun { get; init; }
    public bool OverwriteExisting { get; init; }
}

public sealed class RunCreateResponse
{
    public bool IsDryRun { get; init; }
    public StateMetadata? Metadata { get; init; }
    public RunCreatePlan? Plan { get; init; }
    public IReadOnlyList<StateWarning> Warnings { get; init; } = Array.Empty<StateWarning>();
}

public sealed class RunSummaryResponse
{
    public IReadOnlyList<RunSummary> Items { get; init; } = Array.Empty<RunSummary>();
    public int TotalCount { get; init; }
}

public sealed class RunSummary
{
    public required string RunId { get; init; }
    public required string TemplateId { get; init; }
    public string? TemplateTitle { get; init; }
    public string? TemplateVersion { get; init; }
    public string Mode { get; init; } = "telemetry";
    public DateTimeOffset? CreatedUtc { get; init; }
    public int WarningCount { get; init; }
}

public sealed class RunCreatePlan
{
    public required string TemplateId { get; init; }
    public required string Mode { get; init; }
    public required string OutputRoot { get; init; }
    public string? CaptureDirectory { get; init; }
    public bool DeterministicRunId { get; init; }
    public string? RequestedRunId { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> TelemetryBindings { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<RunCreatePlanFile> Files { get; init; } = Array.Empty<RunCreatePlanFile>();
    public IReadOnlyList<RunCreatePlanWarning> Warnings { get; init; } = Array.Empty<RunCreatePlanWarning>();
}

public sealed class RunCreatePlanFile
{
    public required string NodeId { get; init; }
    public required string Metric { get; init; }
    public required string Path { get; init; }
}

public sealed class RunCreatePlanWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? NodeId { get; init; }
    public IReadOnlyList<int>? Bins { get; init; }
}
