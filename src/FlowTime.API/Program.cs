using System.Text;
using FlowTime.Core;
using FlowTime.Core.Models;
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
            
            // Get nodes from the coreModel since Graph doesn't expose them
            var edges = coreModel.Nodes.Select(n => new
            {
                id = n.Id,
                inputs = Array.Empty<string>() // For graph endpoint, we don't need detailed inputs
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


app.Run();

// Allow WebApplicationFactory to reference the entry point for integration tests
public partial class Program 
{ 
    /// <summary>
    /// Get the artifacts directory with proper precedence: Environment Variable > Configuration > Default
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
            return configValue;
        }
        
        // 3. Default to ./data
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }
}
