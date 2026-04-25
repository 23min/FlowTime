using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlowTime.Core.Models;
using YamlDotNet.Serialization;

namespace FlowTime.Contracts.Dtos;

/// <summary>
/// RNG configuration for FlowTime-Sim (ignored by FlowTime Engine)
/// </summary>
public sealed class RngDto
{
    public string Kind { get; set; } = "pcg32";
    public int? Seed { get; set; }
}

/// <summary>
/// Root model definition for YAML deserialization
/// </summary>
public sealed class ModelDto
{
    public int? SchemaVersion { get; set; }
    public GridDto Grid { get; set; } = new();
    public List<ClassDto> Classes { get; set; } = new();
    public TrafficDto? Traffic { get; set; }
    public List<NodeDto> Nodes { get; set; } = new();
    public List<OutputDto> Outputs { get; set; } = new();
    public RngDto? Rng { get; set; }
    public TopologyDto? Topology { get; set; }

    /// <summary>
    /// Provenance block (forensic / reproducibility — not load-bearing for evaluation).
    /// Nested camelCase shape ratified in m-E24-01 (Q5/A4): seven fields, parameters as
    /// nested map. Replaces the satellite SimProvenance type that is being deleted alongside
    /// SimModelArtifact in m-E24-02. Nullable so callers that author models without
    /// provenance (e.g. tests, CLI snippets) do not have to construct an empty block.
    /// </summary>
    public ProvenanceDto? Provenance { get; set; }
}

/// <summary>
/// Grid definition specifying time bins and duration
/// </summary>
public sealed class GridDto
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public string? StartTimeUtc { get; set; }

    [YamlMember(Alias = "start", ApplyNamingConventions = false)]
    public string? LegacyStart
    {
        get => StartTimeUtc;
        set => StartTimeUtc = value;
    }

    public bool ShouldSerializeLegacyStart() => false;
}

/// <summary>
/// Node definition for different node types (const, expr, pmf, etc.)
/// </summary>
public sealed class NodeDto
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
    public PmfDto? Pmf { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    // For serviceWithBuffer nodes
    public string? Inflow { get; set; }
    public string? Outflow { get; set; }
    public string? Loss { get; set; }
    public DispatchScheduleDto? DispatchSchedule { get; set; }
    // For router nodes
    public RouterInputsDto? Inputs { get; set; }
    public List<RouterRouteDto>? Routes { get; set; }
}

public sealed class PmfDto
{
    public double[] Values { get; set; } = Array.Empty<double>();
    public double[] Probabilities { get; set; } = Array.Empty<double>();
}

public sealed class ClassDto
{
    public string Id { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}

public sealed class TrafficDto
{
    public List<ArrivalDto> Arrivals { get; set; } = new();
}

public sealed class ArrivalDto
{
    public string NodeId { get; set; } = string.Empty;
    public string? ClassId { get; set; }
    public ArrivalPatternDto Pattern { get; set; } = new();
}

public sealed class ArrivalPatternDto
{
    public string Kind { get; set; } = string.Empty;
    public double? RatePerBin { get; set; }
    public double? Rate { get; set; }
}

/// <summary>
/// Output definition for CSV generation.
/// `as` is optional under the m-E24-01 Q3 decision: presence means "also export this
/// series as CSV"; auto-added outputs (e.g. EnsureSemanticsOutputs) emit no `as` field.
/// `exclude` mirrors the Sim-side SimOutput.Exclude shape so the unified DTO covers
/// every Sim-emitted output column without a satellite type.
/// </summary>
public sealed class OutputDto
{
    public string Series { get; set; } = "";
    public List<string>? Exclude { get; set; }
    public string? As { get; set; }
}

public sealed class TopologyDto
{
    public List<TopologyNodeDto> Nodes { get; set; } = new();
    public List<TopologyEdgeDto> Edges { get; set; } = new();
    public List<TopologyConstraintDto> Constraints { get; set; } = new();
}

public sealed class TopologyNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "service";
    public string? NodeRole { get; set; }
    public string? Group { get; set; }
    public UiHintsDto? Ui { get; set; }
    public List<string>? Constraints { get; set; }
    public DispatchScheduleDto? DispatchSchedule { get; set; }
    public TopologySemanticsDto Semantics { get; set; } = new();
    public TopologyInitialConditionDto? InitialCondition { get; set; }
}

