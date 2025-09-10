using FlowTime.Cli.Configuration;
using FlowTime.Core;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.Json;

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
string outDir = OutputDirectoryProvider.GetDefaultOutputDirectory();
bool verbose = false;
bool deterministicRunId = false;
int? rngSeed = null;
double? startTimeBias = null;
string? viaApi = null;
for (int i = 2; i < args.Length; i++)
{
	if (args[i] == "--out" && i + 1 < args.Length) { outDir = args[++i]; }
	else if (args[i] == "--verbose") { verbose = true; }
	else if (args[i] == "--deterministic-run-id") { deterministicRunId = true; }
	else if (args[i] == "--seed" && i + 1 < args.Length) 
	{ 
		if (int.TryParse(args[++i], out var seed)) rngSeed = seed;
		else throw new ArgumentException($"Invalid seed value: {args[i]}");
	}
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

// Parse the model using shared ModelParser
var coreModel = new ModelDefinition
{
	Grid = new GridDefinition { Bins = model.Grid.Bins, BinMinutes = model.Grid.BinMinutes },
	Nodes = model.Nodes.Select(n => new NodeDefinition 
	{ 
		Id = n.Id, 
		Kind = n.Kind, 
		Values = n.Values, 
		Expr = n.Expr 
	}).ToList(),
	Outputs = model.Outputs.Select(o => new OutputDefinition 
	{ 
		Series = o.Series, 
		As = o.As 
	}).ToList()
};
var (grid, graph) = ModelParser.ParseModel(coreModel);
var order = graph.TopologicalOrder();
var ctx = graph.Evaluate(grid);

if (verbose)
{
	Console.WriteLine("FlowTime run summary:");
	Console.WriteLine($"  Grid: bins={grid.Bins}, binMinutes={grid.BinMinutes}");
	Console.WriteLine("  Topological order: " + string.Join(" -> ", order.Select(o => o.Value)));
}

// Persist spec.yaml verbatim (line ending normalized) and compute canonical scenario/model hash
var specVerbatim = yaml.Replace("\r\n", "\n");

	// Create context dictionary for artifact writer
	var context = new Dictionary<NodeId, double[]>();
	foreach (var (nodeId, series) in ctx)
	{
		context[nodeId] = series.ToArray();
	}

	// Use shared artifact writer
	var writeRequest = new RunArtifactWriter.WriteRequest
	{
		Model = model,
		Grid = grid,
		Context = context,
		SpecText = specVerbatim,
		RngSeed = rngSeed,
		StartTimeBias = startTimeBias,
		DeterministicRunId = deterministicRunId,
		OutputDirectory = outDir,
		Verbose = verbose
	};

	var result = await RunArtifactWriter.WriteArtifactsAsync(writeRequest);
	if (verbose) Console.WriteLine($"  RNG seed: {result.FinalSeed} ({(rngSeed.HasValue ? "provided" : "generated")})");
	Console.WriteLine($"Wrote artifacts to {result.RunDirectory}");

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
	Console.WriteLine("  flowtime run <model.yaml> [--out <dir>] [--verbose] [--deterministic-run-id] [--seed <n>] [--via-api <url>]\n");
	Console.WriteLine("Options:");
	Console.WriteLine("  --out <dir>             Output directory (default: ./data, or $FLOWTIME_DATA_DIR)");
	Console.WriteLine("  --verbose               Print grid/topology/output summary");
	Console.WriteLine("  --deterministic-run-id  Generate deterministic runId based on scenario hash (for testing/CI)");
	Console.WriteLine("  --seed <n>              RNG seed for reproducible results (default: random)");
	Console.WriteLine("  --via-api <url>         Route run via API for parity (falls back to local until SVC-M0)\n");
	Console.WriteLine("Help:");
	Console.WriteLine("  -h | --help | /?        Print this help and exit");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --out out/hello --verbose");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --deterministic-run-id --out out/deterministic");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --seed 42 --verbose");
}
	file static class JsonOpts
	{
		public static readonly System.Text.Json.JsonSerializerOptions Value = new(System.Text.Json.JsonSerializerDefaults.Web)
		{
			WriteIndented = true
		};
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
