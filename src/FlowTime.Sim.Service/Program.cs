using System.Globalization;
using FlowTime.Sim.Core;
using FlowTime.Sim.Service; // ScenarioRegistry

// Explicit Program class for integration tests & clear structure
public class Program
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
app.MapGet("/sim/scenarios", () => Results.Ok(ScenarioRegistry.List()));

// POST /sim/overlay  (derive run from base + overlay)
// Body JSON: { baseRunId: string, overlay: { seed?, grid?, arrivals? } }
app.MapPost("/sim/overlay", async (HttpRequest req, CancellationToken ct) =>
{
	try
	{
		using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
		var json = await reader.ReadToEndAsync(ct);
		if (string.IsNullOrWhiteSpace(json)) return Results.BadRequest(new { error = "Empty body" });
		OverlayRequest? body;
		try
		{
			body = System.Text.Json.JsonSerializer.Deserialize<OverlayRequest>(json, new System.Text.Json.JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
		}
		catch (Exception ex)
		{
			return Results.BadRequest(new { error = "Invalid JSON", detail = ex.Message });
		}
		if (body is null || string.IsNullOrWhiteSpace(body.BaseRunId)) return Results.BadRequest(new { error = "baseRunId required" });
		if (!ServiceHelpers.IsSafeId(body.BaseRunId)) return Results.BadRequest(new { error = "Invalid baseRunId" });

		var root = ServiceHelpers.RunsRoot();
		var baseDir = Path.Combine(root, "runs", body.BaseRunId);
		var specPath = Path.Combine(baseDir, "spec.yaml");
		if (!File.Exists(specPath)) return Results.NotFound(new { error = "Base run spec not found" });
		var baseYaml = await File.ReadAllTextAsync(specPath, ct);
		var spec = SimulationSpecLoader.LoadFromString(baseYaml);

		// Apply shallow overlay
		if (body.Overlay is not null)
		{
			if (body.Overlay.Seed.HasValue) spec.seed = body.Overlay.Seed.Value;
			if (body.Overlay.Grid is not null)
			{
				spec.grid ??= new GridSpec();
				if (body.Overlay.Grid.Bins.HasValue) spec.grid.bins = body.Overlay.Grid.Bins.Value;
				if (body.Overlay.Grid.BinMinutes.HasValue) spec.grid.binMinutes = body.Overlay.Grid.BinMinutes.Value;
			}
			if (body.Overlay.Arrivals is not null)
			{
				spec.arrivals ??= new ArrivalsSpec();
				if (!string.IsNullOrWhiteSpace(body.Overlay.Arrivals.Kind)) spec.arrivals.kind = body.Overlay.Arrivals.Kind;
				if (body.Overlay.Arrivals.Values is not null) spec.arrivals.values = body.Overlay.Arrivals.Values.ToList();
				if (body.Overlay.Arrivals.Rate.HasValue)
				{
					spec.arrivals.rate = body.Overlay.Arrivals.Rate.Value;
					spec.arrivals.rates = null; // precedence
				}
				if (body.Overlay.Arrivals.Rates is not null)
				{
					spec.arrivals.rates = body.Overlay.Arrivals.Rates.ToList();
					spec.arrivals.rate = null; // precedence
					}
				}
		}

		var validation = SimulationSpecValidator.Validate(spec);
		if (!validation.IsValid)
		{
			return Results.BadRequest(new { error = "Overlay validation failed", errors = validation.Errors });
		}

		var arrivals = ArrivalGenerators.Generate(spec);
		var artifacts = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(SerializeSpec(spec), spec, arrivals, root, includeEvents: true, ct);
		return Results.Ok(new { simRunId = artifacts.RunId });
	}
	catch (Exception ex)
	{
		return Results.BadRequest(new { error = ex.Message });
	}
});

		app.Lifetime.ApplicationStarted.Register(() =>
		{
			var urls = string.Join(", ", app.Urls);
			app.Logger.LogInformation("FlowTime.Sim.Service started. Urls={Urls}", urls);
		});

		app.Run();
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

	// Overlay DTOs
	public sealed class OverlayRequest
	{
		public string BaseRunId { get; set; } = string.Empty;
		public OverlayPatch? Overlay { get; set; }
	}

	public sealed class OverlayPatch
	{
		public int? Seed { get; set; }
		public OverlayGrid? Grid { get; set; }
		public OverlayArrivals? Arrivals { get; set; }
	}

	public sealed class OverlayGrid
	{
		public int? Bins { get; set; }
		public int? BinMinutes { get; set; }
	}

	public sealed class OverlayArrivals
	{
		public string? Kind { get; set; }
		public IEnumerable<double>? Values { get; set; }
		public double? Rate { get; set; }
		public IEnumerable<double>? Rates { get; set; }
	}


	static string SerializeSpec(SimulationSpec spec)
	{
	// Minimal YAML serializer for persistence; for now we dump JSON-compatible YAML using simple StringBuilder.
	// (Future: switch to a proper YAML emitter if formatting stability is required.)
	var sb = new System.Text.StringBuilder();
	sb.AppendLine("schemaVersion: " + (spec.schemaVersion ?? 1));
	if (!string.IsNullOrWhiteSpace(spec.rng)) sb.AppendLine("rng: " + spec.rng);
	if (spec.seed.HasValue) sb.AppendLine("seed: " + spec.seed.Value);
	if (spec.grid is not null)
	{
		sb.AppendLine("grid:");
		if (spec.grid.bins.HasValue) sb.AppendLine("  bins: " + spec.grid.bins.Value);
		if (spec.grid.binMinutes.HasValue) sb.AppendLine("  binMinutes: " + spec.grid.binMinutes.Value);
		if (!string.IsNullOrWhiteSpace(spec.grid.start)) sb.AppendLine("  start: " + spec.grid.start);
	}
	if (spec.arrivals is not null)
	{
		sb.AppendLine("arrivals:");
		if (!string.IsNullOrWhiteSpace(spec.arrivals.kind)) sb.AppendLine("  kind: " + spec.arrivals.kind);
		if (spec.arrivals.values is not null)
		{
			sb.AppendLine("  values: [" + string.Join(',', spec.arrivals.values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]");
		}
		else if (spec.arrivals.rate.HasValue)
		{
			sb.AppendLine("  rate: " + spec.arrivals.rate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
		else if (spec.arrivals.rates is not null)
		{
			sb.AppendLine("  rates: [" + string.Join(',', spec.arrivals.rates.Select(r => r.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]");
		}
	}
	if (spec.route is not null && !string.IsNullOrWhiteSpace(spec.route.id))
	{
		sb.AppendLine("route:");
		sb.AppendLine("  id: " + spec.route.id);
	}
	return sb.ToString();
    }
}
