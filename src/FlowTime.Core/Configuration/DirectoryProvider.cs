using System;
using System.IO;

namespace FlowTime.Core.Configuration;

/// <summary>
/// Provides consistent directory paths for FlowTime applications
/// </summary>
public static class DirectoryProvider
{
    /// <summary>
    /// Get the default data directory with consistent solution-relative path
    /// </summary>
    /// <returns>The data directory path</returns>
    public static string GetDefaultDataDirectory()
    {
        // 1. Environment variable has highest precedence
        var envVar = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return envVar;
        }
        
        // 2. Find solution root and use <solution-root>/data
        var solutionRoot = FindSolutionRoot();
        if (!string.IsNullOrEmpty(solutionRoot))
        {
            return Path.Combine(solutionRoot, "data");
        }
        
        // 3. Fallback to current directory data (original behavior)
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }
    
    /// <summary>
    /// Find the solution root directory by looking for FlowTime.sln
    /// </summary>
    /// <returns>The solution root path, or null if not found</returns>
    public static string? FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var directoryInfo = new DirectoryInfo(currentDir);
        
        // Walk up the directory tree looking for FlowTime.sln
        while (directoryInfo != null)
        {
            var solutionFile = Path.Combine(directoryInfo.FullName, "FlowTime.sln");
            if (File.Exists(solutionFile))
            {
                return directoryInfo.FullName;
            }
            
            directoryInfo = directoryInfo.Parent;
        }
        
        return null;
    }
}
