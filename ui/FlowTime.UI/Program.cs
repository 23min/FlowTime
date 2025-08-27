using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowTime.UI;
using MudBlazor.Services;
using FlowTime.UI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddScoped<FlowTimeApiOptions>(_ => new FlowTimeApiOptions());
builder.Services.AddScoped<IFlowTimeApiClient, FlowTimeApiClient>();

await builder.Build().RunAsync();
