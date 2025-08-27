using MudBlazor;

namespace FlowTime.UI.Services;

public sealed class ThemeService
{
    public bool IsDarkMode { get; private set; } = false;

    public MudTheme CurrentTheme => IsDarkMode ? darkTheme : lightTheme;

    public event Action? Changed;

    public void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        Changed?.Invoke();
    }

    private static readonly MudTheme lightTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Colors.Blue.Darken2,
            Secondary = Colors.Indigo.Accent3,
        }
    };

    private static readonly MudTheme darkTheme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = Colors.Blue.Lighten2,
            Secondary = Colors.Indigo.Lighten3,
            Background = "#121212",
            Surface = "#1E1E1E",
        }
    };
}
