namespace FlowTime.UI.Services;

public sealed record GraphRunResult(
    int Bins,
    int BinMinutes,
    IReadOnlyList<string> Order,
    IReadOnlyDictionary<string, double[]> Series,
    string? RunId = null);

// Structural graph response (no series data)
public sealed record GraphStructureResult(
    IReadOnlyList<string> Order,
    IReadOnlyList<NodeInfo> Nodes);

public sealed record NodeInfo(string Id, IReadOnlyList<string> Inputs);

public sealed record Result<T>(bool Success, T? Value, string? Error, int StatusCode = 0)
{
    public static Result<T> Ok(T value, int status = 200) => new(true, value, null, status);
    public static Result<T> Fail(string? error, int status = 0) => new(false, default, error, status);
}

public interface IRunClient
{
    Task<Result<bool>> HealthAsync(CancellationToken ct = default);
    Task<Result<GraphRunResult>> RunAsync(string yaml, CancellationToken ct = default);
    Task<Result<GraphStructureResult>> GraphAsync(string yaml, CancellationToken ct = default);
}
