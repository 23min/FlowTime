using System.Globalization;
using FlowTime.Adapters.Synthetic;
using FlowTime.Core;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace FlowTime.API.Services;

/// <summary>
/// Exports FlowTime run data to Parquet format for analytics tools (Spark, BigQuery, etc.).
/// Parquet is a columnar storage format optimized for analytics workloads.
/// </summary>
public static class ParquetExporter
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
        public required byte[] ParquetData { get; init; }
        public required int RowCount { get; init; }
        public required int SeriesCount { get; init; }
        public required string ContentHash { get; init; }
    }
    
    /// <summary>
    /// Export run data from artifact directory to Parquet format
    /// </summary>
    public static async Task<ExportResult> ExportFromRunDirectoryAsync(
        string runDirectory, 
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        
        var reader = new FileSeriesReader();
        var adapter = new RunArtifactAdapter(reader, runDirectory);
        var seriesIndex = await adapter.GetIndexAsync();
        
        // Prepare data collections
        var timeBins = new List<DateTime>();
        var componentIds = new List<string>();
        var measures = new List<string>();
        var values = new List<double>();
        
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
                var seriesValues = series.ToArray();
                
                // Process each time bin in the series
                for (int timeBin = 0; timeBin < seriesValues.Length; timeBin++)
                {
                    var value = seriesValues[timeBin];
                    
                    // Calculate time_bin timestamp
                    var timeBinTimestamp = options.StartTime.AddMinutes(timeBin * options.BinMinutes);
                    
                    // Add to collections
                    timeBins.Add(timeBinTimestamp);
                    componentIds.Add(componentId);
                    measures.Add(measure);
                    values.Add(value);
                }
            }
            catch (Exception)
            {
                // Skip series that can't be read
                continue;
            }
        }
        
        // Create Parquet schema
        var schema = new ParquetSchema(
            new DataField<DateTime>("time_bin"),
            new DataField<string>("component_id"),
            new DataField<string>("measure"),
            new DataField<double>("value")
        );
        
        // Write to Parquet format
        using var memoryStream = new MemoryStream();
        using (var parquetWriter = await ParquetWriter.CreateAsync(schema, memoryStream))
        {
            using var rowGroupWriter = parquetWriter.CreateRowGroup();
            
            await rowGroupWriter.WriteColumnAsync(new DataColumn(
                schema.DataFields[0],
                timeBins.ToArray()
            ));
            
            await rowGroupWriter.WriteColumnAsync(new DataColumn(
                schema.DataFields[1],
                componentIds.ToArray()
            ));
            
            await rowGroupWriter.WriteColumnAsync(new DataColumn(
                schema.DataFields[2],
                measures.ToArray()
            ));
            
            await rowGroupWriter.WriteColumnAsync(new DataColumn(
                schema.DataFields[3],
                values.ToArray()
            ));
        }
        
        var parquetData = memoryStream.ToArray();
        
        // Calculate content hash for determinism
        var hashBytes = System.Security.Cryptography.SHA256.HashData(parquetData);
        var contentHash = "sha256:" + Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return new ExportResult
        {
            ParquetData = parquetData,
            RowCount = values.Count,
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
        
        await File.WriteAllBytesAsync(outputFilePath, result.ParquetData);
        
        return result;
    }
}
