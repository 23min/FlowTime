using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowTime.UI;
using FlowTime.UI.Configuration;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection; // for AddHttpClient
using Microsoft.Extensions.Logging;
using FlowTime.UI.Services;
using System.Reflection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure logging to reduce HTTP client noise
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.Default.LogicalHandler", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.Default.ClientHandler", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.General.LogicalHandler", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.General.ClientHandler", LogLevel.Warning);

// Configure HttpClientFactory for proper HTTP client management
builder.Services.AddHttpClient();

// Default HttpClient for Blazor components (used for static asset fetches like /models/*.yaml)
builder.Services.AddScoped(sp => 
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient();
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    return client;
});

// Named HttpClient for UI host (used for static asset fetches like /models/*.yaml)
builder.Services.AddHttpClient("UI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

// Named HttpClient for FlowTime API
builder.Services.AddHttpClient("FlowTimeAPI", (sp, client) =>
{
    var config = builder.Configuration.GetSection(FlowTimeApiOptions.SectionName).Get<FlowTimeApiOptions>() 
        ?? new FlowTimeApiOptions();
    
    client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes);
});

// Named HttpClient for general API calls with short timeout
builder.Services.AddHttpClient("General", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddMudServices();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Interface context service for dual interface support
builder.Services.AddScoped<FlowTime.UI.Services.Interface.IInterfaceContextService, FlowTime.UI.Services.Interface.InterfaceContextService>();
builder.Services.AddScoped<ILayoutService, LayoutService>();

// Port discovery service for API endpoint fallback
builder.Services.AddScoped<IPortDiscoveryService, PortDiscoveryService>();

// FlowTime API client (for engine/core operations) - now using IHttpClientFactory
builder.Services.AddScoped<IFlowTimeApiClient>(sp =>
{
    var config = builder.Configuration.GetSection(FlowTimeApiOptions.SectionName).Get<FlowTimeApiOptions>() 
        ?? new FlowTimeApiOptions();
    
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var apiHttp = httpClientFactory.CreateClient("FlowTimeAPI");
    return new FlowTimeApiClient(apiHttp, config);
});

// FlowTime-Sim API client (for simulation operations) with port fallback
builder.Services.AddScoped<IFlowTimeSimApiClient, FlowTimeSimApiClientWithFallback>();
builder.Services.AddScoped<FeatureFlagService>();
builder.Services.AddScoped<ApiRunClient>();
builder.Services.AddScoped<SimulationRunClient>();
builder.Services.AddScoped<IRunClient, RunClientRouter>();

// Template services for UI-M2 - Real API Integration with mode switching
builder.Services.AddScoped<ITemplateService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var featureFlags = sp.GetRequiredService<FeatureFlagService>();
	var logger = sp.GetRequiredService<ILogger<TemplateService>>();
	return new TemplateService(simClient, featureFlags, logger);
});

builder.Services.AddScoped<ICatalogService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var featureFlags = sp.GetRequiredService<FeatureFlagService>();
	var logger = sp.GetRequiredService<ILogger<CatalogService>>();
	return new CatalogService(simClient, featureFlags, logger);
});

builder.Services.AddScoped<IFlowTimeSimService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var apiClient = sp.GetRequiredService<IFlowTimeApiClient>();
	var featureFlags = sp.GetRequiredService<FeatureFlagService>();
	var logger = sp.GetRequiredService<ILogger<FlowTimeSimService>>();
	var configuration = sp.GetRequiredService<IConfiguration>();
	return new FlowTimeSimService(simClient, apiClient, featureFlags, logger, configuration);
});

// Simulation results service for artifact-first data loading
builder.Services.AddScoped<ISimResultsService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var apiClient = sp.GetRequiredService<IFlowTimeApiClient>();
	var featureFlags = sp.GetRequiredService<FeatureFlagService>();
	var logger = sp.GetRequiredService<ILogger<SimResultsService>>();
	return new SimResultsService(simClient, apiClient, featureFlags, logger);
});

// Shared services for graph analysis and simulation results
builder.Services.AddScoped<IGraphAnalysisService, GraphAnalysisService>();
builder.Services.AddScoped<ISimulationResultsService, SimulationResultsService>();
builder.Services.AddScoped<IRunDiscoveryService, RunDiscoveryService>();
builder.Services.AddScoped<ITimeTravelDataService, TimeTravelDataService>();
builder.Services.AddScoped<ITimeTravelMetricsClient, TimeTravelMetricsClient>();

var host = builder.Build();
var informationalVersion = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
var version = informationalVersion?.Split('+')[0]
    ?? typeof(App).Assembly.GetName().Version?.ToString()
    ?? "0.0.0";
Console.WriteLine($"FlowTime.UI started v{version}");

await host.RunAsync();
