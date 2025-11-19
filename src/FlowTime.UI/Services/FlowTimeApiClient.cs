using System.Collections.Generic;
using System.Net.Http.Headers;
using System.IO; // for Stream
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.UI.Configuration;
using FlowTime.UI.Components.Topology;

namespace FlowTime.UI.Services;

public interface IFlowTimeApiClient
{
    string? BaseAddress { get; }
    Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default);
    Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default);
    Task<ApiCallResult<RunSummaryResponseDto>> GetRunSummariesAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<ApiCallResult<RunCreateResponseDto>> CreateRunAsync(RunCreateRequestDto request, CancellationToken ct = default);
    Task<ApiCallResult<RunCreateResponseDto>> GetRunAsync(string runId, CancellationToken ct = default);
    Task<ApiCallResult<TelemetryCaptureResponseDto>> GenerateTelemetryCaptureAsync(TelemetryCaptureRequestDto request, CancellationToken ct = default);
    Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetRunStateAsync(string runId, int binIndex, CancellationToken ct = default);
    Task<ApiCallResult<TimeTravelStateWindowDto>> GetRunStateWindowAsync(string runId, int startBin, int endBin, string? mode = null, bool includeEdges = false, CancellationToken ct = default);
    Task<ApiCallResult<GraphResponseModel>> GetRunGraphAsync(string runId, GraphQueryOptions? options = null, CancellationToken ct = default);
    Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default);
    Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default);
    Task<ApiCallResult<TimeTravelMetricsResponseDto>> GetRunMetricsAsync(string runId, CancellationToken ct = default);
    Task<ApiCallResult<BulkArtifactDeleteResponse>> BulkDeleteArtifactsAsync(string[] artifactIds, CancellationToken ct = default);
}

internal sealed class FlowTimeApiClient : IFlowTimeApiClient
{
    private readonly HttpClient http;
    private readonly JsonSerializerOptions json;
    private readonly string apiVersion;
    private readonly string apiBasePath;

    public FlowTimeApiClient(HttpClient http, FlowTimeApiOptions opts)
    {
        // HttpClient is pre-configured with BaseAddress in DI. Respect that to avoid clobbering static client base.
        this.http = http;
        this.apiVersion = opts.ApiVersion;
        apiBasePath = $"{apiVersion}";
        json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string? BaseAddress => http.BaseAddress?.ToString();

    public Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default)
        => GetJson<HealthResponse>($"{apiVersion}/healthz", ct);

