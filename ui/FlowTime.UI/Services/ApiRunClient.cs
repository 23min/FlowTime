namespace FlowTime.UI.Services;

internal sealed class ApiRunClient : IRunClient
{
    private readonly IFlowTimeApiClient api;
    public ApiRunClient(IFlowTimeApiClient api) => this.api = api;

    public async Task<Result<bool>> HealthAsync(CancellationToken ct = default)
    {
        var res = await api.HealthAsync(ct);
        return res.Success ? Result<bool>.Ok(true, res.StatusCode) : Result<bool>.Fail(res.Error, res.StatusCode);
    }

    public async Task<Result<GraphRunResult>> RunAsync(string yaml, CancellationToken ct = default)
    {
        var res = await api.RunAsync(yaml, ct);
        if (!res.Success || res.Value is null) return Result<GraphRunResult>.Fail(res.Error, res.StatusCode);
        var r = res.Value;
        var gr = new GraphRunResult(r.Grid.Bins, r.Grid.BinMinutes, r.Order, r.Series);
        return Result<GraphRunResult>.Ok(gr, res.StatusCode);
    }

    public async Task<Result<GraphRunResult>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var res = await api.GraphAsync(yaml, ct);
        if (!res.Success || res.Value is null) return Result<GraphRunResult>.Fail(res.Error, res.StatusCode);
        var g = res.Value;
        // Graph lacks series in current API: return empty dictionary
        var gr = new GraphRunResult(g.Order.Length, 0, g.Order, new Dictionary<string, double[]>());
        return Result<GraphRunResult>.Ok(gr, res.StatusCode);
    }
}
