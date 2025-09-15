using FlowTime.UI.Configuration;

namespace FlowTime.UI.Services;

public class FlowTimeSimApiClientWithFallback : IFlowTimeSimApiClient
{
    private readonly IPortDiscoveryService portDiscovery;
    private readonly IConfiguration configuration;
    private readonly ILogger<FlowTimeSimApiClientWithFallback> logger;
    private readonly IServiceProvider serviceProvider;
    
    private IFlowTimeSimApiClient? activeClient;
    private string? activeBaseUrl;

    public FlowTimeSimApiClientWithFallback(
        IPortDiscoveryService portDiscovery, 
        IConfiguration configuration,
        ILogger<FlowTimeSimApiClientWithFallback> logger,
        IServiceProvider serviceProvider)
    {
        this.portDiscovery = portDiscovery;
        this.configuration = configuration;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public string? BaseAddress => activeClient?.BaseAddress;

    private async Task<IFlowTimeSimApiClient> GetActiveClientAsync()
    {
        if (activeClient != null)
            return activeClient;

        // Get configuration
        var config = configuration.GetSection(FlowTimeSimApiOptions.SectionName).Get<FlowTimeSimApiOptions>() 
            ?? new FlowTimeSimApiOptions();

        // Discover available port
        var availableUrl = await portDiscovery.GetAvailableFlowTimeSimUrlAsync(config);
        
        // Create HttpClient with discovered URL
        var simHttp = new HttpClient 
        { 
            BaseAddress = new Uri(availableUrl),
            Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes)
        };
        
        var clientLogger = serviceProvider.GetRequiredService<ILogger<FlowTimeSimApiClient>>();
        activeClient = new FlowTimeSimApiClient(simHttp, clientLogger, config.ApiVersion);
        activeBaseUrl = availableUrl;
        
        logger.LogInformation("FlowTime-Sim API client initialized with URL: {Url}", availableUrl);
        
        return activeClient;
    }

    public async Task<Result<bool>> HealthAsync(CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.HealthAsync(ct);
    }

    public async Task<Result<object>> GetDetailedHealthAsync(CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.GetDetailedHealthAsync(ct);
    }

    public async Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.RunAsync(yaml, ct);
    }

    public async Task<Result<SeriesIndex>> GetIndexAsync(string runId, CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.GetIndexAsync(runId, ct);
    }

    public async Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.GetSeriesAsync(runId, seriesId, ct);
    }

    public async Task<Result<List<ScenarioInfo>>> GetScenariosAsync(CancellationToken ct = default)
    {
        var client = await GetActiveClientAsync();
        return await client.GetScenariosAsync(ct);
    }
}