using MudBlazor;
using Microsoft.JSInterop;

namespace FlowTime.UI.Services;

public sealed class ThemeService
{
    private readonly IJSRuntime js;
    private bool loaded;
    private const string storageKey = "ft.theme"; // values: dark|light

    public ThemeService(IJSRuntime js)
    {
        this.js = js;
    }

    public bool IsDark { get; private set; } = true; // default dark; EnsureLoadedAsync may override

    public MudTheme CurrentTheme => IsDark ? DarkTheme : LightTheme;

    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", storageKey);
            if (stored == "light") SetInternal(false, persist:false);
            else if (stored == "dark") SetInternal(true, persist:false);
        }
        catch { /* ignore */ }
    }

    public Task ToggleAsync() => SetAsync(!IsDark);

    public Task SetAsync(bool dark)
    {
        if (IsDark == dark) return Task.CompletedTask;
        SetInternal(dark, persist:true);
        return PersistAsync();
    }

    private void SetInternal(bool dark, bool persist)
    {
        IsDark = dark;
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", storageKey, IsDark ? "dark" : "light"); }
        catch { /* ignore */ }
    }

    public readonly MudTheme LightTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3F51B5",              // Indigo 500
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#7E57C2",            // Deep Purple 400
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#009688",
            Info = "#0288D1",
            Success = "#2E7D32",
            Warning = "#ED6C02",
            Error = "#D32F2F",
            Background = "#FAFBFD",           // very light
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1F2328",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1F2328",
            TextPrimary = "#1F2328",
            TextSecondary = "#525960",
            ActionDefault = "#5E6369",
            ActionDisabled = "#BFC5CC",
            ActionDisabledBackground = "#E4E7EB",
            Divider = "#E2E6EA",
            TableLines = "#E2E6EA",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
    // Typography customization removed for MudBlazor 8 upgrade: 'Default' style type no longer present.
    // TODO: Reintroduce with new style properties (e.g., Body1, Body2) if desired.
    };

    public readonly MudTheme DarkTheme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#90CAF9",              // Light blue 200 for dark contrast
            PrimaryContrastText = "#0B0D10",
            Secondary = "#B39DDB",            // Deep Purple 200
            SecondaryContrastText = "#0B0D10",
            Tertiary = "#80CBC4",
            Info = "#4FC3F7",
            Success = "#81C784",
            Warning = "#FFB74D",
            Error = "#EF9A9A",
            Background = "#161B21",           // match surface for uniform dark background
            Surface = "#161B21",              // elevated surfaces
            AppbarBackground = "#161B21",
            AppbarText = "#E6EAF0",
            DrawerBackground = "#161B21",
            DrawerText = "#E6EAF0",
            TextPrimary = "#E6EAF0",
            TextSecondary = "#B6BDC7",
            ActionDefault = "#E6EAF0",
            ActionDisabled = "#5A636E",
            ActionDisabledBackground = "#1F252B",
            Divider = "#242A31",
            TableLines = "#242A31",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
    // Typography customization removed for MudBlazor 8 upgrade.
    };
}
