using System.Net.Http.Headers;
using System.IO; // for Stream
using System.Text;
using System.Text.Json;
using FlowTime.UI.Configuration;

namespace FlowTime.UI.Services;

public interface IFlowTimeApiClient
{
    string? BaseAddress { get; }
    Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default);
    Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default);
    Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default);
    Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default);
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
        apiBasePath = $"api/{apiVersion}";
        json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                    return ApiCallResult<object>.Ok(detailedHealth, (int)response.StatusCode);
                }
                catch
                {
                    // Fall back to simple health response
                    var simpleHealth = JsonSerializer.Deserialize<SimpleHealthResponse>(content, json);
                    return ApiCallResult<object>.Ok(simpleHealth, (int)response.StatusCode);
                }
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

    public Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default)
        => GetJson<SeriesIndex>($"{apiBasePath}/runs/{runId}/index", ct);

    public async Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"{apiBasePath}/runs/{runId}/series/{seriesId}", HttpCompletionOption.ResponseHeadersRead, ct);
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
