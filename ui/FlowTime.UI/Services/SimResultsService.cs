using System.Globalization;

namespace FlowTime.UI.Services;

public interface ISimResultsService
{
    Task<Result<SimResultData>> GetSimulationResultsAsync(string runId, CancellationToken ct = default);
}

public class SimResultsService : ISimResultsService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly ILogger<SimResultsService> logger;

    public SimResultsService(IFlowTimeSimApiClient simClient, ILogger<SimResultsService> logger)
    {
        this.simClient = simClient;
        this.logger = logger;
    }

    public async Task<Result<SimResultData>> GetSimulationResultsAsync(string runId, CancellationToken ct = default)
    {
        try
        {
            // Step 1: Get the series index following artifact-first pattern
            var indexResult = await simClient.GetIndexAsync(runId, ct);
            if (!indexResult.Success)
            {
                return Result<SimResultData>.Fail($"Failed to get series index: {indexResult.Error}", indexResult.StatusCode);
            }

            var index = indexResult.Value!;
            
            // Step 2: Load all series data from CSV files
            var seriesData = new Dictionary<string, double[]>();
            foreach (var series in index.Series)
            {
                var csvResult = await simClient.GetSeriesAsync(runId, series.Id, ct);
                if (!csvResult.Success)
                {
                    logger.LogWarning("Failed to load series {SeriesId}: {Error}", series.Id, csvResult.Error);
                    continue; // Skip failed series but continue with others
                }

                var values = await ParseCsvStream(csvResult.Value!, ct);
                seriesData[series.Id] = values;
            }

            // Step 3: Create result following UI contracts
            var result = new SimResultData(
                bins: index.Grid.Bins,
                binMinutes: index.Grid.BinMinutes,
                order: index.Series.Select(s => s.Id).ToArray(),
                series: seriesData
            );

            return Result<SimResultData>.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get simulation results for run {RunId}", runId);
            return Result<SimResultData>.Fail($"Error loading results: {ex.Message}");
        }
    }

    private static async Task<double[]> ParseCsvStream(Stream csvStream, CancellationToken ct)
    {
        var values = new List<double>();
        
        using var reader = new StreamReader(csvStream);
        
        // Skip header line if present
        var firstLine = await reader.ReadLineAsync(ct);
        if (firstLine != null && !firstLine.StartsWith("t,"))
        {
            // First line might be data, parse it
            if (TryParseCsvLine(firstLine, out var value))
            {
                values.Add(value);
            }
        }

        // Parse remaining lines
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (TryParseCsvLine(line, out var value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static bool TryParseCsvLine(string line, out double value)
    {
        value = 0.0;
        
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split(',');
        if (parts.Length < 2)
            return false;

        // Second column should be the value (first is time index)
        return double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

// Result data structure that matches UI expectations
public class SimResultData
{
    public int Bins { get; }
    public int BinMinutes { get; }
    public string[] Order { get; }
    public Dictionary<string, double[]> Series { get; }

    public SimResultData(int bins, int binMinutes, string[] order, Dictionary<string, double[]> series)
    {
        Bins = bins;
        BinMinutes = binMinutes;
        Order = order;
        Series = series;
    }
}
