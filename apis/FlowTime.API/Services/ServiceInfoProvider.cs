using FlowTime.API.Models;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FlowTime.API.Services;

public interface IServiceInfoProvider
{
    ServiceInfo GetServiceInfo();
}

public class ServiceInfoProvider : IServiceInfoProvider
{
    private readonly DateTime _startTime;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public ServiceInfoProvider(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _startTime = DateTime.UtcNow;
        _environment = environment;
        _configuration = configuration;
    }

    public ServiceInfo GetServiceInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
        var serviceName = assembly.GetName().Name ?? "FlowTime.API";
        
        return new ServiceInfo
        {
            Status = "healthy",
            Service = serviceName,
            ApiVersion = "v1",
            Version = version,
            Timestamp = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - _startTime,
            Build = new BuildInfo
            {
                Environment = _environment.EnvironmentName
            },
            Capabilities = new CapabilitiesInfo(),
            Dependencies = new DependenciesInfo()
        };
    }
}
