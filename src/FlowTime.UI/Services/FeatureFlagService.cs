using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlowTime.UI.Services;

public sealed class FeatureFlagService
{
    private readonly NavigationManager nav;
    private readonly IJSRuntime js;
    private bool loaded;
    private bool useDemoMode;
    private const string storageKey = "ft.useSimulation"; // Keep old key name for backwards compatibility

    public event Action? Changed;

    public FeatureFlagService(NavigationManager nav, IJSRuntime js)
    {
        this.nav = nav;
        this.js = js;
    }

    public bool UseDemoMode => useDemoMode;

    public async Task EnsureLoadedAsync()
    {
        if (loaded) return;
        loaded = true;
        var previous = useDemoMode; // capture default (false) before potential changes
        // precedence: query string ?sim=1 overrides stored value
        var uri = new Uri(nav.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var simParam = query["sim"];
        if (simParam == "1")
        {
            useDemoMode = true;
            await PersistAsync();
            if (useDemoMode != previous) Changed?.Invoke();
            return;
        }
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", storageKey);
            if (stored == "1") useDemoMode = true;
        }
        catch { /* ignore */ }
        if (useDemoMode != previous) Changed?.Invoke();
    }

    public async Task SetDemoModeAsync(bool value)
    {
        if (useDemoMode == value) return;
        useDemoMode = value;
        await PersistAsync();
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", storageKey, useDemoMode ? "1" : "0");
        }
        catch { /* ignore */ }
    }
}
