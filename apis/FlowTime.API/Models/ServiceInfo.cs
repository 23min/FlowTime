using System.Reflection;

namespace FlowTime.API.Models;

public record ServiceInfo
{
    public string Status { get; init; } = "healthy";
    public string Version { get; init; } = GetVersion();
    public string ApiVersion { get; init; } = "v1";
    public string Service { get; init; } = "flowtime-api";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public BuildInfo Build { get; init; } = new();
    public CapabilitiesInfo Capabilities { get; init; } = new();
    public DependenciesInfo Dependencies { get; init; } = new();
    public TimeSpan Uptime { get; init; }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.1.0";
    }
}

public record BuildInfo
{
    public string Commit { get; init; } = GetCommitHash();
    public DateTime BuildTime { get; init; } = GetBuildTime();
    public string Environment { get; init; } = GetEnvironment();

    private static string GetCommitHash()
    {
        // Try to get from environment variable (set by CI/build)
        var commit = Environment.GetEnvironmentVariable("BUILD_COMMIT");
        if (!string.IsNullOrEmpty(commit))
            return commit;

        // Fallback: try to get from git (development)
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short HEAD",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrEmpty(result) ? "unknown" : result;
        }
        catch
        {
            return "unknown";
        }
    }

    private static DateTime GetBuildTime()
    {
        var buildTime = Environment.GetEnvironmentVariable("BUILD_TIME");
        if (DateTime.TryParse(buildTime, out var parsed))
            return parsed;

        // Fallback to assembly build time approximation
        var assembly = Assembly.GetExecutingAssembly();
        return File.GetLastWriteTimeUtc(assembly.Location);
    }

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    }
}

public record CapabilitiesInfo
{
    public string[] Features { get; init; } = ["run", "graph", "artifacts", "series-streaming"];
    public string MaxModelSize { get; init; } = "10MB";
    public string[] SupportedFormats { get; init; } = ["yaml", "json"];
    public string[] SupportedContentTypes { get; init; } = ["text/plain", "application/yaml"];
}

public record DependenciesInfo
{
    public string DotNet { get; init; } = Environment.Version.ToString();
    public string FlowTimeCore { get; init; } = GetFlowTimeCoreVersion();

    private static string GetFlowTimeCoreVersion()
    {
        try
        {
            var assembly = Assembly.LoadFrom("FlowTime.Core.dll");
            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
