using MudBlazor;

namespace FlowTime.UI.Services;

public sealed class ThemeService
{
    public bool IsDark { get; private set; } = false; // default; may be overridden by JS on startup

    public MudTheme CurrentTheme => IsDark ? DarkTheme : LightTheme;

    public event Action? Changed;

    public void Toggle() => Set(IsDark ? false : true);

    public void Set(bool dark)
    {
        if (IsDark == dark) return;
        IsDark = dark;
        Changed?.Invoke();
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
        Typography = new Typography
        {
            Default = new Default { FontFamily = new[] { "Inter", "Segoe UI", "Arial", "sans-serif" } }
        }
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
            Background = "#0B0D10",           // near-black
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
        Typography = new Typography
        {
            Default = new Default { FontFamily = new[] { "Inter", "Segoe UI", "Arial", "sans-serif" } }
        }
    };
}
