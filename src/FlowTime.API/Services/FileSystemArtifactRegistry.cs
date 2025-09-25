using System.Globalization;
using System.Text.Json;
using FlowTime.API.Models;
using YamlDotNet.RepresentationModel;

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
    private static readonly SemaphoreSlim indexLock = new(1, 1);

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
        
        this.logger.LogInformation("Found {Count} run directories matching 'run_*' pattern", runDirectories.Length);
        if (runDirectories.Length == 0)
        {
            var allDirectories = Directory.GetDirectories(this.dataDirectory, "*", SearchOption.TopDirectoryOnly);
            this.logger.LogInformation("Available directories in {DataDirectory}: {Directories}", 
                this.dataDirectory, string.Join(", ", allDirectories.Select(Path.GetFileName)));
        }

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
            Artifacts = artifacts,
            ArtifactCount = artifacts.Count
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

        // Filter archived artifacts by default unless explicitly requested
        if (!options.IncludeArchived)
        {
            artifacts = artifacts.Where(a => a.Tags == null || !a.Tags.Contains("archived"));
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

    public async Task<Artifact?> ScanRunDirectoryAsync(string runDirectory)
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
            // Handle both test manifests (with "metadata" property) and real manifests (direct properties)
            if (manifestJson.TryGetProperty("metadata", out var metadataElement))
            {
                foreach (var prop in metadataElement.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.ToString();
                }
            }
            
            // Extract real manifest properties for searching
            foreach (var prop in manifestJson.EnumerateObject())
            {
                if (prop.Name != "metadata" && prop.Name != "tags") // Avoid duplicating these
                {
                    metadata[prop.Name] = prop.Value.ToString();
                }
            }
            
            // Extract tags from manifest (for test manifests)
            if (manifestJson.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                var manifestTags = tagsElement.EnumerateArray().Select(t => t.GetString()).Where(t => !string.IsNullOrEmpty(t)).Cast<string>();
                tags.AddRange(manifestTags);
            }
        }
        catch (Exception ex)
        {
            // Skip artifacts with invalid manifest.json, but log for debugging
            this.logger.LogWarning(ex, "Failed to parse manifest.json in directory: {RunDirectory}", runDirectory);
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

        // Extract meaningful title and template tags from spec.yaml if available
        string title = $"Run {parts[2]}"; // Default fallback
        try
        {
            var specPath = files.FirstOrDefault(f => Path.GetFileName(f).Equals("spec.yaml", StringComparison.OrdinalIgnoreCase));
            if (specPath != null)
            {
                this.logger.LogDebug("Processing spec.yaml for directory: {RunDirectory}", runDirectory);
                
                // Use WhenAny for proper timeout implementation
                var specContent = await File.ReadAllTextAsync(specPath);
                
                var titleTask = Task.Run(() => ExtractMeaningfulTitle(specContent, parts[2]));
                var tagsTask = Task.Run(() => ExtractTemplateTags(specContent));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                
                var titleResult = await Task.WhenAny(titleTask, timeoutTask);
                var tagsResult = await Task.WhenAny(tagsTask, timeoutTask);
                
                if (titleResult == titleTask && !titleTask.IsFaulted)
                {
                    var extractedTitle = await titleTask;
                    if (!string.IsNullOrEmpty(extractedTitle) && extractedTitle != $"Run {parts[2]}")
                    {
                        title = extractedTitle;
                        this.logger.LogDebug("Extracted title '{Title}' for directory: {RunDirectory}", title, runDirectory);
                    }
                }
                else if (titleResult == timeoutTask)
                {
                    this.logger.LogWarning("YAML title parsing timed out for directory: {RunDirectory}", runDirectory);
                }
                
                if (tagsResult == tagsTask && !tagsTask.IsFaulted)
                {
                    var templateTags = await tagsTask;
                    if (templateTags.Count > 0)
                    {
                        foreach (var tag in templateTags)
                        {
                            tags.Add(tag);
                        }
                        this.logger.LogDebug("Extracted {Count} template tags for directory: {RunDirectory}: {Tags}", templateTags.Count, runDirectory, string.Join(", ", templateTags));
                    }
                }
                else if (tagsResult == timeoutTask)
                {
                    this.logger.LogWarning("YAML tags parsing timed out for directory: {RunDirectory}", runDirectory);
                }
                
                this.logger.LogDebug("Completed processing spec.yaml for directory: {RunDirectory}", runDirectory);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to extract title from spec.yaml in directory: {RunDirectory}", runDirectory);
            // Keep default title on error
        }

        return new Artifact
        {
            Id = runId,
            Type = "run",
            Title = title,
            Created = created,
            Tags = tags.ToArray(),
            Metadata = metadata,
            Files = files.Select(f => Path.GetFileName(f) ?? f).ToArray(),
            TotalSize = totalSize,
            LastModified = lastModified
        };
    }

    private async Task<RegistryIndex> LoadIndexAsync()
    {
        await indexLock.WaitAsync();
        try
        {
            if (File.Exists(this.indexFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(this.indexFilePath);
                    return JsonSerializer.Deserialize<RegistryIndex>(json, this.jsonOptions) 
                        ?? throw new InvalidOperationException("Failed to deserialize registry index");
                }
                catch (JsonException jsonEx)
                {
                    this.logger.LogError(jsonEx, "Registry index is corrupted at {IndexPath}, rebuilding from scratch", this.indexFilePath);
                    
                    // Backup the corrupted file for investigation
                    var backupPath = $"{this.indexFilePath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                    try
                    {
                        File.Copy(this.indexFilePath, backupPath);
                        this.logger.LogInformation("Corrupted registry backed up to {BackupPath}", backupPath);
                    }
                    catch (Exception backupEx)
                    {
                        this.logger.LogWarning(backupEx, "Failed to backup corrupted registry");
                    }
                }
            }
        }
        finally
        {
            indexLock.Release();
        }
        
        // Rebuild without holding the lock (avoid deadlock)
        this.logger.LogInformation("Registry index not found or corrupted at {IndexPath}, rebuilding from scratch", this.indexFilePath);
        return await RebuildIndexAsync();
    }


    
    private async Task SaveIndexAsync(RegistryIndex index)
    {
        await indexLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(index, this.jsonOptions);
            
            // Write to temporary file first, then atomic move to prevent corruption
            var tempPath = $"{this.indexFilePath}.tmp";
            await File.WriteAllTextAsync(tempPath, json);
            
            // Atomic move
            File.Move(tempPath, this.indexFilePath, overwrite: true);
            
            this.logger.LogDebug("Registry index saved successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to save registry index to {IndexPath}", this.indexFilePath);
            throw;
        }
        finally
        {
            indexLock.Release();
        }
    }

    /// <summary>
    /// Extract meaningful title from YAML spec content with priority-based approach:
    /// 1. Parse structured metadata (title, description fields)
    /// 2. Parse comment-based descriptions from file header  
    /// 3. Extract model structure information from YAML nodes
    /// 4. Fall back to generic title with ID
    /// </summary>
    private static string ExtractMeaningfulTitle(string specContent, string fallbackId)
    {
        try
        {
            // Priority 1: Look for structured metadata fields
            using var reader = new StringReader(specContent);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode rootNode)
            {
                // Check for metadata.title or metadata.description
                if (rootNode.Children.TryGetValue(new YamlScalarNode("metadata"), out var metadataNode) && 
                    metadataNode is YamlMappingNode metadata)
                {
                    // Prefer title first
                    if (metadata.Children.TryGetValue(new YamlScalarNode("title"), out var titleNode) && 
                        titleNode is YamlScalarNode titleScalar && !string.IsNullOrWhiteSpace(titleScalar.Value))
                    {
                        return titleScalar.Value;
                    }
                    
                    // Then description
                    if (metadata.Children.TryGetValue(new YamlScalarNode("description"), out var descNode) && 
                        descNode is YamlScalarNode descScalar && !string.IsNullOrWhiteSpace(descScalar.Value))
                    {
                        // Truncate long descriptions to reasonable title length
                        var desc = descScalar.Value.Trim();
                        if (desc.Length > 80)
                        {
                            var firstSentence = desc.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            return firstSentence?.Trim() ?? desc.Substring(0, 77) + "...";
                        }
                        return desc;
                    }
                }

                // Check for top-level title or description (fallback pattern)
                if (rootNode.Children.TryGetValue(new YamlScalarNode("title"), out var topTitleNode) && 
                    topTitleNode is YamlScalarNode topTitleScalar && !string.IsNullOrWhiteSpace(topTitleScalar.Value))
                {
                    return topTitleScalar.Value;
                }
                
                if (rootNode.Children.TryGetValue(new YamlScalarNode("description"), out var topDescNode) && 
                    topDescNode is YamlScalarNode topDescScalar && !string.IsNullOrWhiteSpace(topDescScalar.Value))
                {
                    var desc = topDescScalar.Value.Trim();
                    if (desc.Length > 80)
                    {
                        var firstSentence = desc.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        return firstSentence?.Trim() ?? desc.Substring(0, 77) + "...";
                    }
                    return desc;
                }
            }

            // Priority 2: Extract meaningful title from top-level comments
            var lines = specContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("# ") && trimmedLine.Length > 2)
                {
                    var comment = trimmedLine.Substring(2).Trim();
                    // Focus on meaningful descriptions, skip generic comments
                    if (!comment.StartsWith("Generated") && 
                        !comment.StartsWith("This is") && 
                        !comment.StartsWith("File:") &&
                        !comment.ToLower().Contains("generated model") &&
                        comment.Length > 10)
                    {
                        return comment;
                    }
                }
                // Stop at first non-comment line that isn't empty
                else if (!trimmedLine.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    break;
                }
            }

            // Priority 3: Parse YAML to extract node names or model structure
            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode rootNodeForStructure)
            {
                // Look for meaningful structure patterns
                if (rootNodeForStructure.Children.TryGetValue(new YamlScalarNode("nodes"), out var nodesNode) && 
                    nodesNode is YamlSequenceNode nodeSeq)
                {
                    var nodeNames = new List<string>();
                    foreach (var nodeItem in nodeSeq.Children.OfType<YamlMappingNode>().Take(3))
                    {
                        if (nodeItem.Children.TryGetValue(new YamlScalarNode("id"), out var idNode) && 
                            idNode is YamlScalarNode idScalar && !string.IsNullOrEmpty(idScalar.Value))
                        {
                            nodeNames.Add(idScalar.Value);
                        }
                    }

                    if (nodeNames.Count > 0)
                    {
                        return $"Model with {string.Join(", ", nodeNames)}";
                    }
                }
                
                // Fallback: Look for any nodes structure pattern
                if (rootNodeForStructure.Children.TryGetValue(new YamlScalarNode("nodes"), out var fallbackNodesNode) && 
                    fallbackNodesNode is YamlMappingNode nodes)
                {
                    var nodeNames = nodes.Children.Keys.OfType<YamlScalarNode>()
                        .Select(k => k.Value)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Take(3)
                        .ToList();

                    if (nodeNames.Count > 0)
                    {
                        return $"Model with {string.Join(", ", nodeNames)}";
                    }
                }
            }

            return $"Run {fallbackId}";
        }
        catch (Exception)
        {
            return $"Run {fallbackId}";
        }
    }

    private static List<string> ExtractTemplateTags(string specContent)
    {
        var tags = new List<string>();
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(specContent));

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode rootNode)
            {
                // Look for metadata section
                if (rootNode.Children.TryGetValue(new YamlScalarNode("metadata"), out var metadataNode) && 
                    metadataNode is YamlMappingNode metadataMapping)
                {
                    // Extract tags from metadata
                    if (metadataMapping.Children.TryGetValue(new YamlScalarNode("tags"), out var tagsNode))
                    {
                        if (tagsNode is YamlSequenceNode tagsSequence)
                        {
                            // Handle array format: tags: [tag1, tag2, tag3]
                            foreach (var tagNode in tagsSequence.Children.OfType<YamlScalarNode>())
                            {
                                var tag = tagNode.Value?.Trim();
                                if (!string.IsNullOrEmpty(tag))
                                {
                                    tags.Add(tag);
                                }
                            }
                        }
                        else if (tagsNode is YamlScalarNode tagsScalar)
                        {
                            // Handle string format: tags: [tag1, tag2, tag3] - parse as string
                            var tagsString = tagsScalar.Value?.Trim();
                            if (!string.IsNullOrEmpty(tagsString))
                            {
                                // Remove brackets and split by comma
                                tagsString = tagsString.Trim('[', ']');
                                var individualTags = tagsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                foreach (var tag in individualTags)
                                {
                                    var cleanTag = tag.Trim();
                                    if (!string.IsNullOrEmpty(cleanTag))
                                    {
                                        tags.Add(cleanTag);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore parsing errors - just return whatever tags we could extract
        }

        return tags;
    }
}