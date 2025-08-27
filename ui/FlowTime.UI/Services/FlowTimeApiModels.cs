using System.Text.Json.Serialization;

namespace FlowTime.UI.Services;

public record HealthResponse([property: JsonPropertyName("status")] string Status);

public record RunResponse(
    [property: JsonPropertyName("grid")] GridInfo Grid,
    [property: JsonPropertyName("order")] string[] Order,
    [property: JsonPropertyName("series")] Dictionary<string, double[]> Series);

public record GraphResponse(
    [property: JsonPropertyName("nodes")] string[] Nodes,
    [property: JsonPropertyName("order")] string[] Order,
    [property: JsonPropertyName("edges")] GraphEdge[] Edges);

public record GraphEdge(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("inputs")] string[] Inputs);

public record GridInfo(
    [property: JsonPropertyName("bins")] int Bins,
    [property: JsonPropertyName("binMinutes")] int BinMinutes);

// Generic wrapper so the UI can surface HTTP status codes and error messages instead of silently returning null.
public sealed record ApiCallResult<T>(T? Value, bool Success, int StatusCode, string? Error)
{
    public static ApiCallResult<T> Ok(T value, int status) => new(value, true, status, null);
    public static ApiCallResult<T> Fail(int status, string? error) => new(default, false, status, error);
}

public sealed class FlowTimeApiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080"; // TODO: externalize (config/env) for deployments
}
