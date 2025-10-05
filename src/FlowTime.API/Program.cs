using System.Text;
using FlowTime.Core;
using FlowTime.Core.Configuration;
using FlowTime.Core.Models;
using FlowTime.Core.Services;
using FlowTime.Adapters.Synthetic;
using FlowTime.API.Models;
using FlowTime.API.Services;
using FlowTime.Contracts.Services;
using Microsoft.AspNetCore.HttpLogging;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IServiceInfoProvider, ServiceInfoProvider>();
builder.Services.AddSingleton<IArtifactRegistry, FileSystemArtifactRegistry>();
builder.Services.AddSingleton<IArtifactRegistry, FileSystemArtifactRegistry>();
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields =
        HttpLoggingFields.RequestPropertiesAndHeaders |
        HttpLoggingFields.ResponsePropertiesAndHeaders |
        HttpLoggingFields.RequestBody;
    o.RequestBodyLogLimit = 4 * 1024; // 4KB
    o.MediaTypeOptions.AddText("text/plain"); // YAML comes as text/plain in M0
});

// Console logging with timestamps (visible in both terminals and internal console)
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// Permissive CORS for local dev (UI runs on separate origin). Tighten in later milestones.
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTP logging (development-friendly)
app.UseHttpLogging();
app.UseCors();

// Explicit startup log so you can confirm the app is running
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = string.Join(", ", app.Urls);
    app.Logger.LogInformation("FlowTime.API started. Env={Env}; Urls={Urls}", app.Environment.EnvironmentName, urls);
});

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

// Health endpoints
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Enhanced health endpoint with service information
app.MapGet("/v1/healthz", (IServiceInfoProvider serviceInfoProvider) =>
{
    var serviceInfo = serviceInfoProvider.GetServiceInfo();
    return Results.Ok(serviceInfo);
});

// V1 API Group
var v1 = app.MapGroup("/v1");

// Artifacts registry endpoints
v1.MapPost("/artifacts/index", async (IArtifactRegistry registry, ILogger<Program> logger) =>
{
    logger.LogInformation("Rebuilding artifact registry index");
    var index = await registry.RebuildIndexAsync();
    return Results.Ok(index);
});

v1.MapGet("/artifacts", async (IArtifactRegistry registry, HttpContext context, ILogger<Program> logger) =>
{
    var query = context.Request.Query;
    var options = new ArtifactQueryOptions
    {
        // Existing options
        Type = query["type"].FirstOrDefault(),
        Search = query["search"].FirstOrDefault(),
        Tags = query["tags"].FirstOrDefault()?.Split(','),
        Skip = int.TryParse(query["skip"].FirstOrDefault(), out var skip) ? skip : 0,
        Limit = int.TryParse(query["limit"].FirstOrDefault(), out var limit) ? Math.Min(limit, 1000) : 50,
        SortBy = query["sortBy"].FirstOrDefault() ?? "created",
        SortOrder = query["sortOrder"].FirstOrDefault() ?? "desc",
        
        // M2.8 Enhanced options
        CreatedAfter = DateTime.TryParse(query["createdAfter"].FirstOrDefault(), out var after) ? after : null,
        CreatedBefore = DateTime.TryParse(query["createdBefore"].FirstOrDefault(), out var before) ? before : null,
        MinFileSize = long.TryParse(query["minSize"].FirstOrDefault(), out var minSize) ? minSize : null,
        MaxFileSize = long.TryParse(query["maxSize"].FirstOrDefault(), out var maxSize) ? maxSize : null,
        FullTextSearch = query["fullText"].FirstOrDefault(),
        RelatedToArtifact = query["relatedTo"].FirstOrDefault(),
        IncludeArchived = bool.TryParse(query["includeArchived"].FirstOrDefault(), out var includeArchived) ? includeArchived : false,
        
        // M2.10 Provenance options
        TemplateId = query["templateId"].FirstOrDefault(),
        ModelId = query["modelId"].FirstOrDefault()
    };
    
    try
    {
        var response = await registry.GetArtifactsAsync(options);
        return Results.Ok(response);
    }
    catch (FileNotFoundException)
    {
        // If index doesn't exist, rebuild it automatically
        logger.LogInformation("Registry index not found, rebuilding automatically");
        await registry.RebuildIndexAsync();
        var response = await registry.GetArtifactsAsync(options);
        return Results.Ok(response);
    }
});

