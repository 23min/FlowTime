using System.Globalization;
using FlowTime.Sim.Core;

var builder = WebApplication.CreateBuilder(args);

// Basic services (CORS permissive for dev; tighten later)
builder.Logging.AddSimpleConsole(o =>
{
	o.SingleLine = true;
	o.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Services.AddCors(p => p.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// Health
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// POST /sim/run  (bootstrap) -- accepts YAML simulation spec in body (text/plain) and returns { simRunId }
// TODO: implement loading, validation, artifact writing (reuse RunArtifactsWriter) and return run id.
app.MapPost("/sim/run", () => Results.StatusCode(501));

// GET /sim/runs/{id}/index  (series/index.json)
app.MapGet("/sim/runs/{id}/index", (string id) => Results.StatusCode(501));

// GET /sim/runs/{id}/series/{seriesId}  (CSV stream)
app.MapGet("/sim/runs/{id}/series/{seriesId}", (string id, string seriesId) => Results.StatusCode(501));

// GET /sim/scenarios  (static list of scenario presets)
app.MapGet("/sim/scenarios", () => Results.StatusCode(501));

// POST /sim/overlay  (derive run from base + overlay)
app.MapPost("/sim/overlay", () => Results.StatusCode(501));

app.Lifetime.ApplicationStarted.Register(() =>
{
	var urls = string.Join(", ", app.Urls);
	app.Logger.LogInformation("FlowTime.Sim.Service started. Urls={Urls}", urls);
});

app.Run();

public partial class Program { }
