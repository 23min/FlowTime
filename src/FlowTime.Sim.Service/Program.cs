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

// POST /sim/run  — accepts YAML simulation spec (text/plain or application/x-yaml) and returns { simRunId }
app.MapPost("/sim/run", async (HttpRequest req, CancellationToken ct) =>
{
	try
	{
		using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
		var yaml = await reader.ReadToEndAsync(ct);
		if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty body" });

		// Parse & validate
		SimulationSpec spec;
		try
		{
			spec = SimulationSpecLoader.LoadFromString(yaml);
		}
		catch (Exception ex)
		{
			return Results.BadRequest(new { error = "YAML parse failed", detail = ex.Message });
		}
		var validation = SimulationSpecValidator.Validate(spec);
		if (!validation.IsValid)
		{
			return Results.BadRequest(new { error = "Spec validation failed", errors = validation.Errors });
		}

		// Generate arrivals (deterministic)
		var arrivals = ArrivalGenerators.Generate(spec);

		// Runs root (parent of /runs directory) — defaults to current dir
		var runsRoot = Environment.GetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT");
		if (string.IsNullOrWhiteSpace(runsRoot)) runsRoot = Directory.GetCurrentDirectory();

		var artifacts = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(yaml, spec, arrivals, runsRoot, includeEvents: true, ct);

		return Results.Ok(new { simRunId = artifacts.RunId });
	}
	catch (OperationCanceledException)
	{
		return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
	}
	catch (Exception ex)
	{
		return Results.Problem(ex.Message);
	}
});

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