// M2.8: Artifact relationships endpoint
v1.MapGet("/artifacts/{id}/relationships", async (string id, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        var relationships = await registry.GetArtifactRelationshipsAsync(id);
        return Results.Ok(relationships);
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Artifact not found for relationships: {ArtifactId}", id);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (FileNotFoundException)
    {
        logger.LogWarning("Registry index not found when querying relationships for: {ArtifactId}", id);
        return Results.NotFound(new { error = "Registry index not found. Try rebuilding with POST /v1/artifacts/index" });
    }
});

// Get individual artifact details
v1.MapGet("/artifacts/{id}", async (string id, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        var artifact = await registry.GetArtifactAsync(id);
        if (artifact == null)
        {
            return Results.NotFound(new { error = $"Artifact '{id}' not found" });
        }
        return Results.Ok(artifact);
    }
    catch (FileNotFoundException)
    {
        logger.LogWarning("Registry index not found when querying artifact: {ArtifactId}", id);
        return Results.NotFound(new { error = "Registry index not found. Try rebuilding with POST /v1/artifacts/index" });
    }
});

// Download artifact files as zip
v1.MapGet("/artifacts/{id}/download", async (string id, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        var artifact = await registry.GetArtifactAsync(id);
        if (artifact == null)
        {
            return Results.NotFound(new { error = $"Artifact '{id}' not found" });
        }

        var artifactsDir = Program.GetArtifactsDirectory(app.Configuration);
        var artifactPath = Path.Combine(artifactsDir, id);
        
        if (!Directory.Exists(artifactPath))
        {
            return Results.NotFound(new { error = $"Artifact directory '{id}' not found" });
        }

        // Create a temporary zip file with unique name
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"{id}_{Guid.NewGuid():N}.zip");
        try
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(artifactPath, tempZipPath);
            
            var zipBytes = await File.ReadAllBytesAsync(tempZipPath);
            var fileName = $"{artifact.Title?.Replace(" ", "_") ?? id}.zip";
            
            return Results.File(zipBytes, "application/zip", fileName);
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }
    }
    catch (FileNotFoundException)
    {
        logger.LogWarning("Registry index not found when downloading artifact: {ArtifactId}", id);
        return Results.NotFound(new { error = "Registry index not found. Try rebuilding with POST /v1/artifacts/index" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating zip for artifact: {ArtifactId}", id);
        return Results.Problem("Error creating artifact download");
    }
});

