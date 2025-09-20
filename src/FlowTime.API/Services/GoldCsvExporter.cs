using System.Globalization;
using System.Text;
using FlowTime.Adapters.Synthetic;

namespace FlowTime.API.Services;

/// <summary>
/// Exports FlowTime run data to Gold CSV format for external analysis.
/// Gold CSV Format: time_bin,component_id,measure,value
/// </summary>
public static class GoldCsvExporter
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
        /// Include header row in output
        /// </summary>
        public bool IncludeHeader { get; init; } = true;
        
        /// <summary>
        /// Timezone for time_bin formatting (defaults to UTC)
        /// </summary>
        public string Timezone { get; init; } = "UTC";
    }
    
    public record ExportResult
    {
        public required string CsvContent { get; init; }
        public required int RowCount { get; init; }
        public required int SeriesCount { get; init; }
        public required string ContentHash { get; init; }
    }
    
    /// <summary>
    /// Export run data from artifact directory to Gold CSV format
    /// </summary>
    public static async Task<ExportResult> ExportFromRunDirectoryAsync(
        string runDirectory, 
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        
        var reader = new FileSeriesReader();
        var adapter = new RunArtifactAdapter(reader, runDirectory);
        
        // Get the index to understand the data structure
        var index = await adapter.GetIndexAsync();
        
        var csvBuilder = new StringBuilder();
        
        // Add header if requested
        if (options.IncludeHeader)
        {
            csvBuilder.AppendLine("time_bin,component_id,measure,value");
        }
        
        int totalRows = 0;
        int seriesCount = 0;
        
        // Process each series in the index
        foreach (var seriesInfo in index.Series)
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
                    
                    // Format: time_bin,component_id,measure,value
                    csvBuilder.AppendFormat(CultureInfo.InvariantCulture, 
                        "{0},{1},{2},{3}\n",
                        timeBinStr,
                        componentId,
                        measure,
                        value);
                    
                    totalRows++;
                }
            }
            catch (Exception)
            {
                // Skip series that can't be read
                continue;
            }
        }
        
        var csvContent = csvBuilder.ToString();
        
        // Calculate content hash for determinism
        var contentBytes = Encoding.UTF8.GetBytes(csvContent);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        var contentHash = "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return new ExportResult
        {
            CsvContent = csvContent,
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
        
        await File.WriteAllTextAsync(outputFilePath, result.CsvContent, Encoding.UTF8);
        
        return result;
    }
}
