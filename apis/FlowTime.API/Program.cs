using System.Text;
using FlowTime.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.AspNetCore.HttpLogging;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddOpenApi();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTP logging (development-friendly)
app.UseHttpLogging();

// Static UI (minimal SPA placeholder). Serves /index.html and assets from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

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

// Health
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// POST /run — body: YAML model
app.MapPost("/run", async (HttpRequest req, ILogger<Program> logger) =>
{
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

        // Minimal debug logging of accepted payload (length + preview)
        var previewLen = Math.Min(200, yaml.Length);
        var preview = yaml.Substring(0, previewLen);
        logger.LogDebug("/run accepted YAML: {Length} chars; preview: {Preview}", yaml.Length, preview);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var model = deserializer.Deserialize<ModelDto>(yaml);

        var grid = new TimeGrid(model.Grid.Bins, model.Grid.BinMinutes);
        var nodes = new List<INode>();
        foreach (var n in model.Nodes)
        {
            if (n.Kind == "const")
            {
                if (n.Values is null) return Results.BadRequest(new { error = $"Node {n.Id}: values required for const" });
                nodes.Add(new ConstSeriesNode(n.Id, n.Values));
            }
            else if (n.Kind == "expr")
            {
                if (string.IsNullOrWhiteSpace(n.Expr)) return Results.BadRequest(new { error = $"Node {n.Id}: expr required for expr kind" });
                // M0 expr support: "<name> <op> <scalar>" where op ∈ {*, +}
                var parts = n.Expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    var left = new NodeId(parts[0]);
                    var op = parts[1] == "*" ? BinOp.Mul : BinOp.Add;
                    if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scalar))
                        return Results.BadRequest(new { error = $"Node {n.Id}: invalid scalar '{parts[2]}'" });
                    nodes.Add(new BinaryOpNode(n.Id, left, new NodeId("__scalar__"), op, scalar));
                }
                else
                {
                    return Results.BadRequest(new { error = $"Node {n.Id}: unsupported expr '{n.Expr}' (M0 supports 'name * k' or 'name + k')" });
                }
            }
            else
            {
                return Results.BadRequest(new { error = $"Unknown node kind: {n.Kind}" });
            }
        }

        var graph = new Graph(nodes);
        var order = graph.TopologicalOrder();
        var ctx = graph.Evaluate(grid);

        var series = order.ToDictionary(id => id.Value, id => ctx[id].ToArray());
        var response = new
        {
            grid = new { bins = grid.Bins, binMinutes = grid.BinMinutes },
            order = order.Select(o => o.Value).ToArray(),
            series
        };
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// POST /graph — returns nodes and edges (inputs)
// TODO: Add GET /models/{id}/graph when models become server resources; keep POST for body-supplied YAML in M0.
app.MapPost("/graph", async (HttpRequest req, ILogger<Program> logger) =>
{
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

        // Minimal debug logging of accepted payload (length + preview)
        var previewLen = Math.Min(200, yaml.Length);
        var preview = yaml.Substring(0, previewLen);
        logger.LogDebug("/graph accepted YAML: {Length} chars; preview: {Preview}", yaml.Length, preview);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var model = deserializer.Deserialize<ModelDto>(yaml);

        var nodes = new List<INode>();
        foreach (var n in model.Nodes)
        {
            if (n.Kind == "const") nodes.Add(new ConstSeriesNode(n.Id, n.Values ?? Array.Empty<double>()));
            else if (n.Kind == "expr")
            {
                if (string.IsNullOrWhiteSpace(n.Expr)) return Results.BadRequest(new { error = $"Node {n.Id}: expr required" });
                var parts = n.Expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    var left = new NodeId(parts[0]);
                    var op = parts[1] == "*" ? BinOp.Mul : BinOp.Add;
                    nodes.Add(new BinaryOpNode(n.Id, left, new NodeId("__scalar__"), op, 0.0));
                }
                else return Results.BadRequest(new { error = $"Node {n.Id}: unsupported expr '{n.Expr}'" });
            }
            else return Results.BadRequest(new { error = $"Unknown node kind: {n.Kind}" });
        }

        var graph = new Graph(nodes);
        var order = graph.TopologicalOrder();
        var edges = nodes.Select(n => new
        {
            id = n.Id.Value,
            inputs = n.Inputs.Select(i => i.Value).ToArray()
        });

        return Results.Ok(new
        {
            nodes = nodes.Select(n => n.Id.Value).ToArray(),
            order = order.Select(o => o.Value).ToArray(),
            edges
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

// Request DTOs (YAML)
public sealed class ModelDto
{
    public GridDto Grid { get; set; } = new();
    public List<NodeDto> Nodes { get; set; } = new();
    // M0 note: Outputs are a CLI concern for CSV emission and are ignored by the API.
    public List<OutputDto> Outputs { get; set; } = new();
}

public sealed class GridDto
{
    public int Bins { get; set; }
    public int BinMinutes { get; set; }
}

public sealed class NodeDto
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
}

public sealed class OutputDto
{
    public string Series { get; set; } = "";
    public string As { get; set; } = "out.csv";
}

// Allow WebApplicationFactory to reference the entry point for integration tests
public partial class Program { }
