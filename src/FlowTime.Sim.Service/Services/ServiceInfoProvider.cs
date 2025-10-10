using System.Reflection;
using System.Runtime.InteropServices;

namespace FlowTime.Sim.Service.Services;

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
    private readonly IConfiguration configuration;
    private readonly ICapabilitiesDetectionService capabilitiesService;

    public ServiceInfoProvider(IWebHostEnvironment environment, IConfiguration configuration, ICapabilitiesDetectionService capabilitiesService)
    {
        _startTime = DateTime.UtcNow;
        _environment = environment;
        this.configuration = configuration;
        this.capabilitiesService = capabilitiesService;
    }

    public ServiceInfo GetServiceInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";
        var serviceName = assembly.GetName().Name ?? "FlowTime.Sim.Service";
        
        return new ServiceInfo
        {
            ServiceName = serviceName,
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
                SupportedFormats = capabilitiesService.GetSupportedFormats(),
                Features = capabilitiesService.GetFeatures(),
                Limits = capabilitiesService.GetLimits()
            },
            Runtime = new RuntimeInfo
            {
                StartTime = _startTime,
                Uptime = DateTime.UtcNow - _startTime,
                Platform = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                FrameworkVersion = RuntimeInformation.FrameworkDescription
            },
            Health = new HealthInfo
            {
                Status = "healthy",
                LastCheckTime = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    { "dataDirectory", Program.ServiceHelpers.DataRoot(configuration) },
                    { "modelsDirectory", Program.ServiceHelpers.ModelsRoot(configuration) },
                    { "catalogsDirectory", Program.ServiceHelpers.CatalogsRoot(configuration) }
                }
            }
        };
    }

    private static string GetCommitHash()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var gitHashAttributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(attr => attr.Key == "GitCommitHash");
            return gitHashAttributes.FirstOrDefault()?.Value ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static DateTime? GetBuildTime(Assembly assembly)
    {
        try
        {
            var buildTimeAttributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(attr => attr.Key == "BuildTime");
            var buildTimeString = buildTimeAttributes.FirstOrDefault()?.Value;
            if (DateTime.TryParse(buildTimeString, out var buildTime))
                return buildTime;
        }
        catch
        {
            // Fall back to assembly creation time
        }
        
        // Fallback: use assembly file creation time
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                return File.GetCreationTimeUtc(location);
        }
        catch
        {
            // Ignore errors
        }
        
        return null;
    }
}

/// <summary>
/// Complete service information model
/// </summary>
public class ServiceInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public BuildInfo Build { get; set; } = new();
    public CapabilitiesInfo Capabilities { get; set; } = new();
    public RuntimeInfo Runtime { get; set; } = new();
    public HealthInfo Health { get; set; } = new();
}

/// <summary>
/// Build and version information
/// </summary>
public class BuildInfo
{
    public string Version { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public DateTime? BuildTime { get; set; }
    public string Environment { get; set; } = string.Empty;
}

/// <summary>
/// Service capabilities and feature information
/// </summary>
public class CapabilitiesInfo
{
    public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    public string[] Features { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Limits { get; set; } = new();
}

/// <summary>
/// Runtime environment information
/// </summary>
public class RuntimeInfo
{
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
}

/// <summary>
/// Health check information
/// </summary>
public class HealthInfo
{
    public string Status { get; set; } = string.Empty;
    public DateTime LastCheckTime { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}
