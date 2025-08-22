using System.Text;
using FlowTime.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// POST /run — body: YAML model
app.MapPost("/run", async (HttpRequest req) =>
{
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

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
app.MapPost("/graph", async (HttpRequest req) =>
{
    try
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var yaml = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(yaml)) return Results.BadRequest(new { error = "Empty request body" });

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
