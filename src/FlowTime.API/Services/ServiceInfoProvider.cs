using FlowTime.API.Models;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FlowTime.API.Services;

/// <summary>
/// Service for providing comprehensive service information including uptime tracking
/// </summary>
public interface IServiceInfoProvider
{
    ServiceInfo GetServiceInfo();
}

/// <summary>
/// Implementation of service information provider with startup time tracking
/// </summary>
public class ServiceInfoProvider : IServiceInfoProvider
{
    private readonly DateTime _startTime;
    private readonly IWebHostEnvironment _environment;

    public ServiceInfoProvider(IWebHostEnvironment environment)
    {
        _startTime = DateTime.UtcNow;
        _environment = environment;
    }

    public ServiceInfo GetServiceInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";
        
        return new ServiceInfo
        {
            ServiceName = "FlowTime.API",
            ApiVersion = "v1",
            Build = new BuildInfo
            {
                Version = version,
                CommitHash = GetCommitHash(),
                BuildTime = GetBuildTime(assembly),
                Environment = _environment.EnvironmentName
            },
            Capabilities = new CapabilitiesInfo
            {
                SupportedFormats = new[] { "yaml", "json", "csv" },
                Features = new[] { "grid-evaluation", "expression-parsing", "artifact-generation", "series-endpoints" },
                Limits = new Dictionary<string, object>
                {
                    ["maxNodes"] = 1000,
                    ["maxBins"] = 10080, // 1 week at 1-minute resolution
                    ["maxYamlSize"] = 1024 * 1024 // 1MB
                }
            },
            Dependencies = new DependenciesInfo
            {
                DotNetVersion = RuntimeInformation.FrameworkDescription,
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                Packages = new Dictionary<string, string>
                {
                    ["Microsoft.AspNetCore"] = "9.0.0",
                    ["YamlDotNet"] = "16.1.3",
                    ["FlowTime.Core"] = version
                }
            },
            Status = "healthy",
            StartTime = _startTime,
            Uptime = DateTime.UtcNow - _startTime
        };
    }

    private static string? GetCommitHash()
    {
        // Try to get commit hash from environment or assembly metadata
        var commitHash = Environment.GetEnvironmentVariable("GIT_COMMIT");
        if (!string.IsNullOrEmpty(commitHash))
        {
            return commitHash.Length > 8 ? commitHash[..8] : commitHash;
        }

        // Fallback: try to read from assembly metadata (if available)
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
        return attribute?.Value;
    }

    private static DateTime? GetBuildTime(Assembly assembly)
    {
        // Try to get build time from assembly
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return File.GetLastWriteTimeUtc(location);
            }
        }
        catch
        {
            // Ignore errors getting build time
        }

        return null;
    }
}
