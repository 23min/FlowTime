using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlowTime.Sim.Core;
using FlowTime.Sim.Service; // TemplateRegistry
using FlowTime.Sim.Service.Services; // ServiceInfoProvider
using FlowTime.Sim.Service.Extensions; // TemplateValidationExtensions

// Explicit Program class for integration tests & clear structure
public partial class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

// Basic services (CORS permissive for dev; tighten later)
builder.Logging.AddSimpleConsole(o =>
{
	o.SingleLine = true;
	o.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Services.AddCors(p => p.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Register services
builder.Services.AddSingleton<IServiceInfoProvider, ServiceInfoProvider>();
builder.Services.AddSingleton<IEndpointDiscoveryService, EndpointDiscoveryService>();
builder.Services.AddSingleton<ICapabilitiesDetectionService, CapabilitiesDetectionService>();
builder.Services.AddSingleton<FlowTime.Sim.Core.Services.INodeBasedTemplateService>(provider =>
{
	var logger = provider.GetRequiredService<ILogger<FlowTime.Sim.Core.Services.NodeBasedTemplateService>>();
	var templatesDirectory = ServiceHelpers.TemplatesRoot(builder.Configuration);
	return new FlowTime.Sim.Core.Services.NodeBasedTemplateService(templatesDirectory, logger);
});
builder.Services.AddSingleton<FlowTime.Sim.Core.Services.IProvenanceService, FlowTime.Sim.Core.Services.ProvenanceService>();

var app = builder.Build();
app.UseCors();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// Access log (one-liner per request)
app.Use(async (ctx, next) =>
{
	var sw = Stopwatch.StartNew();
	try
	{
		await next();
	}
	finally
	{
		sw.Stop();
		var method = ctx.Request.Method;
		var path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value : "/";
		var status = ctx.Response?.StatusCode;
		app.Logger.LogInformation("HTTP {Method} {Path} -> {Status} in {ElapsedMs} ms",
			method, path, status, sw.ElapsedMilliseconds);
	}
});

// Initialize catalogs during startup
ServiceHelpers.EnsureRuntimeCatalogs(app.Configuration);

// Health endpoints - simple and factual
app.MapGet("/healthz", (HttpContext context, IConfiguration config, IEndpointDiscoveryService endpointService) =>
{
    // Check for detailed health parameter
    var includeDetails = context.Request.Query.ContainsKey("detailed") || 
                        context.Request.Query.ContainsKey("include-details");
    
    if (includeDetails)
    {
        // Enhanced but simple health response with only factual information
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var serviceName = assembly.GetName().Name ?? "FlowTime.Sim.Service";
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        
        return Results.Ok(new
        {
            status = "ok",
            service = serviceName,
            version = version,
            timestamp = DateTime.UtcNow,
            uptime = DateTime.UtcNow - process.StartTime,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            dataDirectory = ServiceHelpers.DataRoot(config),
            system = new
            {
                workingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                platform = Environment.OSVersion.Platform.ToString(),
                architecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            availableEndpoints = endpointService.GetAvailableEndpoints()
        });
    }
    else
    {
        // Legacy basic response
        return Results.Ok(new { status = "ok" });
    }
});

// Enhanced health endpoint with service information (v1)
app.MapGet("/v1/healthz", (IServiceInfoProvider serviceInfoProvider, HttpContext context, IConfiguration config) =>
{
    // Check for detailed health parameter
    var includeDetails = context.Request.Query.ContainsKey("detailed") || 
                        context.Request.Query.ContainsKey("include-details");
    
    if (includeDetails)
    {
        // Enhanced but simple health response with only factual information
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return Results.Ok(new
        {
            status = "ok",
            service = "FlowTime.Sim.Service",
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            uptime = DateTime.UtcNow - process.StartTime,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            dataDirectory = ServiceHelpers.DataRoot(config),
            system = new
            {
                workingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                platform = Environment.OSVersion.Platform.ToString(),
                architecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            availableEndpoints = new[]
            {
                "/healthz",
                "/v1/healthz",
                "/api/v1/templates",
                "/api/v1/templates/{id}",
                "/api/v1/templates/{id}/generate",
                "/api/v1/templates/categories",
                "/api/v1/models",
                "/api/v1/models/{templateId}",
                "/api/v1/catalogs",
                "/api/v1/catalogs/{id}",
                "/api/v1/catalogs/validate"
            }
        });
    }
    else
    {
        // Standard v1 health with service info
        var serviceInfo = serviceInfoProvider.GetServiceInfo();
        return Results.Ok(serviceInfo);
    }
});

		// Modern RESTful endpoints under /api/v1
		var api = app.MapGroup("/api/v1");

		// API: GET /api/v1/templates  (list all templates)
		api.MapGet("/templates", async (FlowTime.Sim.Core.Services.INodeBasedTemplateService templateService, string? category) =>
		{
			var items = await templateService.GetAllTemplatesAsync();
			
			var templates = items.Select(t => new
			{
				id = t.Metadata.Id,
				title = t.Metadata.Title ?? t.Metadata.Id,
				description = t.Metadata.Description ?? string.Empty,
				category = "general", // node-based templates don't carry category yet
				tags = t.Metadata.Tags
			});
			
			if (!string.IsNullOrEmpty(category))
			{
				templates = templates.Where(t => string.Equals(t.category, category, StringComparison.OrdinalIgnoreCase));
			}
			return Results.Ok(templates);
		});

		// API: GET /api/v1/templates/{id}  (get template details with parameters)
		api.MapGet("/templates/{id}", async (string id, FlowTime.Sim.Core.Services.INodeBasedTemplateService templateService) =>
		{
			try
			{
				var template = await templateService.GetTemplateAsync(id);
				if (template == null)
				{
					return Results.NotFound(new { error = $"Template '{id}' not found" });
				}

				return Results.Ok(new
				{
					id = template.Metadata.Id,
					title = template.Metadata.Title ?? template.Metadata.Id,
					description = template.Metadata.Description ?? string.Empty,
					category = "general",
					tags = template.Metadata.Tags,
					parameters = template.Parameters?.Select(p => new
					{
						name = p.Name,
						type = p.Type,
						title = p.Title ?? string.Empty,
						description = p.Description ?? string.Empty,
						defaultValue = p.Default,
						min = p.Min,
						max = p.Max
					}) ?? Enumerable.Empty<object>()
				});
			}
			catch (ArgumentException ex)
			{
				return Results.NotFound(new { error = ex.Message });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to retrieve template: {ex.Message}");
			}
		});

		// API: GET /api/v1/templates/categories  (list available categories)
		api.MapGet("/templates/categories", () => 
		{
			// Node-based templates don't carry category; expose a single 'general' category
			var categories = new[] { "general" };
			return Results.Ok(new { categories });
		});

		// API: POST /api/v1/templates/{id}/generate  (generate model from template with parameter substitution)
		// SIM-M2.7: Enhanced to return provenance metadata
		api.MapPost("/templates/{id}/generate", async (string id, HttpRequest req, IConfiguration config, FlowTime.Sim.Core.Services.INodeBasedTemplateService templateService, FlowTime.Sim.Core.Services.IProvenanceService provenanceService) =>
		{
			try
			{
				// Parse request body for parameters
				using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
				var bodyText = await reader.ReadToEndAsync();
				var parameters = new Dictionary<string, object>();
				
				if (!string.IsNullOrWhiteSpace(bodyText))
				{
					var parametersJson = JsonDocument.Parse(bodyText).RootElement;
					foreach (var property in parametersJson.EnumerateObject())
					{
						parameters[property.Name] = property.Value.ValueKind switch
						{
							JsonValueKind.Number => property.Value.GetDouble(),
							JsonValueKind.String => property.Value.GetString() ?? "",
							JsonValueKind.True => true,
							JsonValueKind.False => false,
							JsonValueKind.Array => property.Value,
							_ => property.Value.ToString()
						};
					}
				}

				var modelYaml = await templateService.GenerateEngineModelAsync(id, parameters);

				// SIM-M2.7: Get template metadata for provenance
				var template = await templateService.GetTemplateAsync(id);
				if (template == null)
			{
				return Results.BadRequest(new { error = $"Template '{id}' not found" });
			}

			// SIM-M2.7: Create provenance metadata
			var provenance = provenanceService.CreateProvenance(
				template.Metadata.Id,
				"1.0", // Template schema version (templates don't currently have a version field)
				template.Metadata.Title,
				parameters);

			// SIM-M2.7: Check if provenance should be embedded in YAML
			var embedProvenance = req.Query.ContainsKey("embed_provenance") &&
								  req.Query["embed_provenance"].ToString().ToLowerInvariant() != "false";

			string finalModelYaml = modelYaml;
			if (embedProvenance)
			{
				finalModelYaml = FlowTime.Sim.Core.Services.ProvenanceEmbedder.EmbedProvenance(modelYaml, provenance);
			}				// Compute canonical model hash for integrity/cross-system compatibility
				var modelHash = ModelHasher.ComputeModelHash(finalModelYaml);

				// Use hash prefix for directory naming to prevent parameter collisions
				// Format: data/models/{templateId}/{hashPrefix8}/
				var hashPrefix = modelHash.Substring(7, 8); // Skip "sha256:" prefix, take first 8 hex chars
				var modelsRoot = ServiceHelpers.ModelsRoot(config);
				var templateModelsDir = Path.Combine(modelsRoot, id, hashPrefix);
				Directory.CreateDirectory(templateModelsDir);
				var modelPath = Path.Combine(templateModelsDir, "model.yaml");
				await File.WriteAllTextAsync(modelPath, finalModelYaml, System.Text.Encoding.UTF8);

				// SIM-M2.7: Persist provenance metadata alongside model
				var provenancePath = Path.Combine(templateModelsDir, "provenance.json");
				await File.WriteAllTextAsync(provenancePath, 
					JsonSerializer.Serialize(provenance, new JsonSerializerOptions 
					{ 
						WriteIndented = true,
						PropertyNamingPolicy = JsonNamingPolicy.CamelCase
					}), 
					System.Text.Encoding.UTF8);

				// Persist legacy metadata with hash, parameters, and timestamp
				var metadataPath = Path.Combine(templateModelsDir, "metadata.json");
				var metadata = new
				{
					templateId = id,
					parameters,
					modelHash,
					generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
				};
				await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), System.Text.Encoding.UTF8);

				// SIM-M2.7: Always return JSON with model + provenance
				// Backward compatible: clients can ignore provenance field
				return Results.Ok(new { model = finalModelYaml, provenance });
			}
			catch (ArgumentException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to generate model: {ex.Message}");
			}
		});

		// API: GET /api/v1/models  (list all generated models)
		api.MapGet("/models", (IConfiguration config) =>
		{
			try
			{
				var modelsRoot = ServiceHelpers.ModelsRoot(config);
				if (!Directory.Exists(modelsRoot))
				{
					return Results.Ok(new { models = Array.Empty<object>() });
				}

				var models = new List<object>();
				foreach (var templateDir in Directory.GetDirectories(modelsRoot))
				{
					var templateId = Path.GetFileName(templateDir);
					
					// Check for hash-based subdirectories
					foreach (var hashDir in Directory.GetDirectories(templateDir))
					{
						var modelPath = Path.Combine(hashDir, "model.yaml");
						var metadataPath = Path.Combine(hashDir, "metadata.json");
						
						if (File.Exists(modelPath))
						{
							var fileInfo = new FileInfo(modelPath);
							string? modelHash = null;
							
							// Read modelHash from metadata.json if it exists
							if (File.Exists(metadataPath))
							{
								try
								{
									var metadataJson = File.ReadAllText(metadataPath);
									var metadataDoc = JsonDocument.Parse(metadataJson);
									if (metadataDoc.RootElement.TryGetProperty("modelHash", out var hashElement))
									{
										modelHash = hashElement.GetString();
									}
								}
								catch { /* ignore metadata read errors */ }
							}
							
							models.Add(new
							{
								templateId,
								path = modelPath,
								size = fileInfo.Length,
								modifiedUtc = fileInfo.LastWriteTimeUtc,
								contentType = "application/x-yaml",
								modelHash
							});
						}
					}
				}

				return Results.Ok(new { models });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to list models: {ex.Message}");
			}
		});

		// API: GET /api/v1/models/{templateId}  (get specific model by template ID)
		api.MapGet("/models/{templateId}", (string templateId, IConfiguration config) =>
		{
			try
			{
				var modelsRoot = ServiceHelpers.ModelsRoot(config);
				var templateDir = Path.Combine(modelsRoot, templateId);
				
				if (!Directory.Exists(templateDir))
				{
					return Results.NotFound(new { error = $"No models found for template '{templateId}'" });
				}

				// Find the most recent model (newest hash directory)
				var hashDirs = Directory.GetDirectories(templateDir);
				if (hashDirs.Length == 0)
				{
					return Results.NotFound(new { error = $"No models found for template '{templateId}'" });
				}

				// Get the most recently modified model
				var latestHashDir = hashDirs
					.Select(d => new { Dir = d, Time = Directory.GetLastWriteTimeUtc(d) })
					.OrderByDescending(x => x.Time)
					.First().Dir;

				var modelPath = Path.Combine(latestHashDir, "model.yaml");
				var metadataPath = Path.Combine(latestHashDir, "metadata.json");
				
				if (!File.Exists(modelPath))
				{
					return Results.NotFound(new { error = $"Model file not found for template '{templateId}'" });
				}

				var modelYaml = File.ReadAllText(modelPath);
				var fileInfo = new FileInfo(modelPath);
				string? modelHash = null;
				
				// Read modelHash from metadata.json if it exists
				if (File.Exists(metadataPath))
				{
					try
					{
						var metadataJson = File.ReadAllText(metadataPath);
						var metadataDoc = JsonDocument.Parse(metadataJson);
						if (metadataDoc.RootElement.TryGetProperty("modelHash", out var hashElement))
						{
							modelHash = hashElement.GetString();
						}
					}
					catch { /* ignore metadata read errors */ }
				}

				return Results.Ok(new
				{
					templateId,
					model = modelYaml,
					path = modelPath,
					size = fileInfo.Length,
					modifiedUtc = fileInfo.LastWriteTimeUtc,
					modelHash
				});
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to retrieve model: {ex.Message}");
			}
		});

		// API: GET /api/v1/catalogs (list all catalogs)
		api.MapGet("/catalogs", (IConfiguration config) =>
		{
			try
			{
				var catalogsRoot = ServiceHelpers.CatalogsRoot(config);
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
						app.Logger.LogWarning("Failed to read catalog {FilePath}: {Message}", filePath, ex.Message);
					}
				}

				return Results.Ok(catalogs);
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to list catalogs: {ex.Message}");
			}
		});

		// API: GET /api/v1/catalogs/{id} (get specific catalog)
		api.MapGet("/catalogs/{id}", (string id, IConfiguration config) =>
		{
			try
			{
				if (!ServiceHelpers.IsSafeCatalogId(id))
					return Results.BadRequest(new { error = "Invalid catalog id" });

				var catalogsRoot = ServiceHelpers.CatalogsRoot(config);
				var filePath = Path.Combine(catalogsRoot, id + ".yaml");
				
				if (!File.Exists(filePath))
					return Results.NotFound(new { error = $"Catalog '{id}' not found" });

				var catalog = CatalogIO.ReadCatalogFromFile(filePath);
				return Results.Ok(catalog);
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { error = "Failed to read catalog", detail = ex.Message });
			}
		});

		// API: PUT /api/v1/catalogs/{id} (create/update catalog)
		api.MapPut("/catalogs/{id}", async (string id, HttpRequest req, IConfiguration config) =>
		{
			try
			{
				if (!ServiceHelpers.IsSafeCatalogId(id))
					return Results.BadRequest(new { error = "Invalid catalog id" });

				using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
				var yaml = await reader.ReadToEndAsync();
				if (string.IsNullOrWhiteSpace(yaml))
				{
					return Results.BadRequest(new { error = "Empty body" });
				}

				Catalog catalog;
				try
				{
					catalog = CatalogIO.ParseCatalogFromYaml(yaml);
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { error = "YAML parse failed", detail = ex.Message });
				}

				var validation = catalog.Validate();
				if (!validation.IsValid)
				{
					return Results.BadRequest(new { error = "Catalog validation failed", errors = validation.Errors });
				}

				var catalogsRoot = ServiceHelpers.CatalogsRoot(config);
				var filePath = Path.Combine(catalogsRoot, id + ".yaml");
				await File.WriteAllTextAsync(filePath, yaml, System.Text.Encoding.UTF8);

				var hash = CatalogIO.ComputeCatalogHash(catalog);
				
				return Results.Ok(new
				{
					id,
					hash,
					componentCount = catalog.Components.Count,
					connectionCount = catalog.Connections.Count
				});
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
		});

		// API: POST /api/v1/catalogs/validate (validate catalog without saving)
		api.MapPost("/catalogs/validate", async (HttpRequest req) =>
		{
			try
			{
				using var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8);
				var yaml = await reader.ReadToEndAsync();
				if (string.IsNullOrWhiteSpace(yaml))
				{
					return Results.BadRequest(new { valid = false, errors = new[] { "Empty body" } });
				}

				Catalog catalog;
				try
				{
					catalog = CatalogIO.ParseCatalogFromYaml(yaml);
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { valid = false, errors = new[] { $"YAML parse failed: {ex.Message}" } });
				}

				var validation = catalog.Validate();
				if (!validation.IsValid)
				{
					return Results.BadRequest(new { valid = false, errors = validation.Errors });
				}

				var hash = CatalogIO.ComputeCatalogHash(catalog);
				
				return Results.Ok(new
				{
					valid = true,
					hash,
					componentCount = catalog.Components.Count,
					connectionCount = catalog.Connections.Count
				});
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { valid = false, errors = new[] { ex.Message } });
			}
		});

		app.Lifetime.ApplicationStarted.Register(() =>
		{
			var urls = string.Join(", ", app.Urls);
			app.Logger.LogInformation("FlowTime.Sim.Service started. Urls={Urls}", urls);
		});

		// Validate templates at startup (logs warnings, doesn't fail startup)
		await app.ValidateTemplatesAsync();

		app.Run();
	}

	// Helper utilities
	public static class ServiceHelpers
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
		/// Gets the templates directory.
		/// Order of precedence:
		/// 1. Environment variable FLOWTIME_SIM_TEMPLATES_DIR
		/// 2. Configuration FlowTimeSim:TemplatesDir
		/// 3. Default: "./templates"
		/// </summary>
		public static string TemplatesRoot(IConfiguration? configuration = null)
		{
			// Check templates directory environment variable first
			var templatesDir = Environment.GetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR");
			if (!string.IsNullOrWhiteSpace(templatesDir))
			{
				Directory.CreateDirectory(templatesDir);
				return templatesDir;
			}

			// Check configuration if provided
			if (configuration != null)
			{
				// Check templates directory configuration
				var configTemplatesDir = configuration["FlowTimeSim:TemplatesDir"];
				if (!string.IsNullOrEmpty(configTemplatesDir))
				{
					Directory.CreateDirectory(configTemplatesDir);
					return configTemplatesDir;
				}
			}

			// Default to absolute path to workspace templates directory
			var baseDir = Directory.GetCurrentDirectory();
			var templateRoot = Path.Combine(baseDir, "..", "..", "templates");
			var defaultRoot = Path.GetFullPath(templateRoot);
			Directory.CreateDirectory(defaultRoot);
			return defaultRoot;
		}

		/// <summary>
		/// Gets the models directory (for generated models from templates).
		/// Returns: {DataRoot}/models
		/// </summary>
		public static string ModelsRoot(IConfiguration? configuration = null)
		{
			var dataRoot = DataRoot(configuration);
			var modelsDir = Path.Combine(dataRoot, "models");
			Directory.CreateDirectory(modelsDir);
			return modelsDir;
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

	// === OBSOLETE DTOs REMOVED ===
	// Overlay feature removed - no longer needed for template-based model generation
}
