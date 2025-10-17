namespace FlowTime.UI.Services;

public sealed record GraphRunResult(
    int Bins,
    int BinSize,
    string BinUnit,
    IReadOnlyList<string> Order,
    IReadOnlyDictionary<string, double[]> Series,
    string? RunId = null)
{
    /// <summary>
    /// Computed property for backward compatibility and display purposes.
    /// Converts binSize to minutes based on binUnit.
    /// Throws ArgumentException for invalid binUnit (fail-fast validation).
    /// </summary>
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        "weeks" => BinSize * 10080,
        _ => throw new ArgumentException($"Invalid binUnit: '{BinUnit}'. Expected 'minutes', 'hours', 'days', or 'weeks'.", nameof(BinUnit))
    };
}

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
