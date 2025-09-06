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

// Initialize catalogs during startup
ServiceHelpers.EnsureRuntimeCatalogs(app.Configuration);

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

		// Data root (parent of /runs and /catalogs directories) — uses configuration precedence
		var dataRoot = ServiceHelpers.DataRoot(app.Configuration);
		app.Logger.LogInformation("Creating simulation run in: {DataRoot}", dataRoot);

		var artifacts = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(yaml, spec, arrivals, dataRoot, includeEvents: false, ct);

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
	var runsRoot = ServiceHelpers.RunsRoot(app.Configuration);
	var path = Path.Combine(runsRoot, id, "series", "index.json");
	if (!File.Exists(path)) return Results.NotFound(new { error = "Not found" });
	// Return file contents (small JSON)
	var json = File.ReadAllText(path);
	return Results.Content(json, "application/json");
});

// GET /sim/runs/{id}/series/{seriesId}  (CSV stream)
app.MapGet("/sim/runs/{id}/series/{seriesId}", (string id, string seriesId) =>
{
	if (!ServiceHelpers.IsSafeId(id) || !ServiceHelpers.IsSafeSeriesId(seriesId)) return Results.BadRequest(new { error = "Invalid id" });
	var runsRoot = ServiceHelpers.RunsRoot(app.Configuration);
	var path = Path.Combine(runsRoot, id, "series", seriesId + ".csv");
	if (!File.Exists(path)) return Results.NotFound(new { error = "Not found" });
	var stream = File.OpenRead(path);
	return Results.File(stream, contentType: "text/csv", fileDownloadName: seriesId + ".csv");
});

// GET /sim/scenarios  (static list of scenario presets)
app.MapGet("/sim/scenarios", () => Results.Ok(ScenarioRegistry.List()));

// === CATALOG ENDPOINTS (SIM-CAT-M2 Phase 2) ===

// GET /sim/catalogs  → list catalogs (id, title, hash)
app.MapGet("/sim/catalogs", () =>
{
	try
	{
		var catalogsRoot = ServiceHelpers.CatalogsRoot(app.Configuration);
		if (!Directory.Exists(catalogsRoot))
		{
			return Results.Ok(new { catalogs = Array.Empty<object>() });
		}

		var catalogFiles = Directory.GetFiles(catalogsRoot, "*.yaml", SearchOption.TopDirectoryOnly);
		var catalogs = new List<object>();

		foreach (var filePath in catalogFiles)
		{
			try
			{
				var catalog = CatalogIO.ReadCatalogFromFile(filePath);
				var hash = CatalogIO.ComputeCatalogHash(catalog);
				var fileId = Path.GetFileNameWithoutExtension(filePath);
				
				catalogs.Add(new 
				{
					id = fileId,
					title = catalog.Metadata.Title ?? fileId,
					description = catalog.Metadata.Description,
					hash = hash,
					componentCount = catalog.Components.Count,
					connectionCount = catalog.Connections.Count
				});
			}
			catch (Exception ex)
			{
				// Log and skip invalid catalogs
				Console.WriteLine($"Warning: Failed to read catalog {filePath}: {ex.Message}");
			}
		}

		return Results.Ok(new { catalogs });
	}
	catch (Exception ex)
	{
		return Results.Problem(ex.Message);
	}
});

// GET /sim/catalogs/{id}  → returns Catalog.v1
app.MapGet("/sim/catalogs/{id}", (string id) =>
{
	try
	{
		if (!ServiceHelpers.IsSafeCatalogId(id)) 
			return Results.BadRequest(new { error = "Invalid catalog id" });

		var catalogsRoot = ServiceHelpers.CatalogsRoot(app.Configuration);
		var filePath = Path.Combine(catalogsRoot, id + ".yaml");
		
		if (!File.Exists(filePath))
			return Results.NotFound(new { error = "Catalog not found" });

		var catalog = CatalogIO.ReadCatalogFromFile(filePath);
		return Results.Ok(catalog);
	}
	catch (Exception ex)
	{
		return Results.BadRequest(new { error = "Failed to read catalog", detail = ex.Message });
	}
});

