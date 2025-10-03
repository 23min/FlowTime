using FlowTime.Core;

namespace FlowTime.Adapters.Synthetic;

/// <summary>
/// High-level adapter for working with run artifacts
/// Provides convenient access to series data with proper grid alignment
/// </summary>
public sealed class RunArtifactAdapter
{
    private readonly ISeriesReader reader;
    private readonly string runPath;
    private RunManifest? cachedManifest;
    private SeriesIndex? cachedIndex;

    public RunArtifactAdapter(ISeriesReader reader, string runPath)
    {
        this.reader = reader;
        this.runPath = runPath;
    }

    /// <summary>
    /// Get the run manifest, cached after first load
    /// </summary>
    public async Task<RunManifest> GetManifestAsync()
    {
        cachedManifest ??= await reader.ReadRunInfoAsync(runPath);
        return cachedManifest;
    }

    /// <summary>
    /// Get the series index, cached after first load
    /// </summary>
    public async Task<SeriesIndex> GetIndexAsync()
    {
        cachedIndex ??= await reader.ReadIndexAsync(runPath);
        return cachedIndex;
    }

    /// <summary>
    /// Get a specific series by ID
    /// </summary>
    public async Task<Series> GetSeriesAsync(string seriesId)
    {
        return await reader.ReadSeriesAsync(runPath, seriesId);
    }

    /// <summary>
    /// Get multiple series by ID, handling missing optional series gracefully
    /// </summary>
    public async Task<Dictionary<string, Series?>> GetSeriesAsync(params string[] seriesIds)
    {
        var result = new Dictionary<string, Series?>();
        
        foreach (var seriesId in seriesIds)
        {
            if (reader.SeriesExists(runPath, seriesId))
            {
                result[seriesId] = await reader.ReadSeriesAsync(runPath, seriesId);
            }
            else
            {
                result[seriesId] = null;
            }
        }

        return result;
    }

    /// <summary>
    /// Get all available series for a specific component
    /// </summary>
    public async Task<Dictionary<string, Series>> GetComponentSeriesAsync(string componentId)
    {
        var index = await GetIndexAsync();
        var componentSeries = index.Series
            .Where(s => s.ComponentId == componentId)
            .ToArray();

        var result = new Dictionary<string, Series>();
        foreach (var seriesMetadata in componentSeries)
        {
            if (reader.SeriesExists(runPath, seriesMetadata.Id))
            {
                result[seriesMetadata.Id] = await reader.ReadSeriesAsync(runPath, seriesMetadata.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Validate that the run artifacts are consistent
    /// </summary>
    public async Task<ValidationResult> ValidateAsync()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var manifest = await GetManifestAsync();
            var index = await GetIndexAsync();

            // Check schema version compatibility
            if (manifest.SchemaVersion < 1 || manifest.SchemaVersion > 2)
            {
                warnings.Add($"Unsupported schema version {manifest.SchemaVersion}, expected 1-2");
            }

            // Check grid consistency
            if (manifest.Grid.Bins != index.Grid.Bins || 
                manifest.Grid.BinMinutes != index.Grid.BinMinutes)
            {
                errors.Add("Grid mismatch between run.json and series/index.json");
            }

            // Check series references
            var manifestSeriesIds = manifest.Series.Select(s => s.Id).ToHashSet();
            var indexSeriesIds = index.Series.Select(s => s.Id).ToHashSet();

            foreach (var seriesId in manifestSeriesIds)
            {
                if (!indexSeriesIds.Contains(seriesId))
                {
                    warnings.Add($"Series {seriesId} referenced in run.json but not found in index.json");
                }
            }

            // Check that series files exist and have correct length
            foreach (var seriesMetadata in index.Series)
            {
                if (reader.SeriesExists(runPath, seriesMetadata.Id))
                {
                    try
                    {
                        var series = await reader.ReadSeriesAsync(runPath, seriesMetadata.Id);
                        if (series.Length != seriesMetadata.Points)
                        {
                            errors.Add($"Series {seriesMetadata.Id} has {series.Length} points but index.json says {seriesMetadata.Points}");
                        }
                        if (series.Length != manifest.Grid.Bins)
                        {
                            errors.Add($"Series {seriesMetadata.Id} has {series.Length} points but grid has {manifest.Grid.Bins} bins");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to read series {seriesMetadata.Id}: {ex.Message}");
                    }
                }
                else
                {
                    warnings.Add($"Series file missing: {seriesMetadata.Id}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to read artifacts: {ex.Message}");
        }

        return new ValidationResult(errors.ToArray(), warnings.ToArray());
    }

    /// <summary>
    /// Get a FlowTime.Core.TimeGrid from the run artifacts
    /// </summary>
    public async Task<FlowTime.Core.TimeGrid> GetCoreTimeGridAsync()
    {
        var manifest = await GetManifestAsync();
        var binUnit = manifest.Grid.BinUnit.ToLowerInvariant() switch
        {
            "minutes" => FlowTime.Core.TimeUnit.Minutes,
            "hours" => FlowTime.Core.TimeUnit.Hours,
            "days" => FlowTime.Core.TimeUnit.Days,
            _ => throw new ArgumentException($"Unknown time unit: {manifest.Grid.BinUnit}")
        };
        return new FlowTime.Core.TimeGrid(manifest.Grid.Bins, manifest.Grid.BinSize, binUnit);
    }
}

/// <summary>
/// Result of artifact validation
/// </summary>
public sealed class ValidationResult
{
    public string[] Errors { get; }
    public string[] Warnings { get; }
    public bool IsValid => Errors.Length == 0;

    public ValidationResult(string[] errors, string[] warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }
}
