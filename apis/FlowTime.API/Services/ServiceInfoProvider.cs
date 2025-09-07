using FlowTime.API.Models;

namespace FlowTime.API.Services;

public interface IServiceInfoProvider
{
    ServiceInfo GetServiceInfo();
}

public class ServiceInfoProvider : IServiceInfoProvider
{
    private readonly DateTime _startTime;

    public ServiceInfoProvider()
    {
        _startTime = DateTime.UtcNow;
    }

    public ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Status = "healthy", // Could be enhanced with actual health checks
            Uptime = DateTime.UtcNow - _startTime
        };
    }
}
