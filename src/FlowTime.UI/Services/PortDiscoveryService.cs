using FlowTime.UI.Configuration;
using System.Net.Http;

namespace FlowTime.UI.Services;

public interface IPortDiscoveryService
{
    Task<string> GetAvailableFlowTimeSimUrlAsync(FlowTimeSimApiOptions options);
}

public class PortDiscoveryService : IPortDiscoveryService
{
    private readonly ILogger<PortDiscoveryService> logger;
    private readonly IHttpClientFactory httpClientFactory;

    public PortDiscoveryService(ILogger<PortDiscoveryService> logger, IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAvailableFlowTimeSimUrlAsync(FlowTimeSimApiOptions options)
    {
        // Create list of URLs to try in order
        var urlsToTry = new List<string> { options.BaseUrl };
        urlsToTry.AddRange(options.FallbackUrls);

        foreach (var url in urlsToTry)
        {
            if (await IsPortAvailableAsync(url))
            {
                logger.LogInformation("FlowTime-Sim API available at: {Url}", url);
                return url;
            }
            
            logger.LogDebug("FlowTime-Sim API not available at: {Url}", url);
        }

        // If none work, return the primary URL anyway (will fail but that's expected behavior)
        logger.LogWarning("No FlowTime-Sim API endpoints available, using primary URL: {Url}", options.BaseUrl);
        return options.BaseUrl;
    }

    private async Task<bool> IsPortAvailableAsync(string baseUrl)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2); // Quick timeout for discovery
            
            // Try the versioned health check endpoint
            var uri = new Uri(baseUrl.TrimEnd('/') + "/v1/healthz");
            var response = await client.GetAsync(uri);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogDebug("Port availability check failed for {Url}: {Error}", baseUrl, ex.Message);
            return false;
        }
    }
}
