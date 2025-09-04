using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowTime.UI;
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
builder.Services.AddScoped<FlowTimeApiOptions>(_ => new FlowTimeApiOptions());

// FlowTime API client (for engine/core operations)
builder.Services.AddScoped<IFlowTimeApiClient>(sp =>
{
	var opts = sp.GetRequiredService<FlowTimeApiOptions>();
	var apiHttp = new HttpClient { BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/") };
	return new FlowTimeApiClient(apiHttp, opts);
});

// FlowTime-Sim API client (for simulation operations) 
builder.Services.AddScoped<IFlowTimeSimApiClient>(sp =>
{
	// For UI-M2, FlowTime-Sim runs on port 8081 by default
	var simHttp = new HttpClient { BaseAddress = new Uri("http://localhost:8081/") };
	var logger = sp.GetRequiredService<ILogger<FlowTimeSimApiClient>>();
	return new FlowTimeSimApiClient(simHttp, logger);
});
builder.Services.AddScoped<FeatureFlagService>();
builder.Services.AddScoped<ApiRunClient>();
builder.Services.AddScoped<SimulationRunClient>();
builder.Services.AddScoped<IRunClient, RunClientRouter>();

// Template services for UI-M2 - Real API Integration
builder.Services.AddScoped<ITemplateService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var logger = sp.GetRequiredService<ILogger<TemplateService>>();
	return new TemplateService(simClient, logger);
});

builder.Services.AddScoped<ICatalogService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var logger = sp.GetRequiredService<ILogger<CatalogService>>();
	return new CatalogService(simClient, logger);
});

builder.Services.AddScoped<IFlowTimeSimService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var logger = sp.GetRequiredService<ILogger<FlowTimeSimService>>();
	return new FlowTimeSimService(simClient, logger);
});

// Simulation results service for artifact-first data loading
builder.Services.AddScoped<ISimResultsService>(sp =>
{
	var simClient = sp.GetRequiredService<IFlowTimeSimApiClient>();
	var logger = sp.GetRequiredService<ILogger<SimResultsService>>();
	return new SimResultsService(simClient, logger);
});

await builder.Build().RunAsync();
