namespace FlowTime.Sim.Core.Models;

/// <summary>
/// Provenance metadata for tracking model generation source and parameters.
/// SIM-M2.7: Model Provenance Integration.
/// </summary>
public sealed class ProvenanceMetadata
{
    /// <summary>
    /// Source system that generated the model. Always "flowtime-sim".
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Unique model identifier. Format: model_{timestamp}_{hash}
    /// - Timestamp ensures global uniqueness
    /// - Hash ensures deterministic reproducibility for same inputs
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Template identifier used to generate this model.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Template version at time of generation.
    /// </summary>
    public required string TemplateVersion { get; init; }

    /// <summary>
    /// Human-readable template title.
    /// </summary>
    public required string TemplateTitle { get; init; }

    /// <summary>
    /// Parameter values used during generation.
    /// </summary>
    public required Dictionary<string, object> Parameters { get; init; }

    /// <summary>
    /// ISO8601 timestamp when model was generated (UTC).
    /// </summary>
    public required string GeneratedAt { get; init; }

    /// <summary>
    /// Generator version. Format: "flowtime-sim/{version}"
    /// </summary>
    public required string Generator { get; init; }

    /// <summary>
    /// Provenance schema version. Currently "1".
    /// </summary>
    public required string SchemaVersion { get; init; }
}