// Get individual artifact file
v1.MapGet("/artifacts/{id}/files/{fileName}", async (string id, string fileName, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        var artifact = await registry.GetArtifactAsync(id);
        if (artifact == null)
        {
            return Results.NotFound(new { error = $"Artifact '{id}' not found" });
        }

        var artifactsDir = Program.GetArtifactsDirectory(app.Configuration);
        var filePath = Path.Combine(artifactsDir, id, fileName);
        
        if (!File.Exists(filePath))
        {
            return Results.NotFound(new { error = $"File '{fileName}' not found in artifact '{id}'" });
        }

        // Determine content type based on file extension
        var contentType = Path.GetExtension(fileName).ToLower() switch
        {
            ".json" => "application/json",
            ".yaml" or ".yml" => "text/yaml",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".log" => "text/plain",
            _ => "application/octet-stream"
        };

        var fileBytes = await File.ReadAllBytesAsync(filePath);
        return Results.File(fileBytes, contentType, fileName);
    }
    catch (FileNotFoundException)
    {
        logger.LogWarning("Registry index not found when accessing file: {FileName} in artifact {ArtifactId}", fileName, id);
        return Results.NotFound(new { error = "Registry index not found. Try rebuilding with POST /v1/artifacts/index" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error accessing file: {FileName} in artifact {ArtifactId}", fileName, id);
        return Results.Problem("Error accessing artifact file");
    }
});

// DEBUG: Test individual directory scanning
v1.MapGet("/debug/scan-directory/{dirName}", async (string dirName, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Debug: Scanning individual directory: {DirName}", dirName);
        var artifactsDir = Program.GetArtifactsDirectory(app.Configuration);
        var fullPath = Path.Combine(artifactsDir, dirName);
        
        if (!Directory.Exists(fullPath))
        {
            return Results.NotFound(new { error = $"Directory not found: {dirName}" });
        }
        
        // Cast to FileSystemArtifactRegistry to access ScanRunDirectoryAsync
        if (registry is FileSystemArtifactRegistry fsRegistry)
        {
            var artifact = await fsRegistry.ScanRunDirectoryAsync(fullPath);
            return Results.Ok(new { 
                directory = dirName, 
                success = true, 
                artifact = artifact 
            });
        }
        
        return Results.BadRequest(new { error = "Registry is not FileSystemArtifactRegistry" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Debug: Failed to scan directory: {DirName}", dirName);
        return Results.BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
    }
});

// V1: POST /v1/artifacts/bulk-delete — body: string[] artifact IDs
v1.MapPost("/artifacts/bulk-delete", async (string[] artifactIds, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Bulk delete request for {Count} artifacts: {Ids}", artifactIds.Length, string.Join(", ", artifactIds));
        
        var results = new List<object>();
        var artifactsDir = Program.GetArtifactsDirectory(app.Configuration);
        
        foreach (var artifactId in artifactIds)
        {
            try
            {
                var artifactPath = Path.Combine(artifactsDir, artifactId);
                if (Directory.Exists(artifactPath))
                {
                    Directory.Delete(artifactPath, recursive: true);
                    await registry.RemoveArtifactAsync(artifactId);
                    results.Add(new { id = artifactId, success = true });
                    logger.LogInformation("Successfully deleted artifact: {ArtifactId}", artifactId);
                }
                else
                {
                    results.Add(new { id = artifactId, success = false, error = "Artifact not found" });
                    logger.LogWarning("Artifact not found for deletion: {ArtifactId}", artifactId);
                }
            }
            catch (Exception ex)
            {
                results.Add(new { id = artifactId, success = false, error = ex.Message });
                logger.LogError(ex, "Error deleting artifact: {ArtifactId}", artifactId);
            }
        }
        
        var successCount = results.Count(r => ((dynamic)r).success);
        logger.LogInformation("Bulk delete completed: {SuccessCount}/{TotalCount} artifacts deleted", successCount, artifactIds.Length);
        
        return Results.Ok(new { 
            success = true, 
            processed = artifactIds.Length,
            deleted = successCount,
            results = results 
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in bulk delete operation");
        return Results.Problem("Error performing bulk delete operation");
    }
});

// V1: POST /v1/artifacts/archive — body: string[] artifact IDs
v1.MapPost("/artifacts/archive", async (string[] artifactIds, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Bulk archive request for {Count} artifacts: {Ids}", artifactIds.Length, string.Join(", ", artifactIds));
        
        var results = new List<object>();
        
        foreach (var artifactId in artifactIds)
        {
            try
            {
                var artifact = await registry.GetArtifactAsync(artifactId);
                if (artifact != null)
                {
                    // Add 'archived' tag if not already present
                    if (!artifact.Tags.Contains("archived"))
                    {
                        var tagsList = artifact.Tags.ToList();
                        tagsList.Add("archived");
                        artifact.Tags = tagsList.ToArray();
                        await registry.AddOrUpdateArtifactAsync(artifact);
                    }
                    results.Add(new { id = artifactId, success = true });
                    logger.LogInformation("Successfully archived artifact: {ArtifactId}", artifactId);
                }
                else
                {
                    results.Add(new { id = artifactId, success = false, error = "Artifact not found" });
                    logger.LogWarning("Artifact not found for archiving: {ArtifactId}", artifactId);
                }
            }
            catch (Exception ex)
            {
                results.Add(new { id = artifactId, success = false, error = ex.Message });
                logger.LogError(ex, "Error archiving artifact: {ArtifactId}", artifactId);
            }
        }
        
        var successCount = results.Count(r => ((dynamic)r).success);
        logger.LogInformation("Bulk archive completed: {SuccessCount}/{TotalCount} artifacts archived", successCount, artifactIds.Length);
        
        return Results.Ok(new { 
            success = true, 
            processed = artifactIds.Length,
            archived = successCount,
            results = results 
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in bulk archive operation");
        return Results.Problem("Error performing bulk archive operation");
    }
});

// V1: POST /v1/run — body: YAML model
v1.MapPost("/run", async (HttpRequest req, IArtifactRegistry registry, ILogger<Program> logger) =>
{
    string yaml = string.Empty;
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

        // Minimal debug logging of accepted payload (length + preview)
        var previewLen = Math.Min(200, yaml.Length);
        var preview = yaml.Substring(0, previewLen);
        logger.LogDebug("/run accepted YAML: {Length} chars; preview: {Preview}", yaml.Length, preview);

        // Extract provenance metadata (from header or embedded YAML)
        ProvenanceMetadata? provenance;
        try
        {
            provenance = ProvenanceService.ExtractProvenance(req, yaml, logger);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = $"Invalid provenance: {ex.Message}" });
        }
        
        string? provenanceJson = null;
        if (provenance != null)
        {
            // Set received timestamp
            provenance.ReceivedAt = DateTime.UtcNow.ToString("o");
            provenanceJson = System.Text.Json.JsonSerializer.Serialize(provenance, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        // Strip provenance from YAML to get clean execution spec
        var cleanYaml = ProvenanceService.StripProvenance(yaml);

        // Validate schema before parsing
        var validationResult = ModelValidator.Validate(cleanYaml);
        if (!validationResult.IsValid)
        {
            var errorMsg = string.Join("; ", validationResult.Errors);
            return Results.BadRequest(new { error = errorMsg });
        }

        // Convert API DTO to Core model definition and parse using shared ModelParser
        FlowTime.Core.TimeGrid grid;
        Graph graph;
        FlowTime.Core.Models.ModelDefinition coreModel;
        try
        {
            coreModel = ModelService.ParseAndConvert(cleanYaml);
            
            (grid, graph) = ModelParser.ParseModel(coreModel);
        }
        catch (ModelParseException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var order = graph.TopologicalOrder();
        var ctx = graph.Evaluate(grid);

        // Build artifacts using shared writer
        var artifactsDir = Program.GetArtifactsDirectory(app.Configuration);
        Directory.CreateDirectory(artifactsDir);
        
        // Create context dictionary for artifact writer
        var artifactContext = new Dictionary<NodeId, double[]>();
        foreach (var (nodeId, seriesData) in ctx)
        {
            artifactContext[nodeId] = seriesData.ToArray();
        }

        // Use shared artifact writer
        var writeRequest = new FlowTime.Core.Artifacts.RunArtifactWriter.WriteRequest
        {
            Model = coreModel,
            Grid = grid,
            Context = artifactContext,
            SpecText = cleanYaml, // Use clean YAML without provenance
            RngSeed = null, // API doesn't support seed parameter yet
            StartTimeBias = null,
            DeterministicRunId = false,
            OutputDirectory = artifactsDir,
            Verbose = false,
            ProvenanceJson = provenanceJson // Include provenance if present
        };

        var artifactResult = await FlowTime.Core.Artifacts.RunArtifactWriter.WriteArtifactsAsync(writeRequest);
        logger.LogInformation("Created artifacts at {RunDirectory}", artifactResult.RunDirectory);

        // Automatically add new run to artifact registry (fire-and-forget to avoid blocking response)
        _ = Task.Run(async () =>
        {
            try
            {
                var artifact = await registry.ScanRunDirectoryAsync(artifactResult.RunDirectory);
                if (artifact != null)
                {
                    await registry.AddOrUpdateArtifactAsync(artifact);
                    logger.LogDebug("Automatically added run {RunId} to artifact registry", artifactResult.RunId);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the main request
                logger.LogWarning(ex, "Failed to automatically add run {RunId} to artifact registry", artifactResult.RunId);
            }
        });

        var series = order.ToDictionary(id => id.Value, id => ctx[id].ToArray());
        var response = new
        {
            grid = new { bins = grid.Bins, binSize = grid.BinSize, binUnit = grid.BinUnit.ToString().ToLowerInvariant() },
            order = order.Select(o => o.Value).ToArray(),
            series,
            runId = artifactResult.RunId,
            artifactsPath = artifactResult.RunDirectory,
            modelHash = artifactResult.ScenarioHash // Include model hash for deduplication
        };
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "/run parse/eval failed. Raw YAML length={Length}. First 120 chars: {Preview}", yaml?.Length, yaml is null ? "" : yaml.Substring(0, Math.Min(120, yaml.Length)));
        return Results.BadRequest(new { error = ex.Message });
    }
});
// V1: POST /v1/graph — returns nodes and edges (inputs)
v1.MapPost("/graph", async (HttpRequest req, ILogger<Program> logger) =>
{
    string yaml = string.Empty;
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

        // Minimal debug logging of accepted payload (length + preview)
        var previewLen = Math.Min(200, yaml.Length);
        var preview = yaml.Substring(0, previewLen);
        logger.LogDebug("/graph accepted YAML: {Length} chars; preview: {Preview}", yaml.Length, preview);

        // Convert API DTO to Core model definition and parse using shared ModelParser
        try
        {
            var coreModel = ModelService.ParseAndConvert(yaml);
            
            var (grid, graph) = ModelParser.ParseModel(coreModel);
            var order = graph.TopologicalOrder();
            
            // Since Graph doesn't expose nodes directly, we need to get the info from the model definition
            // and construct the response based on the node metadata in the coreModel
            var edges = coreModel.Nodes.Select(n => new
            {
                id = n.Id,
                inputs = GraphAnalyzer.GetNodeInputs(n, coreModel)
            });

            return Results.Ok(new
            {
                nodes = coreModel.Nodes.Select(n => n.Id).ToArray(),
                order = order.Select(o => o.Value).ToArray(),
                edges
            });
        }
        catch (ModelParseException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "/graph parse failed. Raw YAML length={Length}. First 120 chars: {Preview}", yaml?.Length, yaml is null ? "" : yaml.Substring(0, Math.Min(120, yaml.Length)));
        return Results.BadRequest(new { error = ex.Message });
    }
});

// V1: Artifact endpoints
v1.MapGet("/runs/{runId}/index", async (string runId, ILogger<Program> logger) =>
{
    try
    {
        var artifactsDirectory = Program.GetArtifactsDirectory(builder.Configuration);
        var reader = new FileSeriesReader();
        var runPath = Path.Combine(artifactsDirectory, runId);
        
        if (!Directory.Exists(runPath))
        {
            return Results.NotFound(new { error = $"Run {runId} not found" });
        }

        var adapter = new RunArtifactAdapter(reader, runPath);
        var index = await adapter.GetIndexAsync();
        
        return Results.Ok(index);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to read index for run {RunId}", runId);
        return Results.Problem($"Failed to read index: {ex.Message}");
    }
});

v1.MapGet("/runs/{runId}/series/{seriesId}", async (string runId, string seriesId, ILogger<Program> logger) =>
{
    try
    {
        var artifactsDirectory = Program.GetArtifactsDirectory(builder.Configuration);
        var reader = new FileSeriesReader();
        var runPath = Path.Combine(artifactsDirectory, runId);
        
        if (!Directory.Exists(runPath))
        {
            return Results.NotFound(new { error = $"Run {runId} not found" });
        }

        // First try exact match
        string actualSeriesId = seriesId;
        if (!reader.SeriesExists(runPath, seriesId))
        {
            // Try to find matching series by simple name (e.g., "demand" -> "demand@DEMAND@DEFAULT")
            try
            {
                var adapter = new RunArtifactAdapter(reader, runPath);
                var index = await adapter.GetIndexAsync();
                var matchingSeries = index.Series.FirstOrDefault(s => s.Id.StartsWith(seriesId + "@"));
                
                if (matchingSeries != null)
                {
                    actualSeriesId = matchingSeries.Id;
                }
                else
                {
                    return Results.NotFound(new { error = $"Series {seriesId} not found in run {runId}" });
                }
            }
            catch (FileNotFoundException)
            {
                // No index.json means no series exist for this run
                return Results.NotFound(new { error = $"Series {seriesId} not found in run {runId}" });
            }
        }

        var series = await reader.ReadSeriesAsync(runPath, actualSeriesId);
        
        // Convert Series to CSV string
        var csv = new StringBuilder();
        csv.AppendLine("t,value");
        var values = series.ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            csv.AppendLine($"{i},{values[i].ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        return Results.Text(csv.ToString(), "text/csv");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to read series {SeriesId} for run {RunId}", seriesId, runId);
        return Results.Problem($"Failed to read series: {ex.Message}");
    }
});

// V1: Export endpoints - M2.6 Export System (REST-compliant design)

// Export endpoint - creates all export formats and saves to artifacts (POST for side effects)
v1.MapPost("/runs/{runId}/export", async (string runId, ILogger<Program> logger) =>
{
    try
    {
        var artifactsDirectory = Program.GetArtifactsDirectory(builder.Configuration);
        var runPath = Path.Combine(artifactsDirectory, runId);
        
        if (!Directory.Exists(runPath))
        {
            return Results.NotFound(new { error = $"Run {runId} not found" });
        }

        // Save all available export formats to disk
        await SaveAllExportFormatsAsync(runPath, logger);
        
        var goldDirectory = Path.Combine(runPath, "gold");
        return Results.Ok(new { 
            message = "Export completed successfully",
            runId = runId,
            formats = new[] { "csv", "ndjson", "parquet" },
            artifactsPath = goldDirectory,
            files = new[] {
                "export.csv",
                "export.ndjson", 
                "export.parquet"
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to export run {RunId}", runId);
        return Results.Problem($"Export failed: {ex.Message}");
    }
});

// Retrieve endpoint - returns specific format data (GET with no side effects)  
v1.MapGet("/runs/{runId}/export/{format}", async (string runId, string format, ILogger<Program> logger) =>
{
    try
    {
        var artifactsDirectory = Program.GetArtifactsDirectory(builder.Configuration);
        var runPath = Path.Combine(artifactsDirectory, runId);
        
        if (!Directory.Exists(runPath))
        {
            return Results.NotFound(new { error = $"Run {runId} not found" });
        }

        // Return the requested format (no side effects)
        return format.ToLowerInvariant() switch
        {
            "gold" or "csv" => await GetGoldCsvResponse(runPath, runId, logger),
            "ndjson" => await GetNdjsonResponse(runPath, runId, logger),
            "parquet" => await GetParquetResponse(runPath, runId, logger),
            _ => Results.BadRequest(new { error = $"Unsupported export format: {format}. Supported formats: gold, csv, ndjson, parquet" })
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve run {RunId} in format {Format}", runId, format);
        return Results.Problem($"Retrieve failed: {ex.Message}");
    }
});

// Helper methods for format-specific responses
static async Task<IResult> GetGoldCsvResponse(string runPath, string runId, ILogger logger)
{
    var goldCsvPath = Path.Combine(runPath, "gold", "export.csv");
    if (!File.Exists(goldCsvPath))
    {
        return Results.NotFound(new { error = $"Export CSV file not found for run {runId}. Run POST /export to create it first." });
    }
    
    var csvContent = await File.ReadAllTextAsync(goldCsvPath);
    logger.LogInformation("Retrieved Gold CSV export for run {RunId}: {Size} bytes", runId, csvContent.Length);
    return Results.Text(csvContent, "text/csv", Encoding.UTF8);
}

static async Task<IResult> GetNdjsonResponse(string runPath, string runId, ILogger logger)
{
    var ndjsonPath = Path.Combine(runPath, "gold", "export.ndjson");
    if (!File.Exists(ndjsonPath))
    {
        return Results.NotFound(new { error = $"Export NDJSON file not found for run {runId}. Run POST /export to create it first." });
    }
    
    var ndjsonContent = await File.ReadAllTextAsync(ndjsonPath);
    logger.LogInformation("Retrieved NDJSON export for run {RunId}: {Size} bytes", runId, ndjsonContent.Length);
    return Results.Text(ndjsonContent, "application/x-ndjson", Encoding.UTF8);
}

static async Task<IResult> GetParquetResponse(string runPath, string runId, ILogger logger)
{
    var parquetPath = Path.Combine(runPath, "gold", "export.parquet");
    if (!File.Exists(parquetPath))
    {
        return Results.NotFound(new { error = $"Export Parquet file not found for run {runId}. Run POST /export to create it first." });
    }
    
    var parquetData = await File.ReadAllBytesAsync(parquetPath);
    logger.LogInformation("Retrieved Parquet export for run {RunId}: {Size} bytes", runId, parquetData.Length);
    return Results.Bytes(parquetData, "application/octet-stream", $"{runId}.parquet");
}

// Helper method to save all export formats to disk
static async Task SaveAllExportFormatsAsync(string runPath, ILogger logger)
{
    var goldDirectory = Path.Combine(runPath, "gold");
    Directory.CreateDirectory(goldDirectory); // Ensure gold directory exists
    
    try
    {
        // Save Gold CSV format
        var goldCsvPath = Path.Combine(goldDirectory, "export.csv");
        var csvResult = await GoldCsvExporter.ExportToFileAsync(runPath, goldCsvPath);
        logger.LogInformation("Saved Gold CSV export: {FilePath} ({RowCount} rows, {SeriesCount} series)", 
            goldCsvPath, csvResult.RowCount, csvResult.SeriesCount);
        
        // Save NDJSON format
        var ndjsonPath = Path.Combine(goldDirectory, "export.ndjson");
        var ndjsonResult = await NdjsonExporter.ExportToFileAsync(runPath, ndjsonPath);
        logger.LogInformation("Saved NDJSON export: {FilePath} ({RowCount} rows, {SeriesCount} series)", 
            ndjsonPath, ndjsonResult.RowCount, ndjsonResult.SeriesCount);
        
        // Save Parquet format  
        var parquetPath = Path.Combine(goldDirectory, "export.parquet");
        var parquetResult = await ParquetExporter.ExportToFileAsync(runPath, parquetPath);
        logger.LogInformation("Saved Parquet export: {FilePath} ({RowCount} rows, {SeriesCount} series)", 
            parquetPath, parquetResult.RowCount, parquetResult.SeriesCount);
        
        logger.LogInformation("Saved all export formats for run at {RunPath}: CSV, NDJSON, Parquet", runPath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to save export formats for run at {RunPath}", runPath);
        throw; // Re-throw so the endpoint can return proper error response
    }
}

app.Run();

// Allow WebApplicationFactory to reference the entry point for integration tests
public partial class Program 
{ 
    /// <summary>
    /// Get the artifacts directory with proper precedence: Environment Variable > Configuration > Solution Root Default
    /// </summary>
    public static string GetArtifactsDirectory(IConfiguration configuration)
    {
        // 1. Environment variable has highest precedence
        var envVar = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return envVar;
        }
        
        // 2. Configuration setting (appsettings.json, etc.)
        var configValue = configuration.GetValue<string>("ArtifactsDirectory");
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            // If config value is absolute, use it as-is
            if (Path.IsPathRooted(configValue))
            {
                return configValue;
            }
            
            // If config value is relative, make it relative to solution root
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            if (!string.IsNullOrEmpty(solutionRoot))
            {
                return Path.Combine(solutionRoot, configValue);
            }
            
            // Fallback: relative to current directory
            return Path.Combine(Directory.GetCurrentDirectory(), configValue);
        }
        
        // 3. Default to solution root data directory
        return DirectoryProvider.GetDefaultDataDirectory();
    }
    
    /// <summary>
    /// Helper utilities (similar to flowtime-sim-vnext approach)
    /// </summary>
    public static class ServiceHelpers
    {
        /// <summary>
        /// Gets the root data directory.
        /// Order of precedence:
        /// 1. Environment variable FLOWTIME_DATA_DIR
        /// 2. Configuration ArtifactsDirectory
        /// 3. Default: solution root + "/data"
        /// </summary>
        public static string DataRoot(IConfiguration? configuration = null)
        {
            // Check environment variable first
            var dataDir = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                return dataDir;
            }

            // Check configuration if provided
            if (configuration != null)
            {
                var configDataDir = configuration["ArtifactsDirectory"];
                if (!string.IsNullOrEmpty(configDataDir))
                {
                    // If absolute path, use as-is
                    if (Path.IsPathRooted(configDataDir))
                    {
                        Directory.CreateDirectory(configDataDir);
                        return configDataDir;
                    }
                    
                    // If relative path, make it relative to solution root
                    var solutionRoot = DirectoryProvider.FindSolutionRoot();
                    if (!string.IsNullOrEmpty(solutionRoot))
                    {
                        var fullPath = Path.Combine(solutionRoot, configDataDir);
                        Directory.CreateDirectory(fullPath);
                        return fullPath;
                    }
                }
            }

            // Default to solution root + data
            var defaultPath = DirectoryProvider.GetDefaultDataDirectory();
            Directory.CreateDirectory(defaultPath);
            return defaultPath;
        }

        /// <summary>
        /// Gets the runs directory (same as data directory for FlowTime API)
        /// </summary>
        public static string RunsRoot(IConfiguration? configuration = null)
        {
            // For FlowTime API, runs are stored directly in the data directory
            return DataRoot(configuration);
        }
    }
}
