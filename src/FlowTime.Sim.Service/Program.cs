using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using FlowTime.Sim.Core;
using FlowTime.Sim.Core.Analysis;
using FlowTime.Core.Analysis;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using FlowTime.Sim.Service; // TemplateRegistry
using FlowTime.Sim.Service.Services; // ServiceInfoProvider
using FlowTime.Sim.Service.Extensions; // TemplateValidationExtensions
using FlowTime.Contracts.TimeTravel;
using FlowTime.Contracts.Storage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FlowTime.TimeMachine;
using FlowTime.TimeMachine.Orchestration;

// Explicit Program class for integration tests & clear structure
public partial class Program
{
	private static readonly IDeserializer artifactDeserializer = new DeserializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

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
		builder.Services.AddSingleton<ITemplateWarningRegistry, TemplateWarningRegistry>();
		builder.Services.AddSingleton<ITemplateService>(provider =>
		{
			var logger = provider.GetRequiredService<ILogger<TemplateService>>();
			var templatesDirectory = ServiceHelpers.TemplatesRoot(builder.Configuration);
			return new TemplateService(templatesDirectory, logger);
		});
		builder.Services.AddSingleton<IStorageBackend>(provider =>
		{
			var config = provider.GetRequiredService<IConfiguration>();
			var options = StorageBackendOptions.FromConfiguration(config);
			return StorageBackendFactory.Create(options);
		});
		builder.Services.AddSingleton<TelemetryBundleBuilder>();
		builder.Services.AddSingleton<RunOrchestrationService>();

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

		app.Lifetime.ApplicationStarted.Register(() =>
		{
			_ = Task.Run(async () =>
			{
				using var scope = app.Services.CreateScope();
				var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();
				var warningRegistry = scope.ServiceProvider.GetRequiredService<ITemplateWarningRegistry>();

				var templates = await templateService.GetAllTemplatesAsync();
				foreach (var template in templates)
				{
					try
					{
						var yaml = await templateService.GenerateEngineModelAsync(template.Metadata.Id, new Dictionary<string, object>(), null);
						var analysis = TemplateInvariantAnalyzer.Analyze(yaml);
						warningRegistry.UpdateWarnings(template.Metadata.Id, analysis.Warnings);

						if (analysis.Warnings.Count > 0)
						{
							app.Logger.LogWarning("Template {TemplateId} produced {Count} invariant warning(s) at startup", template.Metadata.Id, analysis.Warnings.Count);
						}
					}
					catch (Exception ex)
					{
						app.Logger.LogWarning(ex, "Invariant analysis failed for template {TemplateId}", template.Metadata.Id);
					}
				}
			});
		});

		// Health endpoints - simple and factual
		app.MapGet("/healthz", (HttpContext context, IConfiguration config, IEndpointDiscoveryService endpointService, ITemplateWarningRegistry warningRegistry) =>
		{
			// Check for detailed health parameter
			var includeDetails = context.Request.Query.ContainsKey("detailed") ||
								context.Request.Query.ContainsKey("include-details");

			var hasWarnings = warningRegistry.HasWarnings;
			var status = hasWarnings ? "warning" : "ok";

			if (includeDetails)
			{
				// Enhanced but simple health response with only factual information
				var process = System.Diagnostics.Process.GetCurrentProcess();
				var assembly = System.Reflection.Assembly.GetExecutingAssembly();
				var serviceName = assembly.GetName().Name ?? "FlowTime.Sim.Service";
				var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

				return Results.Ok(new
				{
					status,
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
					availableEndpoints = endpointService.GetAvailableEndpoints(),
					templateWarnings = warningRegistry.GetWarnings()
				});
			}
			else
			{
				// Legacy basic response
				return Results.Ok(new { status });
			}
		});

