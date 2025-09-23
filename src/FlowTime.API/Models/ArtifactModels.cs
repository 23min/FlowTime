using System.Text.Json.Serialization;

namespace FlowTime.API.Models;

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