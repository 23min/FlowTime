using MudBlazor;

namespace FlowTime.UI.Services;

public sealed class ThemeService
{
    public bool IsDark { get; private set; }

    public MudTheme CurrentTheme => IsDark ? DarkTheme : LightTheme;

    public event Action? Changed;

    public void Toggle()
    {
        IsDark = !IsDark;
        Changed?.Invoke();
    }

    public readonly MudTheme LightTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Colors.Indigo.Default,
            Secondary = Colors.DeepPurple.Accent2,
            Background = "#F5F7FA",
            Surface = "#FFFFFF",
            AppbarBackground = "#283593",
            DrawerBackground = "#FFFFFF",
            TextPrimary = "#1E1E1E",
            TextSecondary = "#555555",
            Divider = "#E0E0E0",
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
            Primary = Colors.Indigo.Lighten2,
            Secondary = Colors.DeepPurple.Accent2,
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "#1E1E1E",
            DrawerBackground = "#1E1E1E",
            TextPrimary = "#ECEFF1",
            TextSecondary = "rgba(255,255,255,0.70)",
            Divider = "#2C2C2C",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
        Typography = new Typography
        {
            Default = new Default { FontFamily = new[] { "Inter", "Segoe UI", "Arial", "sans-serif" } }
        }
    };
}
