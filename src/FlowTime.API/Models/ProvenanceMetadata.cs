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
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Template identifier this model was generated from
    /// </summary>
    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    /// <summary>
    /// Template version used for generation
    /// </summary>
    [JsonPropertyName("template_version")]
    public string? TemplateVersion { get; set; }

    /// <summary>
    /// Timestamp when the model was generated (ISO 8601)
    /// </summary>
    [JsonPropertyName("generated_at")]
    public string? GeneratedAt { get; set; }

    /// <summary>
    /// Timestamp when the model was received by FlowTime Engine (ISO 8601)
    /// </summary>
    [JsonPropertyName("received_at")]
    public string? ReceivedAt { get; set; }

    /// <summary>
    /// Generator tool and version (e.g., "flowtime-sim/0.4.0")
    /// </summary>
    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    /// <summary>
    /// Generation parameters used to create the model.
    /// NOTE: YAML numeric values may be represented as strings due to serialization limitations.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Links to related artifacts
    /// </summary>
    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}
