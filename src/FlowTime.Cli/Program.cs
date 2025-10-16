using FlowTime.Cli.Commands;
using FlowTime.Cli.Configuration;
using FlowTime.Cli.Formatting;
using FlowTime.Core;
using FlowTime.Core.Execution;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Contracts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

if (args.Length == 0 || IsHelp(args[0]))
{
	PrintUsage();
	return 0;
}

if (args[0] == "artifacts")
{
    return await HandleArtifactsCommand(args);
}

if (args[0] == "telemetry")
{
    var telemetryArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();
    if (telemetryArgs.Length == 0)
    {
        PrintTelemetryUsage();
        return 2;
    }

    var subcommand = telemetryArgs[0];
    if (string.Equals(subcommand, "capture", StringComparison.OrdinalIgnoreCase))
    {
        return await TelemetryCaptureCommand.ExecuteAsync(telemetryArgs);
    }

    if (string.Equals(subcommand, "bundle", StringComparison.OrdinalIgnoreCase))
    {
        return await TelemetryBundleCommand.ExecuteAsync(telemetryArgs);
    }

    if (string.Equals(subcommand, "run", StringComparison.OrdinalIgnoreCase))
    {
        return await TelemetryRunCommand.ExecuteAsync(telemetryArgs);
    }

    Console.Error.WriteLine($"Unknown telemetry subcommand: {subcommand}");
    PrintTelemetryUsage();
    return 2;
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
}
Directory.CreateDirectory(outDir);

var yaml = File.ReadAllText(modelPath);

// Validate schema before parsing
var validationResult = ModelValidator.Validate(yaml);
if (!validationResult.IsValid)
{
	Console.Error.WriteLine("Model validation failed:");
	foreach (var error in validationResult.Errors)
	{
		Console.Error.WriteLine($"  - {error}");
	}
	return 1;
}

// Convert YAML to Core model definition and parse using shared ModelParser
FlowTime.Core.Models.TimeGrid grid;
Graph graph;
FlowTime.Core.Models.ModelDefinition coreModel;
try
{
	coreModel = ModelService.ParseAndConvert(yaml);
	
	(grid, graph) = ModelParser.ParseModel(coreModel);
}
catch (ModelParseException ex)
{
	Console.Error.WriteLine($"Error parsing model: {ex.Message}");
	return 1;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Error processing model file: {ex.Message}");
	return 1;
}

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
	Model = coreModel,
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

/// <summary>
/// Handles the 'artifacts list' command to query and display artifacts from the local registry.
/// </summary>
/// <param name="args">Command arguments including subcommand and options</param>
/// <returns>Exit code: 0 for success, 1 for validation errors, 2 for usage errors</returns>
/// <remarks>
/// M2.10: Supports provenance filtering via --template-id and --model-id flags.
/// Works offline by querying the local FileSystemArtifactRegistry.
/// Example: flowtime artifacts list --template-id my-template --limit 10
/// </remarks>
static async Task<int> HandleArtifactsCommand(string[] args)
{
	if (args.Length < 2 || args[1] != "list")
	{
		Console.Error.WriteLine("Unknown artifacts subcommand. Usage: flowtime artifacts list [options]");
		return 2;
	}

	// Parse flags
	string? templateId = null;
	string? modelId = null;
	string dataDir = OutputDirectoryProvider.GetDefaultOutputDirectory();
	int limit = 50;
	int skip = 0;

	for (int i = 2; i < args.Length; i++)
	{
		if (args[i] == "--template-id" && i + 1 < args.Length)
		{
			templateId = args[++i];
		}
		else if (args[i] == "--model-id" && i + 1 < args.Length)
		{
			modelId = args[++i];
		}
		else if (args[i] == "--data-dir" && i + 1 < args.Length)
		{
			dataDir = args[++i];
		}
		else if (args[i] == "--limit" && i + 1 < args.Length)
		{
			if (int.TryParse(args[++i], out var l))
				limit = l;
			else
			{
				Console.Error.WriteLine($"Invalid --limit value: {args[i]}");
				return 2;
			}
		}
		else if (args[i] == "--skip" && i + 1 < args.Length)
		{
			if (int.TryParse(args[++i], out var s))
				skip = s;
			else
			{
				Console.Error.WriteLine($"Invalid --skip value: {args[i]}");
				return 2;
			}
		}
		else
		{
			Console.Error.WriteLine($"Unknown option: {args[i]}");
			return 2;
		}
	}

	// Validate data directory exists
	if (!Directory.Exists(dataDir))
	{
		Console.Error.WriteLine($"Data directory does not exist: {dataDir}");
		return 1;
	}

	// Create registry using shared FileSystemArtifactRegistry
	var config = new ConfigurationBuilder()
		.AddInMemoryCollection(new Dictionary<string, string?> { ["DataDirectory"] = dataDir })
		.Build();

	var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
	var logger = loggerFactory.CreateLogger<FileSystemArtifactRegistry>();
	var registry = new FileSystemArtifactRegistry(config, logger);

	// Rebuild index to ensure all artifacts are discovered
	await registry.RebuildIndexAsync();

	// Query artifacts with provenance filters
	var options = new ArtifactQueryOptions
	{
		TemplateId = templateId,
		ModelId = modelId,
		Limit = limit,
		Skip = skip
	};

	var result = await registry.GetArtifactsAsync(options);

	// Display results as table
	ArtifactTableFormatter.PrintTable(result.Artifacts, result.Total);

	return 0;
}

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
	Console.WriteLine("  flowtime run <model.yaml> [--out <dir>] [--verbose] [--deterministic-run-id] [--seed <n>]\n");
	Console.WriteLine("Options:");
	Console.WriteLine("  --out <dir>             Output directory (default: ./data, or $FLOWTIME_DATA_DIR)");
	Console.WriteLine("  --verbose               Print grid/topology/output summary");
	Console.WriteLine("  --deterministic-run-id  Generate deterministic runId based on scenario hash (for testing/CI)");
	Console.WriteLine("  --seed <n>              RNG seed for reproducible results (default: random)\n");
	Console.WriteLine("Help:");
	Console.WriteLine("  -h | --help | /?        Print this help and exit");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --out out/hello --verbose");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --deterministic-run-id --out out/deterministic");
	Console.WriteLine("  flowtime run examples/hello/model.yaml --seed 42 --verbose");
}

static void PrintTelemetryUsage()
{
    Console.WriteLine("Telemetry Commands");
    Console.WriteLine();
    Console.WriteLine("  flowtime telemetry capture --run-dir <path> [options]");
    Console.WriteLine("  flowtime telemetry bundle --capture-dir <path> --model <model.yaml> [options]");
    Console.WriteLine("  flowtime telemetry run --template-id <id> --capture-dir <path> [options]");
    Console.WriteLine();
    Console.WriteLine("Run with --help after each subcommand for detailed options.");
}

static class JsonOpts
{
	public static readonly System.Text.Json.JsonSerializerOptions Value = new(System.Text.Json.JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};
}
