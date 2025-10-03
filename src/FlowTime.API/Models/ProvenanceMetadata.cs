using System.Text.Json.Serialization;

namespace FlowTime.API.Models;

/// <summary>
/// Model provenance metadata tracking template source and generation info.
/// </summary>
public class ProvenanceMetadata
{
    /// <summary>
    /// Source system (e.g., "flowtime-sim")
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Unique model identifier from the source system
    /// </summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Template identifier this model was generated from
    /// </summary>
    [JsonPropertyName("templateId")]
    public string? TemplateId { get; set; }

    /// <summary>
    /// Template version used for generation
    /// </summary>
    [JsonPropertyName("templateVersion")]
    public string? TemplateVersion { get; set; }

    /// <summary>
    /// Human-readable template title
    /// </summary>
    [JsonPropertyName("templateTitle")]
    public string? TemplateTitle { get; set; }

    /// <summary>
    /// Timestamp when the model was generated (ISO 8601)
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    /// <summary>
    /// Timestamp when the model was received by FlowTime Engine (ISO 8601)
    /// </summary>
    [JsonPropertyName("receivedAt")]
    public string? ReceivedAt { get; set; }

    /// <summary>
    /// Generator tool and version (e.g., "flowtime-sim/0.4.0")
    /// </summary>
    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    /// <summary>
    /// Generation parameters used to create the model.
    /// NOTE: YAML numeric values are represented as strings due to serialization limitations.
    /// See docs/architecture/run-provenance.md for rationale.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Links to related artifacts
    /// </summary>
    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}