    public Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default)
        => GetJson<HealthResponse>("healthz", ct);

    public async Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"{apiVersion}/healthz", ct);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                
                // Try to parse as detailed health response first
                try
                {
                    var detailedHealth = JsonSerializer.Deserialize<DetailedHealthResponse>(content, json);
                    if (detailedHealth is not null)
                    {
                        return ApiCallResult<object>.Ok(detailedHealth, (int)response.StatusCode);
                    }
                }
                catch
                {
                    // ignore and try fallback
                }

                try
                {
                    var simpleHealth = JsonSerializer.Deserialize<SimpleHealthResponse>(content, json);
                    if (simpleHealth is not null)
                    {
                        return ApiCallResult<object>.Ok(simpleHealth, (int)response.StatusCode);
                    }
                }
                catch
                {
                    // ignore and drop to failure below
                }

                return ApiCallResult<object>.Fail((int)response.StatusCode, "Health response could not be parsed.");
            }
            else
            {
                return ApiCallResult<object>.Fail((int)response.StatusCode, $"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            return ApiCallResult<object>.Fail(0, $"Failed to get detailed health: {ex.Message}");
        }
    }

    public Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
        Console.WriteLine($"[FlowTimeApiClient] POST /{apiBasePath}/run len={yaml.Length} preview='{yaml.Substring(0, Math.Min(60, yaml.Length)).Replace("\n", "\\n")}'");
        return PostJson<RunResponse>($"{apiBasePath}/run", content, ct);
    }

    public Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
        Console.WriteLine($"[FlowTimeApiClient] POST /{apiBasePath}/graph len={yaml.Length} preview='{yaml.Substring(0, Math.Min(60, yaml.Length)).Replace("\n", "\\n")}'");
        return PostJson<GraphResponse>($"{apiBasePath}/graph", content, ct);
    }

    public Task<ApiCallResult<RunSummaryResponseDto>> GetRunSummariesAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var path = $"{apiBasePath}/runs?page={page}&pageSize={pageSize}";
        return GetJson<RunSummaryResponseDto>(path, ct);
    }

    public Task<ApiCallResult<RunCreateResponseDto>> CreateRunAsync(RunCreateRequestDto request, CancellationToken ct = default)
        => PostJson<RunCreateResponseDto>($"{apiBasePath}/runs", request, ct);

    public Task<ApiCallResult<RunCreateResponseDto>> GetRunAsync(string runId, CancellationToken ct = default)
        => GetJson<RunCreateResponseDto>($"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}", ct);

    public Task<ApiCallResult<TelemetryCaptureResponseDto>> GenerateTelemetryCaptureAsync(TelemetryCaptureRequestDto request, CancellationToken ct = default)
        => PostJson<TelemetryCaptureResponseDto>($"{apiBasePath}/telemetry/captures", request, ct);

    public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetRunStateAsync(string runId, int binIndex, CancellationToken ct = default)
    {
        var path = $"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/state?binIndex={binIndex}";
        return GetJson<TimeTravelStateSnapshotDto>(path, ct);
    }

    public Task<ApiCallResult<TimeTravelStateWindowDto>> GetRunStateWindowAsync(string runId, int startBin, int endBin, string? mode = null, bool includeEdges = false, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"startBin={startBin}",
            $"endBin={endBin}"
        };

        if (!string.IsNullOrWhiteSpace(mode))
        {
            query.Add($"mode={Uri.EscapeDataString(mode)}");
        }

        if (includeEdges)
        {
            query.Add("include=edges");
        }

        var path = $"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/state_window?{string.Join('&', query)}";
        return GetJson<TimeTravelStateWindowDto>(path, ct);
    }

    public Task<ApiCallResult<GraphResponseModel>> GetRunGraphAsync(string runId, GraphQueryOptions? options = null, CancellationToken ct = default)
    {
        var path = $"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/graph";
        var querySegments = new List<string>();

        if (options is not null)
        {
            if (!string.IsNullOrWhiteSpace(options.Mode))
            {
                querySegments.Add($"mode={Uri.EscapeDataString(options.Mode)}");
            }

            if (options.Kinds is not null && options.Kinds.Count > 0)
            {
                var kinds = string.Join(',', options.Kinds);
                querySegments.Add($"kinds={Uri.EscapeDataString(kinds)}");
            }

            if (options.DependencyFields is not null && options.DependencyFields.Count > 0)
            {
                var dependencies = string.Join(',', options.DependencyFields);
                querySegments.Add($"dependencyFields={Uri.EscapeDataString(dependencies)}");
            }

            if (!string.IsNullOrWhiteSpace(options.EdgeWeight))
            {
                querySegments.Add($"edgeWeight={Uri.EscapeDataString(options.EdgeWeight)}");
            }
        }

        if (querySegments.Count > 0)
        {
            path = $"{path}?{string.Join('&', querySegments)}";
        }

        return GetJson<GraphResponseModel>(path, ct);
    }

    public Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default)
        => GetJson<SeriesIndex>($"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/index", ct);

    public async Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/series/{Uri.EscapeDataString(seriesId)}", HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            if (!res.IsSuccessStatusCode)
            {
                var err = await SafeReadError(res, ct);
                return ApiCallResult<Stream>.Fail((int)res.StatusCode, err);
            }
            await using var body = await res.Content.ReadAsStreamAsync(ct);
            var ms = new MemoryStream();
            await body.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ApiCallResult<Stream>.Ok(ms, (int)res.StatusCode);
        }
        finally
        {
            res.Dispose();
        }
    }

    public Task<ApiCallResult<TimeTravelMetricsResponseDto>> GetRunMetricsAsync(string runId, CancellationToken ct = default)
    {
        var path = $"{apiBasePath}/runs/{Uri.EscapeDataString(runId)}/metrics";
        return GetJson<TimeTravelMetricsResponseDto>(path, ct);
    }

    public Task<ApiCallResult<BulkArtifactDeleteResponse>> BulkDeleteArtifactsAsync(string[] artifactIds, CancellationToken ct = default)
        => PostJson<BulkArtifactDeleteResponse>($"{apiBasePath}/artifacts/bulk-delete", artifactIds, ct);

    private async Task<ApiCallResult<T>> GetJson<T>(string path, CancellationToken ct)
    {
        using var res = await http.GetAsync(path, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await SafeReadError(res, ct);
            return ApiCallResult<T>.Fail((int)res.StatusCode, err);
        }
        await using var s = await res.Content.ReadAsStreamAsync(ct);
        var val = await JsonSerializer.DeserializeAsync<T>(s, json, ct);
        return ApiCallResult<T>.Ok(val!, (int)res.StatusCode);
    }

    private async Task<ApiCallResult<T>> PostJson<T>(string path, HttpContent content, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await SafeReadError(res, ct);
            return ApiCallResult<T>.Fail((int)res.StatusCode, err);
        }
        await using var s = await res.Content.ReadAsStreamAsync(ct);
        var val = await JsonSerializer.DeserializeAsync<T>(s, json, ct);
        return ApiCallResult<T>.Ok(val!, (int)res.StatusCode);
    }

    private Task<ApiCallResult<TResponse>> PostJson<TResponse>(string path, object payload, CancellationToken ct)
    {
        var jsonPayload = JsonSerializer.Serialize(payload, json);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        return PostJson<TResponse>(path, content, ct);
    }

    private static async Task<string?> SafeReadError(HttpResponseMessage res, CancellationToken ct)
    {
        try
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text)) return null;
            // Try to extract { error: "..." }
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    return e.GetString();
            }
            catch { /* ignore parse errors */ }
            return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
        }
        catch { return null; }
    }
}
