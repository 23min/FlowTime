using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowTime.UI;
using FlowTime.UI.Configuration;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection; // for AddHttpClient
using FlowTime.UI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Default HttpClient: points to the UI host (used for static asset fetches like /models/*.yaml)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Interface context service for dual interface support
builder.Services.AddScoped<FlowTime.UI.Services.Interface.IInterfaceContextService, FlowTime.UI.Services.Interface.InterfaceContextService>();
builder.Services.AddScoped<ILayoutService, LayoutService>();

// Port discovery service for API endpoint fallback
builder.Services.AddScoped<IPortDiscoveryService, PortDiscoveryService>();

// FlowTime API client (for engine/core operations)
builder.Services.AddScoped<IFlowTimeApiClient>(sp =>
{
	// Get configuration options
	var config = builder.Configuration.GetSection(FlowTimeApiOptions.SectionName).Get<FlowTimeApiOptions>() 
		?? new FlowTimeApiOptions();
	
	var apiHttp = new HttpClient 
	{ 
		BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/"),
		Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes)
	};
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
	var logger = sp.GetRequiredService<ILogger<SimResultsService>>();
	return new SimResultsService(simClient, apiClient, logger);
});

await builder.Build().RunAsync();