// POST /sim/catalogs/validate  → schema + referential integrity
app.MapPost("/sim/catalogs/validate", async (HttpRequest req, CancellationToken ct) =>
{
	try
	{
		using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
		var yaml = await reader.ReadToEndAsync(ct);
		if (string.IsNullOrWhiteSpace(yaml)) 
			return Results.BadRequest(new { error = "Empty body" });

		// Parse the catalog
		Catalog catalog;
		try
		{
			catalog = CatalogIO.ParseCatalogFromYaml(yaml);
		}
		catch (Exception ex)
		{
			return Results.BadRequest(new { error = "YAML parse failed", detail = ex.Message });
		}

		// Validate the catalog
		var validation = catalog.Validate();
		
		if (validation.IsValid)
		{
			var hash = CatalogIO.ComputeCatalogHash(catalog);
			return Results.Ok(new 
			{
				valid = true, 
				hash = hash,
				componentCount = catalog.Components.Count,
				connectionCount = catalog.Connections.Count
			});
		}
		else
		{
			return Results.BadRequest(new 
			{
				valid = false, 
				errors = validation.Errors
			});
		}
	}
	catch (Exception ex)
	{
		return Results.BadRequest(new { error = "Validation failed", detail = ex.Message });
	}
});

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

		var runsRoot = ServiceHelpers.RunsRoot(app.Configuration);
		var baseDir = Path.Combine(runsRoot, body.BaseRunId);
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
		var dataRoot = ServiceHelpers.DataRoot(app.Configuration);
		var artifacts = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(SerializeSpec(spec), spec, arrivals, dataRoot, includeEvents: false, ct);
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
		/// <summary>
		/// Gets the root data directory (parent of runs and catalogs).
		/// Order of precedence:
		/// 1. Environment variable FLOWTIME_SIM_DATA_DIR
		/// 2. Configuration FlowTimeSim:DataDir
		/// 3. Default: "./data"
		/// </summary>
		public static string DataRoot(IConfiguration? configuration = null)
		{
			// Check primary data directory environment variable first
			var dataDir = Environment.GetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR");
			if (!string.IsNullOrWhiteSpace(dataDir))
			{
				Directory.CreateDirectory(dataDir);
				return dataDir;
			}

			// Check configuration if provided
			if (configuration != null)
			{
				// Check primary data directory configuration
				var configDataDir = configuration["FlowTimeSim:DataDir"];
				if (!string.IsNullOrEmpty(configDataDir))
				{
					Directory.CreateDirectory(configDataDir);
					return configDataDir;
				}
			}

			// Default to ./data directory
			var defaultRoot = "./data";
			Directory.CreateDirectory(defaultRoot);
			return defaultRoot;
		}

		/// <summary>
		/// Gets the root directory for simulation runs.
		/// Order of precedence:
		/// 1. Environment variable FLOWTIME_SIM_DATA_DIR + "/runs"
		/// 2. Configuration FlowTimeSim:DataDir + "/runs"
		/// 3. Default: "./data/runs"
		/// </summary>
		public static string RunsRoot(IConfiguration? configuration = null)
		{
			var dataDir = DataRoot(configuration);
			var runsDir = Path.Combine(dataDir, "runs");
			Directory.CreateDirectory(runsDir);
			return runsDir;
		}

		/// <summary>
		/// Ensures runtime catalogs directory exists and is populated with demo catalogs if empty.
		/// Copies source catalogs to runtime location during startup for consistent behavior.
		/// </summary>
		public static void EnsureRuntimeCatalogs(IConfiguration? configuration = null)
		{
			var dataDir = DataRoot(configuration);
			var runtimeCatalogsDir = Path.Combine(dataDir, "catalogs");
			
			// Find workspace root by looking for the solution file
			var currentDir = Directory.GetCurrentDirectory();
			var workspaceRoot = currentDir;
			while (!File.Exists(Path.Combine(workspaceRoot, "FlowTimeSim.sln")) && Directory.GetParent(workspaceRoot) != null)
			{
				workspaceRoot = Directory.GetParent(workspaceRoot)!.FullName;
			}
			var sourceCatalogsDir = Path.Combine(workspaceRoot, "catalogs");
			
			// Create runtime catalogs directory if it doesn't exist
			Directory.CreateDirectory(runtimeCatalogsDir);
			
			// Copy demo catalogs if runtime directory is empty or doesn't have .yaml files
			if (Directory.GetFiles(runtimeCatalogsDir, "*.yaml").Length == 0)
			{
				if (Directory.Exists(sourceCatalogsDir))
				{
					foreach (var sourceFile in Directory.GetFiles(sourceCatalogsDir, "*.yaml"))
					{
						var fileName = Path.GetFileName(sourceFile);
						var destFile = Path.Combine(runtimeCatalogsDir, fileName);
						// Don't overwrite existing user customizations
						if (!File.Exists(destFile))
						{
							File.Copy(sourceFile, destFile);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the root directory for catalogs.
		/// Always returns the runtime catalogs directory after ensuring it's populated.
		/// </summary>
		public static string CatalogsRoot(IConfiguration? configuration = null)
		{
			var dataDir = DataRoot(configuration);
			var runtimeCatalogsDir = Path.Combine(dataDir, "catalogs");
			
			// Ensure runtime catalogs are set up
			EnsureRuntimeCatalogs(configuration);
			
			return runtimeCatalogsDir;
		}

		public static bool IsSafeId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
		public static bool IsSafeSeriesId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '@');
		public static bool IsSafeCatalogId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.');
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
