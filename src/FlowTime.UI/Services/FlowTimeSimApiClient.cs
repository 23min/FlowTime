using System.Text;
using System.Text.Json;

namespace FlowTime.UI.Services;

public interface IFlowTimeSimApiClient
{
    string? BaseAddress { get; }
    Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default);
    Task<Result<SeriesIndex>> GetIndexAsync(string runId, CancellationToken ct = default);
    Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default);
    Task<Result<List<ApiTemplateInfo>>> GetTemplatesAsync(CancellationToken ct = default);
    Task<Result<ApiTemplateInfo>> GetTemplateAsync(string templateId, CancellationToken ct = default);
    Task<Result<TemplateGenerationResponse>> GenerateModelAsync(string templateId, Dictionary<string, object> parameters, CancellationToken ct = default);
    Task<Result<bool>> HealthAsync(CancellationToken ct = default);
    Task<Result<object>> GetDetailedHealthAsync(CancellationToken ct = default);
}

public class FlowTimeSimApiClient : IFlowTimeSimApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<FlowTimeSimApiClient> logger;
    private readonly string apiVersion;
    private readonly string apiBasePath;

    public FlowTimeSimApiClient(HttpClient httpClient, ILogger<FlowTimeSimApiClient> logger, string apiVersion = "v1")
    {
        this.httpClient = httpClient;
        this.logger = logger;
        this.apiVersion = apiVersion;
        apiBasePath = $"api/{apiVersion}";
    }

    public string? BaseAddress => httpClient.BaseAddress?.ToString();

    public async Task<Result<bool>> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{apiVersion}/healthz", ct);
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
            var response = await httpClient.GetAsync($"{apiVersion}/healthz?detailed=true", ct);
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

    // TODO: This method calls /api/v1/run which was removed from Sim API on Oct 1, 2025.
    // SIMULATE tab functionality is broken in API mode. Consider refactoring to call Engine API instead.
    public async Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
            var response = await httpClient.PostAsync($"{apiBasePath}/run", content, ct);
            
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

    // TODO: Sim API doesn't have /api/v1/runs/{id}/index endpoint. This should call Engine API instead.
    public async Task<Result<SeriesIndex>> GetIndexAsync(string runId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{apiBasePath}/runs/{runId}/index", ct);
            
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

    // TODO: Sim API doesn't have /api/v1/runs/{id}/series/{id} endpoint. This should call Engine API instead.
    public async Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{apiBasePath}/runs/{runId}/series/{seriesId}", ct);
            
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

    public async Task<Result<List<ApiTemplateInfo>>> GetTemplatesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{apiBasePath}/templates", ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<List<ApiTemplateInfo>>.Fail($"Failed to get templates: {errorText}", (int)response.StatusCode);
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<List<ApiTemplateInfo>>(responseText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return result != null 
                ? Result<List<ApiTemplateInfo>>.Ok(result, (int)response.StatusCode)
                : Result<List<ApiTemplateInfo>>.Fail("Failed to parse templates");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get templates");
            return Result<List<ApiTemplateInfo>>.Fail($"Templates error: {ex.Message}");
        }
    }

    public async Task<Result<ApiTemplateInfo>> GetTemplateAsync(string templateId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{apiBasePath}/templates/{templateId}", ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<ApiTemplateInfo>.Fail($"Failed to get template: {errorText}", (int)response.StatusCode);
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<ApiTemplateInfo>(responseText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return result != null 
                ? Result<ApiTemplateInfo>.Ok(result, (int)response.StatusCode)
                : Result<ApiTemplateInfo>.Fail("Failed to parse template");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get template '{TemplateId}'", templateId);
            return Result<ApiTemplateInfo>.Fail($"Template error: {ex.Message}");
        }
    }

    public async Task<Result<TemplateGenerationResponse>> GenerateModelAsync(string templateId, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Generating model from template '{TemplateId}' with {ParamCount} parameters", templateId, parameters.Count);
            
            var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{apiBasePath}/templates/{templateId}/generate", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Model generation failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return Result<TemplateGenerationResponse>.Fail($"Model generation failed: {response.StatusCode}", (int)response.StatusCode);
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            logger.LogInformation("Raw response content length: {Length} chars, first 200 chars: {Preview}", 
                responseContent?.Length ?? 0, 
                responseContent?.Length > 200 ? responseContent.Substring(0, 200) : responseContent);
            
            var result = JsonSerializer.Deserialize<TemplateGenerationResponse>(responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            if (result == null)
            {
                logger.LogWarning("Deserialization returned null for template '{TemplateId}'", templateId);
                return Result<TemplateGenerationResponse>.Fail("Empty response from model generation API");
            }
            
            logger.LogInformation("Successfully generated model from template '{TemplateId}' - model length: {Length} chars", templateId, result.Model?.Length ?? 0);
            return Result<TemplateGenerationResponse>.Ok(result, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate model from template '{TemplateId}'", templateId);
            return Result<TemplateGenerationResponse>.Fail($"Model generation error: {ex.Message}");
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
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public string Timezone { get; set; } = "UTC";
    public string Align { get; set; } = "left";
    
    // INTERNAL ONLY: Computed property for UI display convenience
    // NOT serialized to/from JSON (binMinutes removed from all external schemas)
    // NOTE: Engine validates binUnit at model parse time - invalid units should never reach UI
    [System.Text.Json.Serialization.JsonIgnore]
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        "weeks" => BinSize * 10080,
        _ => throw new ArgumentException($"Invalid binUnit '{BinUnit}'. Engine should have validated this.")
    };
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

public class ApiTemplateInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<ApiTemplateParameter>? Parameters { get; set; }
    public object? Preview { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("captureKey")]
    public string? CaptureKey { get; set; }
}

public class ApiTemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
}

public class TemplateGenerationResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("provenance")]
    public object? Provenance { get; set; }
}
