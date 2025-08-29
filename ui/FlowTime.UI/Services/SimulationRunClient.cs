namespace FlowTime.UI.Services;

internal sealed class SimulationRunClient : IRunClient
{
    // Deterministic synthetic data for quick UI iter.
    public Task<Result<bool>> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(Result<bool>.Ok(true));

    public Task<Result<GraphRunResult>> RunAsync(string yaml, CancellationToken ct = default)
    {
        var order = new[] { "demand", "served" };
        var bins = 8;
        var series = new Dictionary<string, double[]>
        {
            ["demand"] = Enumerable.Repeat(10d, bins).ToArray(),
            ["served"] = Enumerable.Repeat(8d, bins).ToArray()
        };
        var result = new GraphRunResult(bins, 60, order, series);
        return Task.FromResult(Result<GraphRunResult>.Ok(result));
    }

    public Task<Result<GraphRunResult>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var order = new[] { "demand", "served" };
        var result = new GraphRunResult(order.Length, 0, order, new Dictionary<string, double[]>());
        return Task.FromResult(Result<GraphRunResult>.Ok(result));
    }
}
