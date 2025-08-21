using FlowTime.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

if (args.Length == 0 || IsHelp(args[0]))
{
	PrintUsage();
	return 0;
}

if (args[0] != "run")
{
	PrintUsage();
	return 2;
}

string modelPath = args.Length > 1 ? args[1] : throw new ArgumentException("Missing model.yaml path");
string outDir = "out/run";
bool verbose = false;
string? viaApi = null;
for (int i = 2; i < args.Length; i++)
{
	if (args[i] == "--out" && i + 1 < args.Length) { outDir = args[++i]; }
	else if (args[i] == "--verbose") { verbose = true; }
	else if (args[i] == "--via-api" && i + 1 < args.Length) { viaApi = args[++i]; }
}
Directory.CreateDirectory(outDir);

var yaml = File.ReadAllText(modelPath);
var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
var model = deserializer.Deserialize<ModelDto>(yaml);

if (!string.IsNullOrWhiteSpace(viaApi))
{
	Console.WriteLine($"--via-api is specified ({viaApi}), but HTTP mode isn’t implemented yet. Running locally for now to preserve parity and offline support.");
}

var grid = new TimeGrid(model.Grid.Bins, model.Grid.BinMinutes);
var nodes = new List<INode>();
foreach (var n in model.Nodes)
{
	if (n.Kind == "const") nodes.Add(new ConstSeriesNode(n.Id, n.Values!));
	else if (n.Kind == "expr")
	{
		var parts = n.Expr!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 3)
		{
			var left = new NodeId(parts[0]);
			var op = parts[1] == "*" ? BinOp.Mul : BinOp.Add;
			var scalar = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
			nodes.Add(new BinaryOpNode(n.Id, left, new NodeId("__scalar__"), op, scalar));
		}
		else throw new InvalidOperationException($"Unsupported expr: {n.Expr}");
	}
	else throw new InvalidOperationException($"Unknown kind: {n.Kind}");
}

var graph = new Graph(nodes);
var order = graph.TopologicalOrder();
var ctx = graph.Evaluate(grid);

if (verbose)
{
	Console.WriteLine("FlowTime run summary:");
	Console.WriteLine($"  Grid: bins={grid.Bins}, binMinutes={grid.BinMinutes}");
	Console.WriteLine("  Topological order: " + string.Join(" -> ", order.Select(o => o.Value)));
}

foreach (var output in model.Outputs)
{
	var id = new NodeId(output.Series);
	var s = ctx[id];
	var path = Path.Combine(outDir, output.As);
	using var w = new StreamWriter(path);
	w.WriteLine("t,value");
	for (int t = 0; t < s.Length; t++) w.WriteLine($"{t},{s[t].ToString(System.Globalization.CultureInfo.InvariantCulture)}");
	if (verbose) Console.WriteLine($"  Wrote {output.As} ({s.Length} rows)");
}

Console.WriteLine($"Wrote outputs to {outDir}");
return 0;

static bool IsHelp(string? s)
{
	if (string.IsNullOrWhiteSpace(s)) return true;
	s = s.Trim();
	return s.Equals("-h", StringComparison.OrdinalIgnoreCase)
		|| s.Equals("--help", StringComparison.OrdinalIgnoreCase)
		|| s.Equals("/h", StringComparison.OrdinalIgnoreCase)
		|| s.Equals("/?", StringComparison.OrdinalIgnoreCase)
		|| s.Equals("help", StringComparison.OrdinalIgnoreCase);
}

static void PrintUsage()
{
	Console.WriteLine("FlowTime CLI (M0)\n");
	Console.WriteLine("Usage:");
	Console.WriteLine("  flowtime run <model.yaml> [--out <dir>] [--verbose] [--via-api <url>]\n");
	Console.WriteLine("Options:");
	Console.WriteLine("  --out <dir>         Output directory (default: out/run)");
	Console.WriteLine("  --verbose           Print grid/topology/output summary");
	Console.WriteLine("  --via-api <url>     Route run via API for parity (falls back to local until SVC-M0)\n");
	Console.WriteLine("Help:");
	Console.WriteLine("  -h | --help | /?    Print this help and exit");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --out out/hello --verbose");
}

// DTOs for YAML
public sealed class ModelDto
{
	public GridDto Grid { get; set; } = default!;
	public List<NodeDto> Nodes { get; set; } = new();
	public List<OutputDto> Outputs { get; set; } = new();
}
public sealed class GridDto { public int Bins { get; set; } public int BinMinutes { get; set; } }
public sealed class NodeDto { public string Id { get; set; } = ""; public string Kind { get; set; } = "const"; public double[]? Values { get; set; } public string? Expr { get; set; } }
public sealed class OutputDto { public string Series { get; set; } = ""; public string As { get; set; } = "out.csv"; }