public sealed class TopologySemanticsDto
{
    public string Arrivals { get; set; } = string.Empty;
    public string Served { get; set; } = string.Empty;
    public string Errors { get; set; } = string.Empty;
    public string? Attempts { get; set; }
    public string? Failures { get; set; }
    public string? ExhaustedFailures { get; set; }
    public string? RetryEcho { get; set; }
    public string? RetryBudgetRemaining { get; set; }
    public double[]? RetryKernel { get; set; }
    public string? ExternalDemand { get; set; }
    // canonical queue binding (no legacy 'queue' alias)
    public string? QueueDepth { get; set; }
    public string? Capacity { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ParallelismReference? Parallelism { get; set; }
    public string? ProcessingTimeMsSum { get; set; }
    public string? ServedCount { get; set; }
    public double? SlaMin { get; set; }
    public double? MaxAttempts { get; set; }
    public string? BackoffStrategy { get; set; }
    public string? ExhaustedPolicy { get; set; }
    public Dictionary<string, string>? Aliases { get; set; }
}

public sealed class TopologyInitialConditionDto
{
    public double QueueDepth { get; set; }
}

public sealed class TopologyEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Type { get; set; }
    public string? Measure { get; set; }
    public double? Multiplier { get; set; }
    public int? Lag { get; set; }
}

public sealed class TopologyConstraintDto
{
    public string Id { get; set; } = string.Empty;
    public ConstraintSemanticsDto Semantics { get; set; } = new();
}

public sealed class ConstraintSemanticsDto
{
    public string Arrivals { get; set; } = string.Empty;
    public string Served { get; set; } = string.Empty;
    public string? Errors { get; set; }
    public string? LatencyMinutes { get; set; }
}

public sealed class UiHintsDto
{
    public double? X { get; set; }
    public double? Y { get; set; }
}

public sealed class RouterInputsDto
{
    public string? Queue { get; set; }
}

public sealed class RouterRouteDto
{
    public string Target { get; set; } = string.Empty;
    public string[]? Classes { get; set; }
    public double? Weight { get; set; }
}

public sealed class DispatchScheduleDto
{
    public string Kind { get; set; } = "time-based";
    public int PeriodBins { get; set; }
    public int? PhaseOffset { get; set; }
    public string? CapacitySeries { get; set; }
}

/// <summary>
/// Provenance block embedded in the unified post-substitution model.
/// Seven camelCase fields, ratified in m-E24-01 (Q5/A4). Source / schemaVersion are
/// explicitly excluded — `source` collapsed into `generator`, root already carries
/// `schemaVersion`. Provenance is forensic data: no Engine runtime consumer reads it,
/// but Sim and downstream tooling (TelemetryManifest construction in
/// RunOrchestrationService) depend on the shape for reproducibility and lookup.
/// </summary>
public sealed class ProvenanceDto
{
    /// <summary>Producing system identifier, e.g. "flowtime-sim".</summary>
    public string Generator { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC timestamp at which the model was rendered from its template.</summary>
    public string GeneratedAt { get; set; } = string.Empty;

    /// <summary>Template identifier (e.g. "transportation-basic").</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Template version at render time. Enables version-pinned regeneration.</summary>
    public string TemplateVersion { get; set; } = string.Empty;

    /// <summary>Template mode — "simulation" or "telemetry". Survives here because it is
    /// a model-generation input, not a model-runtime field.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Stable identifier for this rendered model. Distinguishes two runs of the
    /// same template with different parameter values.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Template parameter values at render time, serialized as a nested map.
    /// Schema declares additionalProperties: true — values are forensic, not a typed
    /// contract. Nullable so empty parameter sets serialize as YAML omission via
    /// `DefaultValuesHandling.OmitNull` (see D-m-E24-02-03).</summary>
    public Dictionary<string, object?>? Parameters { get; set; }
}
