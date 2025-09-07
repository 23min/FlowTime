namespace FlowTime.Sim.Service.Health;

public class HealthCheckResponse
{
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public ServiceInfo? Service { get; set; }
    public Dictionary<string, EndpointHealth>? Endpoints { get; set; }
    public Dictionary<string, DependencyHealth>? Dependencies { get; set; }
}

public class ServiceInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
}

public class EndpointHealth
{
    public string Status { get; set; } = "";
    public DateTime LastChecked { get; set; }
    public string? Description { get; set; }
    public object? Details { get; set; }
    public string? Error { get; set; }
}

public class DependencyHealth
{
    public string Status { get; set; } = "";
    public DateTime LastChecked { get; set; }
    public string? Description { get; set; }
    public object? Details { get; set; }
    public string? Error { get; set; }
}

public static class HealthStatus
{
    public const string Healthy = "healthy";
    public const string Degraded = "degraded";
    public const string Unhealthy = "unhealthy";
}
