using System.Globalization;

namespace FlowTime.UI.Services;

// Extension method for Gaussian distribution
public static class RandomExtensions
{
    public static double NextGaussian(this Random random)
    {
        // Box-Muller transform for normal distribution
        var u1 = 1.0 - random.NextDouble(); // uniform(0,1] random doubles
        var u2 = 1.0 - random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); // random normal(0,1)
        return randStdNormal;
    }
}

public interface ISimResultsService
{
    Task<Result<SimResultData>> GetSimulationResultsAsync(string runId, CancellationToken ct = default);
}

public class SimResultsService : ISimResultsService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly IFlowTimeApiClient apiClient;
    private readonly FeatureFlagService featureFlags;
    private readonly ILogger<SimResultsService> logger;

    public SimResultsService(IFlowTimeSimApiClient simClient, IFlowTimeApiClient apiClient, FeatureFlagService featureFlags, ILogger<SimResultsService> logger)
    {
        this.simClient = simClient;
        this.apiClient = apiClient;
        this.featureFlags = featureFlags;
        this.logger = logger;
    }

    public async Task<Result<SimResultData>> GetSimulationResultsAsync(string runId, CancellationToken ct = default)
    {
        try
        {
            await featureFlags.EnsureLoadedAsync();
            
            // Handle demo:// URL scheme - always use demo data regardless of mode
            if (runId.StartsWith("demo://", StringComparison.OrdinalIgnoreCase))
            {
                var demoRunId = runId.Substring(7); // Remove "demo://" prefix
                return await GetDemoModeResultsAsync(demoRunId, ct);
            }
            
            // In demo mode, return synthetic data to match the template expectations
            if (featureFlags.UseDemoMode)
            {
                return await GetDemoModeResultsAsync(runId, ct);
            }

            var isEngineRun = runId.StartsWith("run_", StringComparison.OrdinalIgnoreCase);

            // Step 1: Get the series index following artifact-first pattern
            Result<SeriesIndex> indexResult;
            if (isEngineRun)
            {
                var apiIndex = await apiClient.GetRunIndexAsync(runId, ct);
                if (!apiIndex.Success || apiIndex.Value == null)
                {
                    return Result<SimResultData>.Fail($"Failed to get engine series index: {apiIndex.Error}", apiIndex.StatusCode);
                }
                indexResult = Result<SeriesIndex>.Ok(apiIndex.Value, apiIndex.StatusCode);
            }
            else
            {
                indexResult = await simClient.GetIndexAsync(runId, ct);
            }
            if (!indexResult.Success)
            {
                return Result<SimResultData>.Fail($"Failed to get series index: {indexResult.Error}", indexResult.StatusCode);
            }

            var index = indexResult.Value!;
            
            // Step 2: Load all series data from CSV files
            var seriesData = new Dictionary<string, double[]>();
            foreach (var series in index.Series)
            {
                Result<Stream> csvResult;
                if (isEngineRun)
                {
                    var apiSeries = await apiClient.GetRunSeriesAsync(runId, series.Id, ct);
                    csvResult = apiSeries.Success && apiSeries.Value != null
                        ? Result<Stream>.Ok(apiSeries.Value, apiSeries.StatusCode)
                        : Result<Stream>.Fail(apiSeries.Error ?? "Unknown error", apiSeries.StatusCode);
                }
                else
                {
                    csvResult = await simClient.GetSeriesAsync(runId, series.Id, ct);
                }

                if (!csvResult.Success || csvResult.Value == null)
                {
                    logger.LogWarning("Failed to load series {SeriesId}: {Error}", series.Id, csvResult.Error);
                    continue;
                }

                var values = await ParseCsvStream(csvResult.Value, ct);
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

    private async Task<Result<SimResultData>> GetDemoModeResultsAsync(string runId, CancellationToken ct)
    {
        // Generate realistic multi-series data for IT system template
        logger.LogInformation("Generating demo mode results for run {RunId}", runId);
        
        var bins = 24; // 24 hours of data
        var binMinutes = 60; // hourly bins
        var random = new Random(runId.GetHashCode()); // Deterministic but varied per runId
        
        // Generate realistic IT system microservices data
        var seriesData = new Dictionary<string, double[]>
        {
            ["user_requests"] = GenerateTrafficSeries(bins, random, baseRate: 150, peakHours: new[] { 9, 12, 15 }),
            ["api_response"] = GenerateProcessingSeries(bins, random, baseRate: 145, efficiency: 0.97), 
            ["auth_service"] = GenerateServiceSeries(bins, random, baseRate: 145, latency: 25),
            ["business_service"] = GenerateServiceSeries(bins, random, baseRate: 140, latency: 45),
            ["database_service"] = GenerateServiceSeries(bins, random, baseRate: 135, latency: 15)
        };

        var order = seriesData.Keys.ToArray();
        
        var result = new SimResultData(bins, binMinutes, order, seriesData);
        
        // Simulate some processing delay for realism
        await Task.Delay(100, ct);
        
        return Result<SimResultData>.Ok(result);
    }

    private static double[] GenerateTrafficSeries(int bins, Random random, double baseRate, int[] peakHours)
    {
        var values = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            var hour = i;
            var isPeakHour = peakHours.Contains(hour % 24);
            var multiplier = isPeakHour ? 1.5 + random.NextDouble() * 0.5 : 0.7 + random.NextDouble() * 0.6;
            values[i] = Math.Round(baseRate * multiplier + random.NextGaussian() * 10, 1);
        }
        return values;
    }

    private static double[] GenerateProcessingSeries(int bins, Random random, double baseRate, double efficiency)
    {
        var values = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            var processed = baseRate * efficiency * (0.95 + random.NextDouble() * 0.1);
            values[i] = Math.Round(processed + random.NextGaussian() * 5, 1);
        }
        return values;
    }

    private static double[] GenerateServiceSeries(int bins, Random random, double baseRate, double latency)
    {
        var values = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            var throughput = baseRate * (0.9 + random.NextDouble() * 0.2);
            var latencyEffect = 1.0 - (latency / 1000.0); // Higher latency = lower throughput
            values[i] = Math.Round(throughput * latencyEffect + random.NextGaussian() * 8, 1);
        }
        return values;
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
