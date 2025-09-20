using System.Text;
using System.Text.Json;

namespace FlowTime.UI.Services;

public interface IFlowTimeSimApiClient
{
    string? BaseAddress { get; }
    Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default);
    Task<Result<SeriesIndex>> GetIndexAsync(string runId, CancellationToken ct = default);
    Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default);
    Task<Result<List<ScenarioInfo>>> GetScenariosAsync(CancellationToken ct = default);
    Task<Result<bool>> HealthAsync(CancellationToken ct = default);
    Task<Result<object>> GetDetailedHealthAsync(CancellationToken ct = default);
}

public class FlowTimeSimApiClient : IFlowTimeSimApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<FlowTimeSimApiClient> logger;
    private readonly string apiVersion;

    public FlowTimeSimApiClient(HttpClient httpClient, ILogger<FlowTimeSimApiClient> logger, string apiVersion = "v1")
    {
        this.httpClient = httpClient;
        this.logger = logger;
        this.apiVersion = apiVersion;
    }

    public string? BaseAddress => httpClient.BaseAddress?.ToString();

    public async Task<Result<bool>> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/{apiVersion}/healthz", ct);
            return response.IsSuccessStatusCode 
                ? Result<bool>.Ok(true, (int)response.StatusCode)
                : Result<bool>.Fail($"Health check failed: {response.StatusCode}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
            return Result<bool>.Fail($"Health check error: {ex.Message}");
        }
    }

    public async Task<Result<object>> GetDetailedHealthAsync(CancellationToken ct = default)
    {
        try
        {
            // Call with detailed parameter to get full health information including endpoints and storage
            var response = await httpClient.GetAsync($"/{apiVersion}/healthz?detailed=true", ct);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(ct);
            
            // Try to parse as FlowTime-Sim specific detailed health response first
            try
            {
                var simDetailedHealth = JsonSerializer.Deserialize<FlowTimeSimDetailedHealthResponse>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                return new Result<object>(true, simDetailedHealth, null, (int)response.StatusCode);
            }
            catch
            {
                try
                {
                    // Fall back to SimpleHealthResponse format
                    var simpleHealth = JsonSerializer.Deserialize<SimpleHealthResponse>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    return new Result<object>(true, simpleHealth, null, (int)response.StatusCode);
                }
                catch
                {
                    // Final fallback to generic object
                    var genericHealth = JsonSerializer.Deserialize<object>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    return new Result<object>(true, genericHealth, null, (int)response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            return new Result<object>(false, null, $"Failed to get detailed health: {ex.Message}");
        }
    }

    public async Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
            var response = await httpClient.PostAsync($"/{apiVersion}/sim/run", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<SimRunResponse>.Fail($"Simulation run failed: {errorText}", (int)response.StatusCode);
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<SimRunResponse>(responseText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return result != null 
                ? Result<SimRunResponse>.Ok(result, (int)response.StatusCode)
                : Result<SimRunResponse>.Fail("Failed to parse simulation response");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulation run failed");
            return Result<SimRunResponse>.Fail($"Simulation error: {ex.Message}");
        }
    }

    public async Task<Result<SeriesIndex>> GetIndexAsync(string runId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/{apiVersion}/sim/runs/{runId}/index", ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<SeriesIndex>.Fail($"Failed to get index: {errorText}", (int)response.StatusCode);
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<SeriesIndex>(responseText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return result != null 
                ? Result<SeriesIndex>.Ok(result, (int)response.StatusCode)
                : Result<SeriesIndex>.Fail("Failed to parse series index");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get series index for run {RunId}", runId);
            return Result<SeriesIndex>.Fail($"Index error: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/{apiVersion}/sim/runs/{runId}/series/{seriesId}", ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<Stream>.Fail($"Failed to get series: {errorText}", (int)response.StatusCode);
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return Result<Stream>.Ok(stream, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get series {SeriesId} for run {RunId}", seriesId, runId);
            return Result<Stream>.Fail($"Series error: {ex.Message}");
        }
    }

    public async Task<Result<List<ScenarioInfo>>> GetScenariosAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/{apiVersion}/sim/templates", ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<List<ScenarioInfo>>.Fail($"Failed to get templates: {errorText}", (int)response.StatusCode);
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<List<ScenarioInfo>>(responseText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return result != null 
                ? Result<List<ScenarioInfo>>.Ok(result, (int)response.StatusCode)
                : Result<List<ScenarioInfo>>.Fail("Failed to parse templates");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get templates");
            return Result<List<ScenarioInfo>>.Fail($"Templates error: {ex.Message}");
        }
    }
}

// API Response models following the integration spec
public class SimRunResponse
{
    public string SimRunId { get; set; } = string.Empty;
}

public class SeriesIndex
{
    public int SchemaVersion { get; set; }
    public SimGridInfo Grid { get; set; } = new();
    public List<SeriesInfo> Series { get; set; } = new();
    public FormatsInfo Formats { get; set; } = new();
}

public class SimGridInfo
{
    public int Bins { get; set; }
    public int BinMinutes { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Align { get; set; } = "left";
}

public class SeriesInfo
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Hash { get; set; } = string.Empty;
}

public class FormatsInfo
{
    public string GoldTable { get; set; } = string.Empty;
}

public class ScenarioInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public object? Preview { get; set; }
}
