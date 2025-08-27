using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FlowTime.UI.Services;

public interface IFlowTimeApiClient
{
    string? BaseAddress { get; }
    Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default);
    Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default);
    Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default);
}

internal sealed class FlowTimeApiClient : IFlowTimeApiClient
{
    private readonly HttpClient http;
    private readonly JsonSerializerOptions json;

    public FlowTimeApiClient(HttpClient http, FlowTimeApiOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        this.http = http;
        json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public string? BaseAddress => http.BaseAddress?.ToString();

    public Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default)
        => GetJson<HealthResponse>("healthz", ct);

    public Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
        Console.WriteLine($"[FlowTimeApiClient] POST /run len={yaml.Length} preview='{yaml.Substring(0, Math.Min(60, yaml.Length)).Replace("\n", "\\n")}'");
        return PostJson<RunResponse>("run", content, ct);
    }

    public Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
        Console.WriteLine($"[FlowTimeApiClient] POST /graph len={yaml.Length} preview='{yaml.Substring(0, Math.Min(60, yaml.Length)).Replace("\n", "\\n")}'");
        return PostJson<GraphResponse>("graph", content, ct);
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