		// Enhanced health endpoint with service information (v1)
		app.MapGet("/v1/healthz", (IServiceInfoProvider serviceInfoProvider, HttpContext context, IConfiguration config, ITemplateWarningRegistry warningRegistry) =>
		{
			// Check for detailed health parameter
			var includeDetails = context.Request.Query.ContainsKey("detailed") ||
								context.Request.Query.ContainsKey("include-details");
			var hasWarnings = warningRegistry.HasWarnings;
			var status = hasWarnings ? "warning" : "ok";

			if (includeDetails)
			{
				// Enhanced but simple health response with only factual information
				var process = System.Diagnostics.Process.GetCurrentProcess();
				return Results.Ok(new
				{
					status,
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
				"/api/v1/templates/refresh",
				"/api/v1/templates/categories",
				"/api/v1/models",
				"/api/v1/models/{templateId}"
					},
					templateWarnings = warningRegistry.GetWarnings()
				});
			}
			else
			{
				// Standard v1 health with service info
				var serviceInfo = serviceInfoProvider.GetServiceInfo();
				if (warningRegistry.HasWarnings)
				{
					serviceInfo.Health.Status = "warning";
					serviceInfo.Health.Details["templateWarnings"] = warningRegistry.GetWarnings();
				}
				return Results.Ok(serviceInfo);
			}
		});

		// Modern RESTful endpoints under /api/v1
		var api = app.MapGroup("/api/v1");
		var orchestration = api.MapGroup("/orchestration");
		orchestration.MapRunOrchestrationEndpoints();

		// API: GET /api/v1/templates  (list all templates)
		api.MapGet("/templates", async (ITemplateService templateService, string? category) =>
		{
			var items = await templateService.GetAllTemplatesAsync();

			var templates = items.Select(t => new
			{
				id = t.Metadata.Id,
				title = t.Metadata.Title ?? t.Metadata.Id,
				description = t.Metadata.Description ?? string.Empty,
				narrative = t.Metadata.Narrative,
				category = "general", // node-based templates don't carry category yet
				tags = t.Metadata.Tags,
				version = t.Metadata.Version,
				captureKey = t.Metadata.CaptureKey
			});

			if (!string.IsNullOrEmpty(category))
			{
				templates = templates.Where(t => string.Equals(t.category, category, StringComparison.OrdinalIgnoreCase));
			}
			return Results.Ok(templates);
		});

		// API: GET /api/v1/templates/{id}  (get template details with parameters)
		api.MapGet("/templates/{id}", async (string id, ITemplateService templateService) =>
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
					narrative = template.Metadata.Narrative,
					category = "general",
					tags = template.Metadata.Tags,
					version = template.Metadata.Version,
					captureKey = template.Metadata.CaptureKey,
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

		// API: GET /api/v1/templates/{id}/source  (get template YAML source)
		api.MapGet("/templates/{id}/source", async (string id, ITemplateService templateService) =>
		{
			try
			{
				var source = await templateService.GetTemplateSourceAsync(id);
				if (string.IsNullOrWhiteSpace(source))
				{
					return Results.NotFound(new { error = $"Template '{id}' not found" });
				}

				return Results.Ok(new { id, source });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to retrieve template source: {ex.Message}");
			}
		});

		// API: GET /api/v1/templates/categories  (list available categories)
		api.MapGet("/templates/categories", () =>
		{
			// Node-based templates don't carry category; expose a single 'general' category
			var categories = new[] { "general" };
			return Results.Ok(new { categories });
		});

		// API: POST /api/v1/templates/refresh (clear template cache)
		api.MapPost("/templates/refresh", async (ITemplateService templateService, ILogger<Program> logger) =>
		{
			var count = await templateService.RefreshAsync().ConfigureAwait(false);
			logger.LogInformation("Template cache refreshed via FlowTime-Sim API. {Count} template(s) reloaded.", count);
			return Results.Ok(new { status = "refreshed", templates = count });
		});

		// API: POST /api/v1/templates/{id}/generate  (generate model from template with parameter substitution)
		// SIM-M2.7: Enhanced to return provenance metadata
		api.MapPost("/templates/{id}/generate", async (
			string id,
			HttpRequest req,
			IConfiguration config,
			ITemplateService templateService,
			ITemplateWarningRegistry warningRegistry,
			IStorageBackend storage,
			CancellationToken cancellationToken) =>
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

				TemplateMode? modeOverride = null;
				if (req.Query.TryGetValue("mode", out var modeValues))
				{
					var modeText = modeValues.ToString();
					if (!string.IsNullOrWhiteSpace(modeText))
					{
						try
						{
							modeOverride = TemplateModeExtensions.Parse(modeText);
						}
						catch (TemplateValidationException)
						{
							return Results.BadRequest(new { error = $"Invalid mode '{modeText}'. Expected 'simulation' or 'telemetry'." });
						}
					}
				}

				var modelYaml = await templateService.GenerateEngineModelAsync(id, parameters, modeOverride);
				var invariantAnalysis = TemplateInvariantAnalyzer.Analyze(modelYaml);
				return await BuildGenerateResponseAsync(id, modelYaml, invariantAnalysis.Warnings, config, warningRegistry, storage, cancellationToken).ConfigureAwait(false);
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

		// API: POST /api/v1/drafts/generate  (generate model from draft template source)
		api.MapPost("/drafts/generate", async (
			DraftTemplateRequest request,
			IConfiguration config,
			ILogger<TemplateService> templateLogger,
			ITemplateWarningRegistry warningRegistry,
			IStorageBackend storage,
			CancellationToken cancellationToken) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			var resolveResult = await ResolveDraftSourceAsync(request.Source, config, storage, cancellationToken).ConfigureAwait(false);
			if (resolveResult.ErrorResult is not null)
			{
				return resolveResult.ErrorResult;
			}

			if (resolveResult.Value is null)
			{
				return Results.BadRequest(new { error = "Draft source resolution failed." });
			}

			var (draftId, draftYaml) = resolveResult.Value;
			var parameters = BuildParameters(request.Parameters);
			TemplateMode? modeOverride = null;
			if (!string.IsNullOrWhiteSpace(request.Mode))
			{
				try
				{
					modeOverride = TemplateModeExtensions.Parse(request.Mode);
				}
				catch (TemplateValidationException)
				{
					return Results.BadRequest(new { error = $"Invalid mode '{request.Mode}'. Expected 'simulation' or 'telemetry'." });
				}
			}

			try
			{
				var draftTemplateService = CreateDraftTemplateService(draftId, draftYaml, templateLogger);
				var modelYaml = await draftTemplateService.GenerateEngineModelAsync(draftId, parameters, modeOverride).ConfigureAwait(false);
				var invariantAnalysis = TemplateInvariantAnalyzer.Analyze(modelYaml);
				return await BuildGenerateResponseAsync(draftId, modelYaml, invariantAnalysis.Warnings, config, warningRegistry, storage, cancellationToken).ConfigureAwait(false);
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

		// API: POST /api/v1/drafts/run  (orchestrate run from draft template source)
		api.MapPost("/drafts/run", async (
			DraftRunRequest request,
			RunOrchestrationService orchestration,
			TelemetryBundleBuilder bundleBuilder,
			IConfiguration config,
			ILogger<RunOrchestrationService> logger,
			ILogger<TemplateService> templateLogger,
			IStorageBackend storage,
			CancellationToken cancellationToken) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			var resolveResult = await ResolveDraftSourceAsync(request.Source, config, storage, cancellationToken).ConfigureAwait(false);
			if (resolveResult.ErrorResult is not null)
			{
				return resolveResult.ErrorResult;
			}

			if (resolveResult.Value is null)
			{
				return Results.BadRequest(new { error = "Draft source resolution failed." });
			}

			var (draftId, draftYaml) = resolveResult.Value;
			var mode = string.IsNullOrWhiteSpace(request.Mode) ? "telemetry" : request.Mode.Trim().ToLowerInvariant();
			if (mode is not ("telemetry" or "simulation"))
			{
				return Results.BadRequest(new { error = "mode must be 'telemetry' or 'simulation'." });
			}

			if (mode == "telemetry" && (request.Telemetry?.CaptureDirectory is null || string.IsNullOrWhiteSpace(request.Telemetry.CaptureDirectory)))
			{
				return Results.BadRequest(new { error = "telemetry.captureDirectory is required for telemetry runs." });
			}

			var runsRoot = Path.Combine(ServiceHelpers.DataRoot(config), "runs");
			Directory.CreateDirectory(runsRoot);
			var parameters = RunOrchestrationContractMapper.ConvertParameters(request.Parameters);
			var telemetryBindings = RunOrchestrationContractMapper.CloneTelemetryBindings(request.Telemetry);

			var orchestrationRequest = new RunOrchestrationRequest
			{
				TemplateId = draftId,
				Mode = mode,
				CaptureDirectory = request.Telemetry?.CaptureDirectory,
				TelemetryBindings = telemetryBindings,
				Parameters = parameters,
				OutputRoot = runsRoot,
				DeterministicRunId = request.Options?.DeterministicRunId ?? false,
				RunId = request.Options?.RunId,
				DryRun = request.Options?.DryRun ?? false,
				OverwriteExisting = request.Options?.OverwriteExisting ?? false,
				Rng = request.Rng
			};

			try
			{
				var draftTemplateService = CreateDraftTemplateService(draftId, draftYaml, templateLogger);
				var draftOrchestration = new RunOrchestrationService(draftTemplateService, bundleBuilder, logger, config);
				var outcome = await draftOrchestration.CreateRunAsync(orchestrationRequest, cancellationToken).ConfigureAwait(false);
				if (outcome.IsDryRun)
				{
					var plan = outcome.Plan ?? throw new InvalidOperationException("Dry-run outcome missing plan details.");
					return Results.Ok(new RunCreateResponse
					{
						IsDryRun = true,
						Plan = RunOrchestrationContractMapper.BuildPlan(plan),
						Warnings = Array.Empty<StateWarning>(),
						CanReplay = false,
						Telemetry = null,
						WasReused = false
					});
				}

				var result = outcome.Result ?? throw new InvalidOperationException("Run outcome missing result payload.");
				var metadata = RunOrchestrationContractMapper.BuildStateMetadata(result);
				var warnings = RunOrchestrationContractMapper.BuildStateWarnings(result.TelemetryManifest);
				var canReplay = RunOrchestrationContractMapper.DetermineCanReplay(result);

				logger.LogInformation("Run {RunId} created for draft {DraftId} (mode={Mode}, reused={Reused})", metadata.RunId, draftId, metadata.Mode, result.WasReused);

				return Results.Created($"/api/v1/drafts/run/{metadata.RunId}", new RunCreateResponse
				{
					IsDryRun = false,
					Metadata = metadata,
					Warnings = warnings,
					CanReplay = canReplay,
					Telemetry = RunOrchestrationContractMapper.BuildTelemetrySummary(result),
					WasReused = result.WasReused
				});
			}
			catch (TemplateValidationException ex)
			{
				logger.LogWarning(ex, "Template validation failed for draft {DraftId}", draftId);
				return Results.BadRequest(new { error = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				logger.LogWarning(ex, "Run validation failed for draft {DraftId}", draftId);
				return Results.BadRequest(new { error = ex.Message });
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to create run for draft {DraftId}", draftId);
				return Results.Problem(title: "Run creation failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
			}
		});

		// API: POST /api/v1/series/ingest  (ingest pre-aggregated series data)
		api.MapPost("/series/ingest", (SeriesIngestRequest request, IConfiguration config) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			if (string.IsNullOrWhiteSpace(request.Content))
			{
				return Results.BadRequest(new { error = "content is required." });
			}

			var format = string.IsNullOrWhiteSpace(request.Format) ? "csv" : request.Format.Trim().ToLowerInvariant();
			if (format is not ("csv" or "table"))
			{
				return Results.BadRequest(new { error = "format must be 'csv' or 'table'." });
			}

			var seriesId = string.IsNullOrWhiteSpace(request.SeriesId)
				? $"series-{Guid.NewGuid():N}"
				: request.SeriesId.Trim();

			if (!ServiceHelpers.IsSafeSeriesId(seriesId))
			{
				return Results.BadRequest(new { error = "seriesId contains invalid characters." });
			}

			string detailLevel;
			try
			{
				detailLevel = ResolveDetailLevel(request.DetailLevel);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
			SeriesParseResult parseResult;
			try
			{
				parseResult = SeriesParser.Parse(request.Content);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}

			var seriesRoot = ServiceHelpers.SeriesRoot(config);
			var storage = new SeriesStorage(seriesRoot);
			var document = new SeriesDocument
			{
				SeriesId = seriesId,
				Bins = parseResult.Bins,
				Values = parseResult.Values,
				Metadata = request.Metadata,
				SourceFormat = format
			};

			storage.Save(document);

			var response = new Dictionary<string, object?>
			{
				["seriesId"] = seriesId,
				["count"] = parseResult.Values.Length,
				["metadata"] = request.Metadata
			};

			if (detailLevel == "expert")
			{
				response["diagnostics"] = new
				{
					parseResult.Diagnostics.Delimiter,
					parseResult.Diagnostics.HasHeader,
					parseResult.Diagnostics.Columns,
					parseResult.Diagnostics.RowCount,
					seriesPath = Path.Combine(seriesRoot, seriesId)
				};
			}

			return Results.Ok(response);
		});

		// API: POST /api/v1/series/summarize  (summarize stored series)
		api.MapPost("/series/summarize", (SeriesSummaryRequest request, IConfiguration config) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			if (string.IsNullOrWhiteSpace(request.SeriesId))
			{
				return Results.BadRequest(new { error = "seriesId is required." });
			}

			if (!ServiceHelpers.IsSafeSeriesId(request.SeriesId))
			{
				return Results.BadRequest(new { error = "seriesId contains invalid characters." });
			}

			string detailLevel;
			try
			{
				detailLevel = ResolveDetailLevel(request.DetailLevel);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
			var storage = new SeriesStorage(ServiceHelpers.SeriesRoot(config));

			SeriesDocument document;
			try
			{
				document = storage.Load(request.SeriesId);
			}
			catch (FileNotFoundException)
			{
				return Results.NotFound(new { error = $"Series '{request.SeriesId}' not found." });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to read series: {ex.Message}");
			}

			var summary = BuildSeriesSummary(document);
			var response = new Dictionary<string, object?>
			{
				["seriesId"] = document.SeriesId,
				["count"] = document.Values.Length,
				["min"] = summary.Min,
				["max"] = summary.Max,
				["avg"] = summary.Avg,
				["peak"] = new { summary.PeakBin, summary.PeakValue },
				["percentiles"] = summary.Percentiles,
				["periodicity"] = summary.Periodicity
			};

			if (detailLevel == "expert")
			{
				response["diagnostics"] = summary.Diagnostics;
			}

			return Results.Ok(response);
		});

		// API: POST /api/v1/profiles/fit  (fit profile or PMF from samples/summary)
		api.MapPost("/profiles/fit", (ProfileFitRequest request, IConfiguration config) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			string detailLevel;
			try
			{
				detailLevel = ResolveDetailLevel(request.DetailLevel);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}

			var mode = ResolveProfileMode(request.Mode, request.SeriesId, request.Samples, request.Summary);
			if (mode is null)
			{
				return Results.BadRequest(new { error = "mode must be 'profile' or 'pmf' (or provide a clear input source)." });
			}

			var storage = new SeriesStorage(ServiceHelpers.SeriesRoot(config));
			if (mode == "profile")
			{
				TemplateProfile profile;
				string method;
				object summary;
				object diagnostics;
				try
				{
					(profile, method, summary, diagnostics) = BuildProfileFit(request, storage);
				}
				catch (FileNotFoundException)
				{
					return Results.NotFound(new { error = $"Series '{request.SeriesId}' not found." });
				}
				catch (InvalidOperationException ex)
				{
					return Results.BadRequest(new { error = ex.Message });
				}
				var response = new Dictionary<string, object?>
				{
					["kind"] = "profile",
					["method"] = method,
					["profile"] = profile,
					["summary"] = summary
				};

				if (detailLevel == "expert")
				{
					response["diagnostics"] = diagnostics;
				}

				return Results.Ok(response);
			}

			PmfSpec pmf;
			string methodText;
			object pmfSummary;
			object pmfDiagnostics;
			try
			{
				(pmf, methodText, pmfSummary, pmfDiagnostics) = BuildPmfFit(request, storage);
			}
			catch (FileNotFoundException)
			{
				return Results.NotFound(new { error = $"Series '{request.SeriesId}' not found." });
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
			var pmfResponse = new Dictionary<string, object?>
			{
				["kind"] = "pmf",
				["method"] = methodText,
				["pmf"] = pmf,
				["summary"] = pmfSummary
			};

			if (detailLevel == "expert")
			{
				pmfResponse["diagnostics"] = pmfDiagnostics;
			}

			return Results.Ok(pmfResponse);
		});

		// API: POST /api/v1/profiles/preview  (preview profile/PMF)
		api.MapPost("/profiles/preview", (ProfilePreviewRequest request) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			string detailLevel;
			try
			{
				detailLevel = ResolveDetailLevel(request.DetailLevel);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}

			if (request.Profile is null && request.Pmf is null)
			{
				return Results.BadRequest(new { error = "profile or pmf must be provided." });
			}

			var preview = BuildProfilePreview(request);
			var response = new Dictionary<string, object?>
			{
				["summary"] = preview.Summary
			};

			if (detailLevel == "expert")
			{
				response["diagnostics"] = preview.Diagnostics;
			}

			return Results.Ok(response);
		});

		// API: POST /api/v1/drafts/map-profile  (apply profile/PMF to a draft node)
		api.MapPost("/drafts/map-profile", async (
			DraftProfileMapRequest request,
			IConfiguration config,
			ILogger<TemplateService> templateLogger,
			IStorageBackend storage,
			CancellationToken cancellationToken) =>
		{
			if (request is null)
			{
				return Results.BadRequest(new { error = "Request body is required." });
			}

			if (string.IsNullOrWhiteSpace(request.NodeId))
			{
				return Results.BadRequest(new { error = "nodeId is required." });
			}

			string detailLevel;
			try
			{
				detailLevel = ResolveDetailLevel(request.DetailLevel);
			}
			catch (InvalidOperationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}

			if (request.Profile is null && request.Pmf is null)
			{
				return Results.BadRequest(new { error = "profile or pmf must be provided." });
			}

			var resolveResult = await ResolveDraftSourceAsync(request.Source, config, storage, cancellationToken).ConfigureAwait(false);
			if (resolveResult.ErrorResult is not null)
			{
				return resolveResult.ErrorResult;
			}

			if (resolveResult.Value is null)
			{
				return Results.BadRequest(new { error = "Draft source resolution failed." });
			}

			var (draftId, draftYaml) = resolveResult.Value;
			Template template;
			try
			{
				template = TemplateParser.ParseFromYaml(draftYaml);
			}
			catch (TemplateParsingException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
			catch (TemplateValidationException ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}

			var node = template.Nodes.FirstOrDefault(n => string.Equals(n.Id, request.NodeId, StringComparison.OrdinalIgnoreCase));
			if (node is null)
			{
				return Results.NotFound(new { error = $"Node '{request.NodeId}' not found." });
			}

			if (request.Pmf is not null)
			{
				node.Kind = "pmf";
				node.Pmf = request.Pmf;
				node.Values = null;
				node.Expr = null;
				node.Dependencies = null;
			}

			if (request.Profile is not null)
			{
				if (request.Pmf is null && node.Pmf is null)
				{
					return Results.BadRequest(new { error = "profile requires a pmf to be present on the target node." });
				}

				node.Profile = request.Profile;
			}

			if (request.Provenance is not null && request.Provenance.Count > 0)
			{
				node.Metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (var kvp in request.Provenance)
				{
					node.Metadata[kvp.Key] = kvp.Value;
				}
			}

			var updatedYaml = TemplateYamlSerializer.Serialize(template);

			var response = new Dictionary<string, object?>
			{
				["draftId"] = draftId,
				["nodeId"] = node.Id,
				["content"] = updatedYaml
			};

			if (detailLevel == "expert")
			{
				response["diagnostics"] = new
				{
					nodeKind = node.Kind,
					metadata = node.Metadata,
					templateId = template.Metadata?.Id
				};
			}

			return Results.Ok(response);
		});

		// API: GET /api/v1/models  (list all generated models)
		api.MapGet("/models", async (IStorageBackend storage, CancellationToken cancellationToken) =>
		{
			try
			{
				var items = await storage.ListAsync(new StorageListRequest { Kind = StorageKind.Model }, cancellationToken).ConfigureAwait(false);
				var models = items.Select(item =>
				{
					var metadata = item.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					metadata.TryGetValue("templateId", out var templateId);
					metadata.TryGetValue("schemaVersion", out var schemaVersion);
					metadata.TryGetValue("mode", out var mode);
					metadata.TryGetValue("modelHash", out var modelHash);

					return new
					{
						templateId,
						schema = string.IsNullOrWhiteSpace(schemaVersion) ? null : $"schema-{schemaVersion}",
						mode = string.IsNullOrWhiteSpace(mode) ? null : $"mode-{mode}",
						storageRef = item.Reference.ToString(),
						size = item.SizeBytes,
						modifiedUtc = item.UpdatedUtc,
						contentType = "application/zip",
						modelHash
					};
				}).ToList();

				return Results.Ok(new { models });
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to list models: {ex.Message}");
			}
		});

		// API: GET /api/v1/models/{templateId}  (get specific model by template ID)
		api.MapGet("/models/{templateId}", async (string templateId, IStorageBackend storage, CancellationToken cancellationToken) =>
		{
			try
			{
				var items = await storage.ListAsync(new StorageListRequest { Kind = StorageKind.Model }, cancellationToken).ConfigureAwait(false);
				var matches = items
					.Where(item =>
					{
						if (item.Metadata is null)
						{
							return false;
						}

						return item.Metadata.TryGetValue("templateId", out var value)
							&& string.Equals(value, templateId, StringComparison.OrdinalIgnoreCase);
					})
					.OrderByDescending(item => item.UpdatedUtc)
					.ToList();

				if (matches.Count == 0)
				{
					return Results.NotFound(new { error = $"No models found for template '{templateId}'" });
				}

				var selected = matches[0];
				var stored = await storage.ReadAsync(selected.Reference, cancellationToken).ConfigureAwait(false);
				if (stored is null)
				{
					return Results.NotFound(new { error = $"Model data missing for template '{templateId}'" });
				}

				var modelYaml = ReadArchiveEntry(stored.Content, "model.yaml");
				if (string.IsNullOrWhiteSpace(modelYaml))
				{
					return Results.Problem($"Model archive for template '{templateId}' was missing model.yaml.");
				}

				var metadata = selected.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				metadata.TryGetValue("schemaVersion", out var schemaVersion);
				metadata.TryGetValue("mode", out var mode);
				metadata.TryGetValue("modelHash", out var modelHash);

				return Results.Ok(new
				{
					templateId,
					schema = string.IsNullOrWhiteSpace(schemaVersion) ? null : $"schema-{schemaVersion}",
					mode = string.IsNullOrWhiteSpace(mode) ? null : $"mode-{mode}",
					model = modelYaml,
					storageRef = selected.Reference.ToString(),
					size = selected.SizeBytes,
					modifiedUtc = selected.UpdatedUtc,
					modelHash
				});
			}
			catch (Exception ex)
			{
				return Results.Problem($"Failed to retrieve model: {ex.Message}");
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

	private static TemplateService CreateDraftTemplateService(string draftId, string yaml, ILogger<TemplateService> logger)
	{
		var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[draftId] = yaml
		};
		return new TemplateService(templates, logger);
	}

	private static async Task<IResult> BuildGenerateResponseAsync(
		string templateId,
		string modelYaml,
		IReadOnlyList<InvariantWarning> warnings,
		IConfiguration config,
		ITemplateWarningRegistry warningRegistry,
		IStorageBackend storage,
		CancellationToken cancellationToken)
	{
		warningRegistry.UpdateWarnings(templateId, warnings);

		var modelHash = ModelHasher.ComputeModelHash(modelYaml);
		var artifact = artifactDeserializer.Deserialize<SimModelArtifact>(modelYaml);
		var hasWindow = !string.IsNullOrWhiteSpace(artifact.Window?.Start);
		var hasTopology = artifact.Topology?.Nodes?.Count > 0;
		var telemetryMetadata = FlowTime.Core.TimeTravel.TelemetrySourceMetadataExtractor.Extract(modelYaml);
		var hasTelemetrySources = telemetryMetadata.TelemetrySources.Count > 0;

		const string hashPrefixLabel = "sha256:";
		var modelHashValue = modelHash.StartsWith(hashPrefixLabel, StringComparison.OrdinalIgnoreCase)
			? modelHash[hashPrefixLabel.Length..]
			: modelHash;
		var modelId = $"model_{modelHashValue}";

		var metadata = new
		{
			templateId = artifact.Metadata.Id,
			templateTitle = artifact.Metadata.Title,
			templateVersion = artifact.Metadata.Version,
			schemaVersion = artifact.SchemaVersion,
			mode = artifact.Mode,
			hasWindow,
			hasTopology,
			hasTelemetrySources,
			telemetrySources = telemetryMetadata.TelemetrySources,
			nodeSources = telemetryMetadata.NodeSources,
			modelHash = modelHashValue,
			parameters = artifact.Provenance.Parameters,
			generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
		};

		var provenanceJson = JsonSerializer.Serialize(artifact.Provenance, new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});
		var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
		var archiveBytes = BuildModelArchive(modelYaml, provenanceJson, metadataJson);

		var modelWrite = await storage.WriteAsync(new StorageWriteRequest
		{
			Kind = StorageKind.Model,
			Id = modelId,
			Content = archiveBytes,
			ContentType = "application/zip",
			Metadata = new Dictionary<string, string>
			{
				["templateId"] = artifact.Metadata.Id,
				["templateTitle"] = artifact.Metadata.Title,
				["templateVersion"] = artifact.Metadata.Version,
				["schemaVersion"] = artifact.SchemaVersion.ToString(CultureInfo.InvariantCulture),
				["mode"] = artifact.Mode,
				["modelHash"] = modelHashValue,
				["generatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
			}
		}, cancellationToken).ConfigureAwait(false);

		var metadataSummary = new
		{
			templateId = artifact.Metadata.Id,
			templateTitle = artifact.Metadata.Title,
			templateVersion = artifact.Metadata.Version,
			schemaVersion = artifact.SchemaVersion,
			generator = artifact.Generator,
			mode = artifact.Mode,
			hasWindow,
			hasTopology,
			hasTelemetrySources,
			modelHash
		};

		var warningsPayload = warnings.Select(w => new
		{
			nodeId = w.NodeId,
			code = w.Code,
			message = w.Message,
			bins = w.Bins
		}).ToArray();

		return Results.Ok(new { model = modelYaml, provenance = artifact.Provenance, metadata = metadataSummary, warnings = warningsPayload, modelRef = modelWrite.Reference });
	}

	private static Dictionary<string, object> BuildParameters(Dictionary<string, JsonElement>? parameters)
	{
		var converted = RunOrchestrationContractMapper.ConvertParameters(parameters);
		var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in converted)
		{
			if (kvp.Value is not null)
			{
				result[kvp.Key] = kvp.Value;
			}
		}
		return result;
	}

	private static byte[] BuildModelArchive(string modelYaml, string provenanceJson, string metadataJson)
	{
		using var stream = new MemoryStream();
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			WriteArchiveTextEntry(archive, "model.yaml", modelYaml);
			WriteArchiveTextEntry(archive, "provenance.json", provenanceJson);
			WriteArchiveTextEntry(archive, "metadata.json", metadataJson);
		}

		return stream.ToArray();
	}

	private static void WriteArchiveTextEntry(ZipArchive archive, string name, string content)
	{
		var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
		using var entryStream = entry.Open();
		using var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8);
		writer.Write(content);
	}

	private static string? ReadArchiveEntry(byte[] archiveBytes, string name)
	{
		using var stream = new MemoryStream(archiveBytes);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
		var entry = archive.GetEntry(name);
		if (entry is null)
		{
			return null;
		}

		using var entryStream = entry.Open();
		using var reader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
		return reader.ReadToEnd();
	}

	private static Task<(DraftSourceResolution? Value, IResult? ErrorResult)> ResolveDraftSourceAsync(
		DraftSource source,
		IConfiguration config,
		IStorageBackend storage,
		CancellationToken cancellationToken)
	{
		if (source is null)
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = "source is required." })));
		}

		var type = source.Type?.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(type))
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = "source.type is required." })));
		}

		if (type != "inline")
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = $"Unsupported source.type '{source.Type}'. Only 'inline' is supported." })));
		}

		if (string.IsNullOrWhiteSpace(source.Id))
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = "source.id is required for inline drafts." })));
		}
		if (string.IsNullOrWhiteSpace(source.Content))
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = "source.content is required for inline drafts." })));
		}

		if (!ServiceHelpers.IsSafeId(source.Id))
		{
			return Task.FromResult<(DraftSourceResolution?, IResult?)>((null, Results.BadRequest(new { error = "Invalid draft id." })));
		}

		return Task.FromResult<(DraftSourceResolution?, IResult?)>((new DraftSourceResolution(source.Id, source.Content), null));
	}

	private static string ResolveDetailLevel(string? detailLevel)
	{
		if (string.IsNullOrWhiteSpace(detailLevel))
		{
			return "basic";
		}

		var normalized = detailLevel.Trim().ToLowerInvariant();
		if (normalized is "basic" or "expert")
		{
			return normalized;
		}

		throw new InvalidOperationException("detailLevel must be 'basic' or 'expert'.");
	}

	private static SeriesSummary BuildSeriesSummary(SeriesDocument document)
	{
		if (document.Values.Length == 0)
		{
			throw new InvalidOperationException("Series contains no values.");
		}

		var values = document.Values;
		var sorted = values.OrderBy(value => value).ToArray();
		var min = sorted[0];
		var max = sorted[^1];
		var avg = values.Average();
		var sum = values.Sum();
		var peakIndex = Array.IndexOf(values, max);
		var percentiles = new Dictionary<string, double>
		{
			["p50"] = Percentile(sorted, 0.50),
			["p90"] = Percentile(sorted, 0.90),
			["p95"] = Percentile(sorted, 0.95),
			["p99"] = Percentile(sorted, 0.99)
		};

		var periodicity = DetectPeriodicity(document, out var periodicityScore);
		var diagnostics = new SeriesSummaryDiagnostics
		{
			Sum = sum,
			Median = percentiles["p50"],
			StdDev = ComputeStdDev(values, avg),
			PeriodicityScore = periodicityScore
		};

		return new SeriesSummary(
			min,
			max,
			avg,
			percentiles,
			document.Bins.ElementAtOrDefault(peakIndex),
			max,
			periodicity,
			diagnostics);
	}

	private static double Percentile(double[] sorted, double percentile)
	{
		if (sorted.Length == 1)
		{
			return sorted[0];
		}

		var position = (sorted.Length - 1) * percentile;
		var lower = (int)Math.Floor(position);
		var upper = (int)Math.Ceiling(position);
		if (lower == upper)
		{
			return sorted[lower];
		}

		var fraction = position - lower;
		return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
	}

	private static double ComputeStdDev(double[] values, double mean)
	{
		if (values.Length <= 1)
		{
			return 0d;
		}

		var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
		return Math.Sqrt(variance);
	}

	private static PeriodicityInfo DetectPeriodicity(SeriesDocument document, out double? score)
	{
		score = null;
		if (document.Metadata?.BinSize is null || string.IsNullOrWhiteSpace(document.Metadata.BinUnit))
		{
			return new PeriodicityInfo(false, null, null);
		}

		var binsPerDay = TryGetBinsPerDay(document.Metadata.BinSize.Value, document.Metadata.BinUnit);
		if (binsPerDay is null)
		{
			return new PeriodicityInfo(false, null, null);
		}

		var candidates = new List<(string Label, int Period)>
		{
			("daily", binsPerDay.Value),
			("weekly", binsPerDay.Value * 7)
		};

		var bestScore = 0d;
		string? bestLabel = null;
		int? bestPeriod = null;

		foreach (var candidate in candidates)
		{
			if (candidate.Period <= 0 || document.Values.Length < candidate.Period * 2)
			{
				continue;
			}

			var candidateScore = ComputePeriodicityScore(document.Values, candidate.Period);
			if (candidateScore > bestScore)
			{
				bestScore = candidateScore;
				bestLabel = candidate.Label;
				bestPeriod = candidate.Period;
			}
		}

		score = bestScore > 0 ? bestScore : null;
		if (bestScore >= 0.85 && bestPeriod.HasValue && bestLabel != null)
		{
			return new PeriodicityInfo(true, bestPeriod, bestLabel);
		}

		return new PeriodicityInfo(false, bestPeriod, bestLabel);
	}

	private static int? TryGetBinsPerDay(int binSize, string binUnit)
	{
		var normalized = binUnit.Trim().ToLowerInvariant();
		return normalized switch
		{
			"minute" or "minutes" => 24 * 60 % binSize == 0 ? 24 * 60 / binSize : null,
			"hour" or "hours" => 24 % binSize == 0 ? 24 / binSize : null,
			"second" or "seconds" => 24 * 60 * 60 % binSize == 0 ? 24 * 60 * 60 / binSize : null,
			"day" or "days" => 1 % binSize == 0 ? 1 / binSize : null,
			_ => null
		};
	}

	private static double ComputePeriodicityScore(double[] values, int period)
	{
		double diffSum = 0;
		double baseSum = 0;

		for (var i = period; i < values.Length; i++)
		{
			var diff = Math.Abs(values[i] - values[i - period]);
			diffSum += diff;
			baseSum += Math.Abs(values[i]);
		}

		if (baseSum <= 0)
		{
			return 0d;
		}

		var score = 1d - (diffSum / baseSum);
		return Math.Clamp(score, 0d, 1d);
	}

	private static string? ResolveProfileMode(string? mode, string? seriesId, double[]? samples, ProfileSummaryStats? summary)
	{
		if (!string.IsNullOrWhiteSpace(mode))
		{
			var normalized = mode.Trim().ToLowerInvariant();
			if (normalized is "profile" or "pmf")
			{
				return normalized;
			}
			return null;
		}

		if (!string.IsNullOrWhiteSpace(seriesId))
		{
			return "profile";
		}

		if (samples is not null && samples.Length > 0)
		{
			return "pmf";
		}

		if (summary is not null)
		{
			return "pmf";
		}

		return null;
	}

	private static (TemplateProfile Profile, string Method, object Summary, object Diagnostics) BuildProfileFit(
		ProfileFitRequest request,
		SeriesStorage storage)
	{
		double[] values;
		string method;
		string source;

		if (!string.IsNullOrWhiteSpace(request.SeriesId))
		{
			var document = storage.Load(request.SeriesId);
			values = document.Values;
			method = "series-normalized";
			source = $"series:{document.SeriesId}";
		}
		else if (request.Summary is not null)
		{
			if (request.Bins is null || request.Bins <= 0)
			{
				throw new InvalidOperationException("bins is required when fitting a profile from summary stats.");
			}

			values = BuildSyntheticProfile(request.Bins.Value, request.Summary);
			method = "summary-synthetic";
			source = "summary";
		}
		else
		{
			throw new InvalidOperationException("seriesId or summary is required to fit a profile.");
		}

		var weights = NormalizeWeights(values);
		var profile = new TemplateProfile
		{
			Kind = "inline",
			Weights = weights
		};

		var summary = new
		{
			source,
			count = values.Length,
			min = values.Min(),
			max = values.Max(),
			avg = values.Average()
		};

		var diagnostics = new
		{
			method,
			weightsMin = weights.Min(),
			weightsMax = weights.Max(),
			weightsAvg = weights.Average()
		};

		return (profile, method, summary, diagnostics);
	}

	private static (PmfSpec Pmf, string Method, object Summary, object Diagnostics) BuildPmfFit(
		ProfileFitRequest request,
		SeriesStorage storage)
	{
		double[] samples;
		string method;
		string source;

		if (request.Samples is not null && request.Samples.Length > 0)
		{
			samples = request.Samples;
			method = "empirical-samples";
			source = "samples";
		}
		else if (!string.IsNullOrWhiteSpace(request.SeriesId))
		{
			var document = storage.Load(request.SeriesId);
			samples = document.Values;
			method = "empirical-series";
			source = $"series:{document.SeriesId}";
		}
		else if (request.Summary is not null)
		{
			var pmfFromSummary = BuildPmfFromSummary(request.Summary);
			var summary = new
			{
				source = "summary",
				count = request.Summary.Count,
				min = request.Summary.Min,
				max = request.Summary.Max
			};
			var diagnostics = new
			{
				method = "synthetic-triangular",
				used = new { request.Summary.Min, request.Summary.Max, request.Summary.P50, request.Summary.Avg }
			};
			return (pmfFromSummary, "synthetic-triangular", summary, diagnostics);
		}
		else
		{
			throw new InvalidOperationException("samples, seriesId, or summary is required to fit a PMF.");
		}

		var pmf = BuildPmfFromSamples(samples);
		var expectedValue = ComputeExpectedValue(pmf);
		var pmfSummary = new
		{
			source,
			count = samples.Length,
			expected = expectedValue,
			min = pmf.Values.Min(),
			max = pmf.Values.Max()
		};
		var pmfDiagnostics = new
		{
			method,
			uniqueValues = pmf.Values.Length
		};

		return (pmf, method, pmfSummary, pmfDiagnostics);
	}

	private static double[] BuildSyntheticProfile(int bins, ProfileSummaryStats summary)
	{
		var weights = Enumerable.Repeat(1d, bins).ToArray();
		if (summary.PeakBin.HasValue && summary.PeakBin.Value >= 0 && summary.PeakBin.Value < bins)
		{
			weights[summary.PeakBin.Value] = 1.5d;
		}
		return weights;
	}

	private static double[] NormalizeWeights(double[] values)
	{
		if (values.Length == 0)
		{
			throw new InvalidOperationException("Profile weights must have at least one value.");
		}

		if (values.Any(v => v < 0))
		{
			throw new InvalidOperationException("Profile weights must be non-negative.");
		}

		var average = values.Average();
		if (average <= 0)
		{
			throw new InvalidOperationException("Profile weights must have a positive average.");
		}

		var scale = 1d / average;
		return values.Select(v => v * scale).ToArray();
	}

	private static PmfSpec BuildPmfFromSamples(double[] samples)
	{
		if (samples.Length == 0)
		{
			throw new InvalidOperationException("samples must contain at least one value.");
		}

		var grouped = samples
			.GroupBy(value => Math.Round(value, 6))
			.OrderBy(group => group.Key)
			.ToArray();

		var values = grouped.Select(group => group.Key).ToArray();
		var counts = grouped.Select(group => group.Count()).ToArray();
		var total = counts.Sum();
		var probabilities = counts.Select(count => count / (double)total).ToArray();

		return new PmfSpec
		{
			Values = values,
			Probabilities = probabilities
		};
	}

	private static PmfSpec BuildPmfFromSummary(ProfileSummaryStats summary)
	{
		if (!summary.Min.HasValue || !summary.Max.HasValue)
		{
			throw new InvalidOperationException("summary.min and summary.max are required to build a PMF.");
		}

		var min = summary.Min.Value;
		var max = summary.Max.Value;

		if (Math.Abs(max - min) < 1e-9)
		{
			return new PmfSpec
			{
				Values = new[] { min },
				Probabilities = new[] { 1d }
			};
		}

		var peak = summary.P50 ?? summary.Avg ?? (min + max) / 2d;
		var values = new[] { min, peak, max };
		var probabilities = new[] { 0.25d, 0.5d, 0.25d };

		return new PmfSpec
		{
			Values = values,
			Probabilities = probabilities
		};
	}

	private static double ComputeExpectedValue(PmfSpec pmf)
	{
		double sum = 0;
		for (var i = 0; i < pmf.Values.Length; i++)
		{
			sum += pmf.Values[i] * pmf.Probabilities[i];
		}
		return sum;
	}

	private static ProfilePreview BuildProfilePreview(ProfilePreviewRequest request)
	{
		if (request.Profile is not null)
		{
			var weights = request.Profile.Weights ?? Array.Empty<double>();
			var summary = new
			{
				kind = "profile",
				count = weights.Length,
				min = weights.Length > 0 ? weights.Min() : 0d,
				max = weights.Length > 0 ? weights.Max() : 0d,
				avg = weights.Length > 0 ? weights.Average() : 0d,
				sample = weights.Take(10).ToArray()
			};

			var diagnostics = new
			{
				normalized = Math.Abs(summary.avg - 1d) < 0.01,
				request.Profile.Kind
			};

			return new ProfilePreview(summary, diagnostics);
		}

		var pmf = request.Pmf ?? new PmfSpec();
		var expected = pmf.Values.Length == 0 ? 0d : ComputeExpectedValue(pmf);
		var summaryPmf = new
		{
			kind = "pmf",
			count = pmf.Values.Length,
			expected,
			min = pmf.Values.Length > 0 ? pmf.Values.Min() : 0d,
			max = pmf.Values.Length > 0 ? pmf.Values.Max() : 0d,
			preview = pmf.Values.Zip(pmf.Probabilities).Take(10).Select(pair => new { value = pair.First, probability = pair.Second }).ToArray()
		};

		var diagnosticsPmf = new
		{
			normalized = Math.Abs(pmf.Probabilities.Sum() - 1d) < 0.01,
			uniqueValues = pmf.Values.Length
		};

		return new ProfilePreview(summaryPmf, diagnosticsPmf);
	}

	// Helper utilities
	public static class ServiceHelpers
	{
		/// <summary>
		/// Gets the root data directory (parent of runs).
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
		/// Gets the draft templates directory.
		/// Order of precedence:
		/// 1. Environment variable FLOWTIME_SIM_DRAFT_TEMPLATES_DIR
		/// 2. Configuration FlowTimeSim:DraftTemplatesDir
		/// 3. Default: "../../templates-draft"
		/// </summary>
		public static string DraftTemplatesRoot(IConfiguration? configuration = null)
		{
			var draftsDir = Environment.GetEnvironmentVariable("FLOWTIME_SIM_DRAFT_TEMPLATES_DIR");
			if (!string.IsNullOrWhiteSpace(draftsDir))
			{
				Directory.CreateDirectory(draftsDir);
				return draftsDir;
			}

			if (configuration != null)
			{
				var configDraftsDir = configuration["FlowTimeSim:DraftTemplatesDir"];
				if (!string.IsNullOrWhiteSpace(configDraftsDir))
				{
					Directory.CreateDirectory(configDraftsDir);
					return configDraftsDir;
				}
			}

			var baseDir = Directory.GetCurrentDirectory();
			var draftRoot = Path.Combine(baseDir, "..", "..", "templates-draft");
			var defaultRoot = Path.GetFullPath(draftRoot);
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

		public static string SeriesRoot(IConfiguration? configuration = null)
		{
			var dataRoot = DataRoot(configuration);
			var seriesDir = Path.Combine(dataRoot, "series");
			Directory.CreateDirectory(seriesDir);
			return seriesDir;
		}

		public static bool IsSafeId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
		public static bool IsSafeSeriesId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length < 128 && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '@');
	}

	public sealed record DraftSourceResolution(string DraftId, string Content);

	public sealed class DraftSource
	{
		public string Type { get; init; } = "inline";
		public string? Id { get; init; }
		public string? Content { get; init; }
	}

	public sealed class DraftTemplateRequest
	{
		public DraftSource Source { get; init; } = new();
		public Dictionary<string, JsonElement>? Parameters { get; init; }
		public string? Mode { get; init; }
		public object? Actor { get; init; }
	}

	public sealed class DraftRunRequest
	{
		public DraftSource Source { get; init; } = new();
		public string? Mode { get; init; }
		public Dictionary<string, JsonElement>? Parameters { get; init; }
		public RunTelemetryOptions? Telemetry { get; init; }
		public RunRngOptions? Rng { get; init; }
		public RunCreationOptions? Options { get; init; }
		public object? Actor { get; init; }
	}

	public sealed class SeriesIngestRequest
	{
		public string? SeriesId { get; init; }
		public string? Format { get; init; }
		public string? Content { get; init; }
		public SeriesMetadata? Metadata { get; init; }
		public string? DetailLevel { get; init; }
	}

	public sealed class SeriesSummaryRequest
	{
		public string? SeriesId { get; init; }
		public string? DetailLevel { get; init; }
	}

	public sealed class ProfileFitRequest
	{
		public string? Mode { get; init; }
		public string? SeriesId { get; init; }
		public double[]? Samples { get; init; }
		public ProfileSummaryStats? Summary { get; init; }
		public int? Bins { get; init; }
		public string? DetailLevel { get; init; }
	}

	public sealed class ProfileSummaryStats
	{
		public double? Min { get; init; }
		public double? Max { get; init; }
		public double? Avg { get; init; }
		public double? P50 { get; init; }
		public double? P90 { get; init; }
		public double? P95 { get; init; }
		public double? P99 { get; init; }
		public int? Count { get; init; }
		public int? PeakBin { get; init; }
	}

	public sealed class ProfilePreviewRequest
	{
		public TemplateProfile? Profile { get; init; }
		public PmfSpec? Pmf { get; init; }
		public string? DetailLevel { get; init; }
	}

	public sealed class DraftProfileMapRequest
	{
		public DraftSource Source { get; init; } = new();
		public string? NodeId { get; init; }
		public TemplateProfile? Profile { get; init; }
		public PmfSpec? Pmf { get; init; }
		public Dictionary<string, string>? Provenance { get; init; }
		public string? DetailLevel { get; init; }
	}

	private sealed record SeriesSummary(
		double Min,
		double Max,
		double Avg,
		Dictionary<string, double> Percentiles,
		int PeakBin,
		double PeakValue,
		PeriodicityInfo Periodicity,
		SeriesSummaryDiagnostics Diagnostics);

	private sealed class SeriesSummaryDiagnostics
	{
		public double Sum { get; init; }
		public double Median { get; init; }
		public double StdDev { get; init; }
		public double? PeriodicityScore { get; init; }
	}

	private sealed record PeriodicityInfo(bool Detected, int? PeriodBins, string? Label);

	private sealed record ProfilePreview(object Summary, object Diagnostics);

	private static class TemplateYamlSerializer
	{
		private static readonly ISerializer Serializer = new SerializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
			.Build();

		public static string Serialize(Template template) => Serializer.Serialize(template);
	}

	// === OBSOLETE DTOs REMOVED ===
	// Overlay feature removed - no longer needed for template-based model generation
}
