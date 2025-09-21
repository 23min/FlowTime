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

// V1: POST /v1/run — body: YAML model
v1.MapPost("/run", async (HttpRequest req, ILogger<Program> logger) =>
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

        // Convert API DTO to Core model definition and parse using shared ModelParser
        FlowTime.Core.TimeGrid grid;
        Graph graph;
        FlowTime.Core.Models.ModelDefinition coreModel;
        try
        {
            coreModel = ModelService.ParseAndConvert(yaml);
            
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
            SpecText = yaml,
            RngSeed = null, // API doesn't support seed parameter yet
            StartTimeBias = null,
            DeterministicRunId = false,
            OutputDirectory = artifactsDir,
            Verbose = false
        };

        var artifactResult = await FlowTime.Core.Artifacts.RunArtifactWriter.WriteArtifactsAsync(writeRequest);
        logger.LogInformation("Created artifacts at {RunDirectory}", artifactResult.RunDirectory);

        var series = order.ToDictionary(id => id.Value, id => ctx[id].ToArray());
        var response = new
        {
            grid = new { bins = grid.Bins, binMinutes = grid.BinMinutes },
            order = order.Select(o => o.Value).ToArray(),
            series,
            runId = artifactResult.RunId,
            artifactsPath = artifactResult.RunDirectory
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
