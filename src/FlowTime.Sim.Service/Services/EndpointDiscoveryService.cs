using Microsoft.AspNetCore.Routing;

namespace FlowTime.Sim.Service.Services;

/// <summary>
/// Service for discovering available endpoints dynamically
/// </summary>
public interface IEndpointDiscoveryService
{
    string[] GetAvailableEndpoints();
}

/// <summary>
/// Implementation that discovers endpoints from the routing system
/// </summary>
public class EndpointDiscoveryService : IEndpointDiscoveryService
{
    private readonly IServiceProvider serviceProvider;

    public EndpointDiscoveryService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public string[] GetAvailableEndpoints()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var endpointDataSource = scope.ServiceProvider.GetService<EndpointDataSource>();
            
            if (endpointDataSource == null)
                return GetFallbackEndpoints();

            var endpoints = endpointDataSource.Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText != null)
                .Select(e => e.RoutePattern.RawText!)
                .Distinct()
                .OrderBy(e => e)
                .ToArray();

            return endpoints.Any() ? endpoints : GetFallbackEndpoints();
        }
        catch
        {
            // Fallback if endpoint discovery fails
            return GetFallbackEndpoints();
        }
    }

    private static string[] GetFallbackEndpoints()
    {
        // Minimal fallback - only the endpoints we know must exist
        return new[] { "/healthz", "/v1/healthz" };
    }
}
