using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowTime.Adapters.Synthetic;
using FlowTime.Core;

namespace FlowTime.API.Services;

/// <summary>
/// Exports FlowTime run data to NDJSON (Newline Delimited JSON) format for streaming/API consumption.
/// Each line is a separate JSON object with telemetry data point.
/// </summary>
public static class NdjsonExporter
{
    public record ExportOptions
    {
        /// <summary>
        /// Start time for the time_bin column (ISO 8601 format)
        /// </summary>
        public DateTime StartTime { get; init; } = DateTime.UtcNow.Date;
        
        /// <summary>
        /// Duration of each bin in minutes
        /// </summary>
        public int BinMinutes { get; init; } = 1;
        
        /// <summary>
        /// Timezone for time_bin formatting (defaults to UTC)
        /// </summary>
        public string Timezone { get; init; } = "UTC";
    }
    
    public record ExportResult
    {
        public required string NdjsonContent { get; init; }
        public required int RowCount { get; init; }
        public required int SeriesCount { get; init; }
        public required string ContentHash { get; init; }
    }
    
    /// <summary>
    /// Export run data from artifact directory to NDJSON format
    /// </summary>
    public static async Task<ExportResult> ExportFromRunDirectoryAsync(
        string runDirectory, 
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        
        var reader = new FileSeriesReader();
        var adapter = new RunArtifactAdapter(reader, runDirectory);
        var seriesIndex = await adapter.GetIndexAsync();
        
        var ndjsonBuilder = new StringBuilder();
        var totalRows = 0;
        var seriesCount = 0;
        
        foreach (var seriesInfo in seriesIndex.Series)
        {
            seriesCount++;
            
            // Extract component_id and measure from series ID
            // Series ID format: "measure@COMPONENT_ID@DEFAULT"
            var parts = seriesInfo.Id.Split('@');
            if (parts.Length < 2)
            {
                continue; // Skip malformed series IDs
            }
            
            var measure = parts[0];
            var componentId = parts[1];
            
            try
            {
                // Get the series data as a Series object
                var series = await adapter.GetSeriesAsync(seriesInfo.Id);
                var values = series.ToArray();
                
                // Process each time bin in the series
                for (int timeBin = 0; timeBin < values.Length; timeBin++)
                {
                    var value = values[timeBin];
                    
                    // Calculate time_bin timestamp
                    var timeBinTimestamp = options.StartTime.AddMinutes(timeBin * options.BinMinutes);
                    var timeBinStr = timeBinTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                    
                    // Create JSON object for this data point
                    var dataPoint = new
                    {
                        time_bin = timeBinStr,
                        component_id = componentId,
                        measure = measure,
                        value = value
                    };
                    
                    // Serialize to JSON and add newline (NDJSON format)
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var jsonLine = JsonSerializer.Serialize(dataPoint, jsonOptions);
                    ndjsonBuilder.AppendLine(jsonLine);
                    
                    totalRows++;
                }
            }
            catch (Exception)
            {
                // Skip series that can't be read
                continue;
            }
        }
        
        var ndjsonContent = ndjsonBuilder.ToString();
        
        // Calculate content hash for determinism
        var contentBytes = Encoding.UTF8.GetBytes(ndjsonContent);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        var contentHash = "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return new ExportResult
        {
            NdjsonContent = ndjsonContent,
            RowCount = totalRows,
            SeriesCount = seriesCount,
            ContentHash = contentHash
        };
    }
    
    /// <summary>
    /// Export run data directly to a file
    /// </summary>
    public static async Task<ExportResult> ExportToFileAsync(
        string runDirectory,
        string outputFilePath,
        ExportOptions? options = null)
    {
        var result = await ExportFromRunDirectoryAsync(runDirectory, options);
        
        await File.WriteAllTextAsync(outputFilePath, result.NdjsonContent, Encoding.UTF8);
        
        return result;
    }
}
