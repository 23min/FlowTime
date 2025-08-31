namespace FlowTime.UI.Services;

internal sealed class RunClientRouter : IRunClient, IDisposable
{
    private readonly ApiRunClient api;
    private readonly SimulationRunClient sim;
    private readonly FeatureFlagService flags;
    private bool disposed;

    public RunClientRouter(ApiRunClient api, SimulationRunClient sim, FeatureFlagService flags)
    {
        this.api = api; this.sim = sim; this.flags = flags;
        // ensure flags loaded lazily (fire and forget)
        _ = flags.EnsureLoadedAsync();
        flags.Changed += OnFlagsChanged;
    }

    private void OnFlagsChanged() => StateChanged?.Invoke();
    public event Action? StateChanged;

    private IRunClient Current => flags.UseSimulation ? sim : api;

    public Task<Result<bool>> HealthAsync(CancellationToken ct = default) => Current.HealthAsync(ct);
    public Task<Result<GraphRunResult>> RunAsync(string yaml, CancellationToken ct = default) => Current.RunAsync(yaml, ct);
    public Task<Result<GraphStructureResult>> GraphAsync(string yaml, CancellationToken ct = default) => Current.GraphAsync(yaml, ct);

    public void Dispose()
    {
        if (disposed) return; disposed = true; flags.Changed -= OnFlagsChanged;
    }
}
