namespace FlowTime.UI.Services;

public sealed class SimulationRunClient : IRunClient
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
        var result = new GraphRunResult(bins, 60, "minutes", order, series, null); // 60 minutes binSize
        return Task.FromResult(Result<GraphRunResult>.Ok(result));
    }

    public Task<Result<GraphStructureResult>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var order = new[] { "demand", "served" };
        var nodes = new List<NodeInfo>
        {
            new NodeInfo("demand", Array.Empty<string>()),
            new NodeInfo("served", new[]{"demand"})
        };
        var result = new GraphStructureResult(order, nodes);
        return Task.FromResult(Result<GraphStructureResult>.Ok(result));
    }
}
