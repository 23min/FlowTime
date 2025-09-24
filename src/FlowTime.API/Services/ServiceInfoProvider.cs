using FlowTime.API.Models;
using FlowTime.Core.Configuration;
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
    private readonly DateTime startTime;
    private readonly IWebHostEnvironment environment;
    private readonly IConfiguration configuration;

    public ServiceInfoProvider(IWebHostEnvironment environment, IConfiguration configuration)
    {
        startTime = DateTime.UtcNow;
        this.environment = environment;
        this.configuration = configuration;
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
                Environment = environment.EnvironmentName
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
            StartTime = startTime,
            Uptime = DateTime.UtcNow - startTime,
            Health = new HealthInfo
            {
                Status = "healthy",
                LastCheckTime = DateTime.UtcNow,
                Details = new HealthDetails
                {
                    DataDirectory = GetDataDirectory(),
                    RunsDirectory = GetRunsDirectory()
                }
            }
        };
    }

    private static string? GetCommitHash()
    {
        // Try to get commit hash from assembly metadata first (embedded at build time)
        var assembly = Assembly.GetExecutingAssembly();
        var gitHashAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitCommitHash");
        if (gitHashAttribute != null && !string.IsNullOrEmpty(gitHashAttribute.Value))
        {
            return gitHashAttribute.Value;
        }

        // Fallback to environment variable
        var commitHash = Environment.GetEnvironmentVariable("GIT_COMMIT");
        if (!string.IsNullOrEmpty(commitHash))
        {
            return commitHash.Length > 8 ? commitHash[..8] : commitHash;
        }

        return "unknown";
    }

    private static DateTime? GetBuildTime(Assembly assembly)
    {
        // Try to get build time from assembly metadata first (embedded at build time)
        var buildTimeAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "BuildTime");
        if (buildTimeAttribute != null && DateTime.TryParse(buildTimeAttribute.Value, out var buildTime))
        {
            return buildTime;
        }

        // Fallback to assembly file time
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

    private string GetDataDirectory()
    {
        // Use the same method as the rest of the API for consistency
        return Program.GetArtifactsDirectory(configuration);
    }

    private string GetRunsDirectory()
    {
        // FlowTime API stores runs directly in the data directory with run_ prefix
        return Program.ServiceHelpers.RunsRoot(configuration);
    }
}
