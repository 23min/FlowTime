using FlowTime.Core;

namespace FlowTime.Adapters.Synthetic;

/// <summary>
/// Interface for reading FlowTime/Sim file artifacts
/// </summary>
public interface ISeriesReader
{
    /// <summary>
    /// Read the run metadata from run.json
    /// </summary>
    Task<RunManifest> ReadRunInfoAsync(string runPath);

    /// <summary>
    /// Read the deterministic manifest from manifest.json
    /// </summary>
    Task<DeterministicManifest> ReadManifestAsync(string runPath);

    /// <summary>
    /// Read the series index from series/index.json
    /// </summary>
    Task<SeriesIndex> ReadIndexAsync(string runPath);

    /// <summary>
    /// Read a specific series CSV file
    /// </summary>
    Task<Series> ReadSeriesAsync(string runPath, string seriesId);

    /// <summary>
    /// Check if a series file exists
    /// </summary>
    bool SeriesExists(string runPath, string seriesId);
}
