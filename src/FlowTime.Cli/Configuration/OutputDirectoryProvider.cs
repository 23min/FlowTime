using System;
using System.IO;

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
        // 1. Environment variable has highest precedence
        var envVar = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return envVar;
        }
        
        // 2. Default to ./data
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }
}
