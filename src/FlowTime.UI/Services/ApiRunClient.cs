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
        var gr = new GraphRunResult(r.Grid.Bins, r.Grid.BinSize, r.Grid.BinUnit, r.Order, r.Series, r.RunId);
        return Result<GraphRunResult>.Ok(gr, res.StatusCode);
    }

    public async Task<Result<GraphStructureResult>> GraphAsync(string yaml, CancellationToken ct = default)
    {
        var res = await api.GraphAsync(yaml, ct);
        if (!res.Success || res.Value is null) return Result<GraphStructureResult>.Fail(res.Error, res.StatusCode);
        var g = res.Value;
        var nodes = g.Edges.Select(e => new NodeInfo(e.Id, e.Inputs)).ToList();
        var gs = new GraphStructureResult(g.Order, nodes);
        return Result<GraphStructureResult>.Ok(gs, res.StatusCode);
    }
}
