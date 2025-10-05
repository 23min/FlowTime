using System.Text.Json.Serialization;

namespace FlowTime.Contracts.Services;

/// <summary>
/// Represents an artifact in the registry
/// </summary>
public class Artifact
{
    /// <summary>
    /// Unique identifier for the artifact (e.g., run_20250921T161133Z_0bcdbb6f)
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Type of artifact: "run", "model", "telemetry"
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable title for the artifact
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// When the artifact was created
    /// </summary>
    public required DateTime Created { get; set; }

    /// <summary>
    /// User-managed tags for categorization
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Additional metadata extracted from manifest/run files
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Available files in this artifact
    /// </summary>
    public string[] Files { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Total size of all files in this artifact (bytes)
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Registry index file structure
/// </summary>
public class RegistryIndex
{
    /// <summary>
    /// Schema version for the registry format
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// When the index was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// All artifacts in the registry
    /// </summary>
    public List<Artifact> Artifacts { get; set; } = new();

    /// <summary>
    /// Number of artifacts in the registry
    /// </summary>
    [JsonPropertyName("artifactCount")]
    public int ArtifactCount { get; set; }
}

/// <summary>
/// Response for artifact listing API
/// </summary>
public class ArtifactListResponse
{
    /// <summary>
    /// List of artifacts matching the query
    /// </summary>
    public List<Artifact> Artifacts { get; set; } = new();

    /// <summary>
    /// Total number of artifacts available
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Number of artifacts returned in this response
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Artifact relationships information
/// </summary>
public class ArtifactRelationships
{
    /// <summary>
    /// The artifact ID these relationships are for
    /// </summary>
    public required string ArtifactId { get; set; }

    /// <summary>
    /// Artifacts that this artifact is derived from (e.g., model used for run)
    /// </summary>
    public List<ArtifactReference> DerivedFrom { get; set; } = new();

    /// <summary>
    /// Artifacts that are derived from this artifact (e.g., runs created from model)
    /// </summary>
    public List<ArtifactReference> Derivatives { get; set; } = new();

    /// <summary>
    /// Related artifacts (e.g., same model, similar parameters)
    /// </summary>
    public List<ArtifactReference> Related { get; set; } = new();
}

/// <summary>
/// Reference to another artifact
/// </summary>
public class ArtifactReference
{
    /// <summary>
    /// Referenced artifact ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Referenced artifact type
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Referenced artifact title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Relationship type (e.g., "derived_from", "uses_model")
    /// </summary>
    public string? RelationshipType { get; set; }
}
