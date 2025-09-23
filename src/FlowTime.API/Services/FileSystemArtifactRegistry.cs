using System.Globalization;
using System.Text.Json;
using FlowTime.API.Models;

namespace FlowTime.API.Services;

/// <summary>
/// File-based artifact registry implementation
/// Uses JSON index file for fast lookups and scans directories for artifacts
/// </summary>
public class FileSystemArtifactRegistry : IArtifactRegistry
{
    private readonly string dataDirectory;
    private readonly string indexFilePath;
    private readonly ILogger<FileSystemArtifactRegistry> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public FileSystemArtifactRegistry(IConfiguration configuration, ILogger<FileSystemArtifactRegistry> logger)
    {
        this.dataDirectory = configuration.GetValue<string>("ArtifactsDirectory") ?? configuration.GetValue<string>("DataDirectory") ?? "data";
        this.indexFilePath = Path.Combine(this.dataDirectory, "registry-index.json");
        this.logger = logger;
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Ensure data directory exists
        Directory.CreateDirectory(this.dataDirectory);
    }

    public async Task<RegistryIndex> RebuildIndexAsync()
    {
        this.logger.LogInformation("Rebuilding artifact registry index from directory: {DataDirectory}", this.dataDirectory);

        var artifacts = new List<Artifact>();
        var runDirectories = Directory.GetDirectories(this.dataDirectory, "run_*", SearchOption.TopDirectoryOnly);

        foreach (var runDir in runDirectories)
        {
            try
            {
                var artifact = await ScanRunDirectoryAsync(runDir);
                if (artifact != null)
                {
                    artifacts.Add(artifact);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to scan run directory: {RunDirectory}", runDir);
            }
        }

        var index = new RegistryIndex
        {
            SchemaVersion = 1,
            LastUpdated = DateTime.UtcNow,
            Artifacts = artifacts
        };

        await SaveIndexAsync(index);
        this.logger.LogInformation("Registry index rebuilt with {Count} artifacts", artifacts.Count);

        return index;
    }

    public async Task<ArtifactListResponse> GetArtifactsAsync(ArtifactQueryOptions? options = null)
    {
        options ??= new ArtifactQueryOptions();

        var index = await LoadIndexAsync();
        var artifacts = index.Artifacts.AsEnumerable();

        // Apply filters
        if (!string.IsNullOrEmpty(options.Type))
        {
            artifacts = artifacts.Where(a => a.Type.Equals(options.Type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            var searchLower = options.Search.ToLowerInvariant();
            artifacts = artifacts.Where(a => 
                a.Title.ToLowerInvariant().Contains(searchLower) ||
                (a.Metadata != null && a.Metadata.Values.Any(v => v.ToString()?.ToLowerInvariant().Contains(searchLower) == true)));
        }

        if (options.Tags != null && options.Tags.Length > 0)
        {
            artifacts = artifacts.Where(a => a.Tags != null && a.Tags.Any(tag => options.Tags.Contains(tag)));
        }

        // Enhanced M2.8 filters
        if (options.CreatedAfter.HasValue)
        {
            artifacts = artifacts.Where(a => a.Created >= options.CreatedAfter.Value);
        }

        if (options.CreatedBefore.HasValue)
        {
            artifacts = artifacts.Where(a => a.Created <= options.CreatedBefore.Value);
        }

        if (options.MinFileSize.HasValue)
        {
            artifacts = artifacts.Where(a => a.TotalSize >= options.MinFileSize.Value);
        }

        if (options.MaxFileSize.HasValue)
        {
            artifacts = artifacts.Where(a => a.TotalSize <= options.MaxFileSize.Value);
        }

        if (!string.IsNullOrEmpty(options.FullTextSearch))
        {
            var searchLower = options.FullTextSearch.ToLowerInvariant();
            artifacts = artifacts.Where(a => 
                a.Title.ToLowerInvariant().Contains(searchLower) ||
                a.Id.ToLowerInvariant().Contains(searchLower) ||
                (a.Tags != null && a.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchLower))) ||
                (a.Metadata != null && a.Metadata.Values.Any(v => v.ToString()?.ToLowerInvariant().Contains(searchLower) == true)) ||
                (a.Files != null && a.Files.Any(f => f.ToLowerInvariant().Contains(searchLower))));
        }

        if (!string.IsNullOrEmpty(options.RelatedToArtifact))
        {
            // Find artifacts with similar characteristics to the specified artifact
            var relatedTo = index.Artifacts.FirstOrDefault(a => a.Id == options.RelatedToArtifact);
            if (relatedTo != null)
            {
                artifacts = artifacts.Where(a => a.Id != options.RelatedToArtifact && (
                    a.Type == relatedTo.Type ||
                    (a.Tags != null && relatedTo.Tags != null && a.Tags.Intersect(relatedTo.Tags).Any()) ||
                    Math.Abs((a.Created - relatedTo.Created).TotalHours) < 24));
            }
        }

        // Enhanced sorting with new fields
        artifacts = options.SortBy.ToLowerInvariant() switch
        {
            "title" => options.SortOrder.ToLowerInvariant() == "asc" 
                ? artifacts.OrderBy(a => a.Title) 
                : artifacts.OrderByDescending(a => a.Title),
            "id" => options.SortOrder.ToLowerInvariant() == "asc" 
                ? artifacts.OrderBy(a => a.Id) 
                : artifacts.OrderByDescending(a => a.Id),
            "size" => options.SortOrder.ToLowerInvariant() == "asc" 
                ? artifacts.OrderBy(a => a.TotalSize) 
                : artifacts.OrderByDescending(a => a.TotalSize),
            "modified" => options.SortOrder.ToLowerInvariant() == "asc" 
                ? artifacts.OrderBy(a => a.LastModified) 
                : artifacts.OrderByDescending(a => a.LastModified),
            _ => options.SortOrder.ToLowerInvariant() == "asc" 
                ? artifacts.OrderBy(a => a.Created) 
                : artifacts.OrderByDescending(a => a.Created)
        };

        var totalCount = artifacts.Count();
        
        // M2.8: Support up to 1000 artifacts per page for large collections
        var effectiveLimit = Math.Min(options.Limit, 1000);
        var pagedArtifacts = artifacts.Skip(options.Skip).Take(effectiveLimit).ToList();

        return new ArtifactListResponse
        {
            Artifacts = pagedArtifacts,
            Total = totalCount,
            Count = pagedArtifacts.Count
        };
    }

    public async Task<Artifact?> GetArtifactAsync(string id)
    {
        var index = await LoadIndexAsync();
        return index.Artifacts.FirstOrDefault(a => a.Id == id);
    }

    public async Task AddOrUpdateArtifactAsync(Artifact artifact)
    {
        var index = await LoadIndexAsync();
        var artifacts = index.Artifacts;

        var existingIndex = artifacts.FindIndex(a => a.Id == artifact.Id);
        if (existingIndex >= 0)
        {
            artifacts[existingIndex] = artifact;
        }
        else
        {
            artifacts.Add(artifact);
        }

        index.LastUpdated = DateTime.UtcNow;

        await SaveIndexAsync(index);
    }

    public async Task RemoveArtifactAsync(string id)
    {
        var index = await LoadIndexAsync();
        index.Artifacts.RemoveAll(a => a.Id == id);
        index.LastUpdated = DateTime.UtcNow;

        await SaveIndexAsync(index);
    }

    public async Task<ArtifactRelationships> GetArtifactRelationshipsAsync(string id)
    {
        var index = await LoadIndexAsync();
        var artifact = index.Artifacts.FirstOrDefault(a => a.Id == id);
        
        if (artifact == null)
        {
            throw new ArgumentException($"Artifact with ID '{id}' not found", nameof(id));
        }

        var relationships = new ArtifactRelationships
        {
            ArtifactId = id
        };

        // For run artifacts, find related models and other runs
        if (artifact.Type == "run")
        {
            // Find model artifacts that might be related (same time period, similar tags)
            var potentialModels = index.Artifacts
                .Where(a => a.Type == "model" && Math.Abs((a.Created - artifact.Created).TotalHours) < 24)
                .Take(5);

            foreach (var model in potentialModels)
            {
                relationships.DerivedFrom.Add(new ArtifactReference
                {
                    Id = model.Id,
                    Type = model.Type,
                    Title = model.Title,
                    RelationshipType = "model-source"
                });
            }

            // Find other runs with similar characteristics
            var similarRuns = index.Artifacts
                .Where(a => a.Type == "run" && a.Id != id)
                .Where(a => a.Tags.Intersect(artifact.Tags).Any() || 
                           Math.Abs((a.Created - artifact.Created).TotalHours) < 1)
                .Take(5);

            foreach (var run in similarRuns)
            {
                relationships.Related.Add(new ArtifactReference
                {
                    Id = run.Id,
                    Type = run.Type,
                    Title = run.Title,
                    RelationshipType = "similar-run"
                });
            }
        }

        return relationships;
    }

    private async Task<Artifact?> ScanRunDirectoryAsync(string runDirectory)
    {
        var dirName = Path.GetFileName(runDirectory);
        if (!dirName.StartsWith("run_"))
        {
            return null;
        }

        // Extract timestamp and ID from directory name: run_YYYYMMDDTHHMMSSZ_<id>
        var parts = dirName.Split('_');
        if (parts.Length != 3)
        {
            return null;
        }

        var timestampStr = parts[1];
        var runId = dirName; // Use full directory name as ID

        if (!DateTime.TryParseExact(timestampStr, "yyyyMMddTHHmmssZ", 
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
        {
            return null;
        }

        var metadata = new Dictionary<string, object>();
        var tags = new List<string> { "run" };

        // Look for specific files to extract metadata
        var files = Directory.GetFiles(runDirectory);
        
        // M2.8: Calculate total size and last modified time
        long totalSize = 0;
        DateTime lastModified = created;
        
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
                if (fileInfo.LastWriteTimeUtc > lastModified)
                {
                    lastModified = fileInfo.LastWriteTimeUtc;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to get file info for: {FilePath}", file);
            }
        }
        
        // Validate that this is a proper run directory
        // For M2.7, we require manifest.json for proper artifacts
        var hasManifest = files.Any(f => Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
        
        // Skip directories without manifest.json
        if (!hasManifest)
        {
            return null;
        }
        
        // Try to validate manifest.json and extract metadata
        try
        {
            var manifestPath = files.First(f => Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
            var manifestContent = await File.ReadAllTextAsync(manifestPath);
            var manifestJson = JsonSerializer.Deserialize<JsonElement>(manifestContent); // Will throw if invalid JSON
            
            // M2.8: Extract metadata from manifest for enhanced searching
            if (manifestJson.TryGetProperty("metadata", out var metadataElement))
            {
                foreach (var prop in metadataElement.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.ToString();
                }
            }
            
            // Extract additional manifest properties for search
            if (manifestJson.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                var manifestTags = tagsElement.EnumerateArray().Select(t => t.GetString()).Where(t => !string.IsNullOrEmpty(t));
                tags.AddRange(manifestTags!);
            }
        }
        catch (Exception)
        {
            // Skip artifacts with invalid manifest.json
            return null;
        }
        
        // Check for CSV output file
        var csvFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".csv", StringComparison.OrdinalIgnoreCase));
        if (csvFile != null)
        {
            metadata["outputFile"] = Path.GetFileName(csvFile);
            metadata["outputFormat"] = "csv";
            tags.Add("csv");
        }

        // Check for JSON output file
        var jsonFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase));
        if (jsonFile != null)
        {
            metadata["outputFile"] = Path.GetFileName(jsonFile);
            metadata["outputFormat"] = "json";
            tags.Add("json");
        }

        // Look for metadata files (model definition, config, etc.)
        var yamlFiles = files.Where(f => Path.GetExtension(f).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                         Path.GetExtension(f).Equals(".yml", StringComparison.OrdinalIgnoreCase));
        if (yamlFiles.Any())
        {
            metadata["configFiles"] = yamlFiles.Select(Path.GetFileName).ToArray();
            tags.Add("configured");
        }

        metadata["directoryPath"] = runDirectory;
        metadata["fileCount"] = files.Length;
        metadata["totalSizeBytes"] = totalSize;

        return new Artifact
        {
            Id = runId,
            Type = "run",
            Title = $"Run {parts[2]}", // Use just the suffix for title
            Created = created,
            Tags = tags.ToArray(),
            Metadata = metadata,
            Files = files.Select(Path.GetFileName).ToArray(),
            TotalSize = totalSize,
            LastModified = lastModified
        };
    }

    private async Task<RegistryIndex> LoadIndexAsync()
    {
        if (!File.Exists(this.indexFilePath))
        {
            throw new FileNotFoundException($"Registry index not found at {this.indexFilePath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(this.indexFilePath);
            return JsonSerializer.Deserialize<RegistryIndex>(json, this.jsonOptions) 
                ?? throw new InvalidOperationException("Failed to deserialize registry index");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load registry index from {IndexPath}", this.indexFilePath);
            throw;
        }
    }

    private async Task SaveIndexAsync(RegistryIndex index)
    {
        try
        {
            var json = JsonSerializer.Serialize(index, this.jsonOptions);
            await File.WriteAllTextAsync(this.indexFilePath, json);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to save registry index to {IndexPath}", this.indexFilePath);
            throw;
        }
    }
}