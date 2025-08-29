namespace FlowTime.UI.Services;

public sealed record GraphRunResult(
    int Bins,
    int BinMinutes,
    IReadOnlyList<string> Order,
    IReadOnlyDictionary<string, double[]> Series);

public sealed record Result<T>(bool Success, T? Value, string? Error, int StatusCode = 0)
{
    public static Result<T> Ok(T value, int status = 200) => new(true, value, null, status);
    public static Result<T> Fail(string? error, int status = 0) => new(false, default, error, status);
}

public interface IRunClient
{
    Task<Result<bool>> HealthAsync(CancellationToken ct = default);
    Task<Result<GraphRunResult>> RunAsync(string yaml, CancellationToken ct = default);
    Task<Result<GraphRunResult>> GraphAsync(string yaml, CancellationToken ct = default);
}
