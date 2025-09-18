using FlowTime.UI.Configuration;

namespace FlowTime.UI.Services;

public interface IPortDiscoveryService
{
    Task<string> GetAvailableFlowTimeSimUrlAsync(FlowTimeSimApiOptions options);
}

public class PortDiscoveryService : IPortDiscoveryService
{
    private readonly ILogger<PortDiscoveryService> logger;

    public PortDiscoveryService(ILogger<PortDiscoveryService> logger)
    {
        this.logger = logger;
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
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(2); // Quick timeout for discovery
            
            // Try a simple health check or basic connection
            var uri = new Uri(baseUrl.TrimEnd('/') + "/healthz");
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