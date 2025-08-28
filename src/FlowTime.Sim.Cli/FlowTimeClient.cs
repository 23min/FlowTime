using System.Text;
using System.Text.Json;

namespace FlowTime.Sim.Cli;

public sealed class FlowTimeGrid
{
    public int bins { get; init; }
    public int binMinutes { get; init; }
}

public sealed class FlowTimeRunResponse
{
    public FlowTimeGrid grid { get; init; } = new();
    public string[] order { get; init; } = Array.Empty<string>();
    public Dictionary<string, double[]> series { get; init; } = new(StringComparer.Ordinal);
}

public sealed class FlowTimeError
{
    public string? error { get; init; }
}

public static class FlowTimeClient
{
    public static async Task<FlowTimeRunResponse> RunAsync(HttpClient http, string baseUrl, string yaml, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/run");
        req.Content = new StringContent(yaml, Encoding.UTF8, "text/plain");
        // Defensive: ensure Accept header present (tests set it, but callers might forget)
        if (!http.DefaultRequestHeaders.Accept.Any(a => a.MediaType == "application/json"))
        {
            http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await JsonSerializer.DeserializeAsync<FlowTimeError>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }, ct).ConfigureAwait(false);

                var msg = err?.error ?? $"FlowTime returned {(int)resp.StatusCode} {resp.ReasonPhrase}";
                msg = "FlowTime API error: " + msg;
                throw new InvalidOperationException(msg);
            }
            catch (JsonException)
            {
                resp.EnsureSuccessStatusCode();
            }
        }

        var res = await JsonSerializer.DeserializeAsync<FlowTimeRunResponse>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, ct).ConfigureAwait(false);

        if (res is null) throw new InvalidOperationException("Empty response from FlowTime.");
        return res;
    }
}
