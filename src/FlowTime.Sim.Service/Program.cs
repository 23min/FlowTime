using System.Globalization;
using FlowTime.Sim.Core;

// Entry point wrapped in explicit class so we can define helper static class below without top-level ordering issues.
public partial class Program
{
	public static void Main(string[] args)
	{
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
app.MapGet("/sim/runs/{id}/index", (string id) =>
{
	if (!ServiceHelpers.IsSafeId(id)) return Results.BadRequest(new { error = "Invalid id" });
	var root = ServiceHelpers.RunsRoot();
	var path = Path.Combine(root, "runs", id, "series", "index.json");
	if (!File.Exists(path)) return Results.NotFound(new { error = "Not found" });
	// Return file contents (small JSON)
	var json = File.ReadAllText(path);
	return Results.Content(json, "application/json");
});

// GET /sim/runs/{id}/series/{seriesId}  (CSV stream)
app.MapGet("/sim/runs/{id}/series/{seriesId}", (string id, string seriesId) =>
{
	if (!ServiceHelpers.IsSafeId(id) || !ServiceHelpers.IsSafeSeriesId(seriesId)) return Results.BadRequest(new { error = "Invalid id" });
	var root = ServiceHelpers.RunsRoot();
	var path = Path.Combine(root, "runs", id, "series", seriesId + ".csv");
	if (!File.Exists(path)) return Results.NotFound(new { error = "Not found" });
	var stream = File.OpenRead(path);
	return Results.File(stream, contentType: "text/csv", fileDownloadName: seriesId + ".csv");
});

// GET /sim/scenarios  (static list of scenario presets)
app.MapGet("/sim/scenarios", () => Results.Ok(Service.ScenarioRegistry.List()));

// POST /sim/overlay  (derive run from base + overlay)
app.MapPost("/sim/overlay", () => Results.StatusCode(501));

		app.Lifetime.ApplicationStarted.Register(() =>
		{
			var urls = string.Join(", ", app.Urls);
			app.Logger.LogInformation("FlowTime.Sim.Service started. Urls={Urls}", urls);
		});

		app.Run();
	}
}

// Helper utilities
static class ServiceHelpers
{
	public static string RunsRoot()
	{
		var runsRoot = Environment.GetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT");
		if (string.IsNullOrWhiteSpace(runsRoot)) runsRoot = Directory.GetCurrentDirectory();
		return runsRoot;
	}
	public static bool IsSafeId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
	public static bool IsSafeSeriesId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '@');
}
