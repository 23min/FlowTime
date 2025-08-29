using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlowTime.UI.Services;

public sealed class FeatureFlagService
{
    private readonly NavigationManager nav;
    private readonly IJSRuntime js;
    private bool loaded;
    private bool useSimulation;
    private const string storageKey = "ft.useSimulation";

    public event Action? Changed;

    public FeatureFlagService(NavigationManager nav, IJSRuntime js)
    {
        this.nav = nav;
        this.js = js;
    }

    public bool UseSimulation => useSimulation;

    public async Task EnsureLoadedAsync()
    {
        if (loaded) return;
        loaded = true;
        // precedence: query string ?sim=1 overrides stored value
        var uri = new Uri(nav.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var simParam = query["sim"];
        if (simParam == "1")
        {
            useSimulation = true;
            await PersistAsync();
            return;
        }
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", storageKey);
            if (stored == "1") useSimulation = true;
        }
        catch { /* ignore */ }
    }

    public async Task SetUseSimulationAsync(bool value)
    {
        if (useSimulation == value) return;
        useSimulation = value;
        await PersistAsync();
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", storageKey, useSimulation ? "1" : "0");
        }
        catch { /* ignore */ }
    }
}
