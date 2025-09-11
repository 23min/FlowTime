using FlowTime.Core.Configuration;

namespace FlowTime.Cli.Configuration;

/// <summary>
/// Provides output directory configuration with environment variable support
/// </summary>
public static class OutputDirectoryProvider
{
    /// <summary>
    /// Get the default output directory with environment variable support
    /// </summary>
    /// <returns>The output directory path</returns>
    public static string GetDefaultOutputDirectory()
    {
        return DirectoryProvider.GetDefaultDataDirectory();
    }
}
