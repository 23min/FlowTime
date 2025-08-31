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
// Dedicated API HttpClient instance (separate from the default static-files client).
builder.Services.AddScoped<IFlowTimeApiClient>(sp =>
{
	var opts = sp.GetRequiredService<FlowTimeApiOptions>();
	var apiHttp = new HttpClient { BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/") };
	return new FlowTimeApiClient(apiHttp, opts);
});
builder.Services.AddScoped<FeatureFlagService>();
builder.Services.AddScoped<ApiRunClient>();
builder.Services.AddScoped<SimulationRunClient>();
builder.Services.AddScoped<IRunClient, RunClientRouter>();

await builder.Build().RunAsync();
