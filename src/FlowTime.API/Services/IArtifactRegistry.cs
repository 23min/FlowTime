using FlowTime.API.Models;

namespace FlowTime.API.Services;

/// <summary>
/// Interface for artifact registry operations
/// Designed to be pluggable for future database migration
/// </summary>
public interface IArtifactRegistry
{
    /// <summary>
    /// Scan the artifacts directory and rebuild the registry index
    /// </summary>
    Task<RegistryIndex> RebuildIndexAsync();

    /// <summary>
    /// Get all artifacts matching the specified filters
    /// </summary>
    Task<ArtifactListResponse> GetArtifactsAsync(ArtifactQueryOptions? options = null);

    /// <summary>
    /// Get a specific artifact by ID
    /// </summary>
    Task<Artifact?> GetArtifactAsync(string id);

    /// <summary>
    /// Add or update an artifact in the registry
    /// </summary>
    Task AddOrUpdateArtifactAsync(Artifact artifact);

    /// <summary>
    /// Remove an artifact from the registry
    /// </summary>
    Task RemoveArtifactAsync(string id);

    /// <summary>
    /// Get artifacts related to the specified artifact
    /// </summary>
    Task<ArtifactRelationships> GetArtifactRelationshipsAsync(string id);
}

/// <summary>
/// Options for querying artifacts with enhanced filtering capabilities
/// </summary>
public class ArtifactQueryOptions
{
    /// <summary>
    /// Filter by artifact type
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Search in title and metadata
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Filter by tags (any of the specified tags)
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Skip this many artifacts (for pagination)
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Take at most this many artifacts (for pagination, max 1000)
    /// </summary>
    public int Limit { get; set; } = 50;

    /// <summary>
    /// Sort field (id, created, title, size)
    /// </summary>
    public string SortBy { get; set; } = "created";

    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortOrder { get; set; } = "desc";

    // Enhanced filtering options for M2.8

    /// <summary>
    /// Filter artifacts created after this date
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Filter artifacts created before this date
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Filter by minimum file size in bytes
    /// </summary>
    public long? MinFileSize { get; set; }

    /// <summary>
    /// Filter by maximum file size in bytes
    /// </summary>
    public long? MaxFileSize { get; set; }

    /// <summary>
    /// Full-text search across all artifact content and metadata
    /// </summary>
    public string? FullTextSearch { get; set; }

    /// <summary>
    /// Find artifacts related to the specified artifact ID
    /// </summary>
    public string? RelatedToArtifact { get; set; }
}