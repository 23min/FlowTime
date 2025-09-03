using FlowTime.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.RepresentationModel; // for canonical hashing
using Json.Schema; // for JSON Schema validation
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
string outDir = "out/run";
bool verbose = false;
bool deterministicRunId = false;
int? rngSeed = null;
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

// Persist spec.yaml verbatim (line ending normalized) and compute canonical scenario/model hash
var specVerbatim = yaml.Replace("\r\n", "\n");
var scenarioHash = ComputeScenarioHash(specVerbatim);

// M1 artifact layout: runs/<runId>/...
var runId = deterministicRunId ? 
	$"engine_deterministic_{scenarioHash[7..15]}" : // use first 8 chars of hash for deterministic case
	$"engine_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{Guid.NewGuid().ToString("N")[..8]}";
var runDir = Path.Combine(outDir, runId);
var seriesDir = Path.Combine(runDir, "series");
Directory.CreateDirectory(seriesDir);
Directory.CreateDirectory(Path.Combine(runDir, "gold")); // placeholder

File.WriteAllText(Path.Combine(runDir, "spec.yaml"), specVerbatim);

var seriesMetas = new List<SeriesMeta>();
var seriesHashes = new Dictionary<string,string>();

// Write per-series CSVs based on requested outputs only (M0 behavior)
foreach (var output in model.Outputs)
{
	var nodeId = new NodeId(output.Series);
	var s = ctx[nodeId];
	var measure = output.Series; // the measure name (e.g., "served", "arrivals")
	var componentId = nodeId.Value.ToUpperInvariant(); // component ID (e.g., "SERVED")
	var seriesId = $"{measure}@{componentId}@DEFAULT"; // measure@componentId@class format per contracts
	var csvName = seriesId + ".csv";
	var path = Path.Combine(seriesDir, csvName);
	using (var w = new StreamWriter(path, false, System.Text.Encoding.UTF8, 4096))
	{
		w.NewLine = "\n";
		w.WriteLine("t,value");
		for (int t = 0; t < s.Length; t++)
		{
			w.Write(t);
			w.Write(',');
			w.Write(s[t].ToString(System.Globalization.CultureInfo.InvariantCulture));
			w.Write('\n');
		}
	}
	var bytes = File.ReadAllBytes(path);
	var hash = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
	seriesHashes[seriesId] = hash;
	seriesMetas.Add(new SeriesMeta
	{
		Id = seriesId,
		Kind = "flow", // until more kinds
		Path = $"series/{csvName}",
		Unit = "entities/bin",
		ComponentId = nodeId.Value.ToUpperInvariant(),
		Class = "DEFAULT",
		Points = s.Length,
		Hash = hash
	});
	if (verbose) Console.WriteLine($"  Wrote {csvName} ({s.Length} rows)");
}

// Build run.json
var runJson = new RunJson
{
	SchemaVersion = 1,
	RunId = runId,
	EngineVersion = "0.1.0", // TODO: derive from assembly
	Source = "engine",
	Grid = new GridJson { Bins = grid.Bins, BinMinutes = grid.BinMinutes, Timezone = "UTC", Align = "left" },
	ScenarioHash = scenarioHash,
	ModelHash = scenarioHash, // engine MAY emit modelHash; using same canonical hash for now
	Warnings = Array.Empty<string>(),
	Series = seriesMetas.Select(m => new RunSeriesEntry { Id = m.Id, Path = m.Path, Unit = m.Unit }).ToList(),
	Events = new EventsJson { SchemaVersion = 0, FieldsReserved = new[]{"entityType","eventType","componentId","connectionId","class","simTime","wallTime","correlationId","attrs"} }
};
File.WriteAllText(Path.Combine(runDir, "run.json"), System.Text.Json.JsonSerializer.Serialize(runJson, JsonOpts.Value), System.Text.Encoding.UTF8);

// Build series/index.json
var index = new SeriesIndexJson
{
	SchemaVersion = 1,
	Grid = new IndexGridJson { Bins = grid.Bins, BinMinutes = grid.BinMinutes, Timezone = "UTC" },
	Series = seriesMetas,
	Formats = new FormatsJson { GoldTable = new GoldTableJson { Path = "gold/node_time_bin.parquet", Dimensions = new[]{"time_bin","component_id","class"}, Measures = new[]{"arrivals","served","errors"} } }
};
Directory.CreateDirectory(seriesDir);
File.WriteAllText(Path.Combine(seriesDir, "index.json"), System.Text.Json.JsonSerializer.Serialize(index, JsonOpts.Value), System.Text.Encoding.UTF8);

// Build manifest.json
var finalSeed = rngSeed ?? Random.Shared.Next(0, int.MaxValue); // use provided seed or generate random
var manifest = new ManifestJson
{
	SchemaVersion = 1,
	ScenarioHash = runJson.ScenarioHash,
	ModelHash = runJson.ModelHash,
	Rng = new RngJson { Kind = "pcg32", Seed = finalSeed },
	SeriesHashes = seriesHashes,
	EventCount = 0,
	CreatedUtc = DateTime.UtcNow.ToString("o")
};
File.WriteAllText(Path.Combine(runDir, "manifest.json"), System.Text.Json.JsonSerializer.Serialize(manifest, JsonOpts.Value), System.Text.Encoding.UTF8);

// Validate generated artifacts against JSON Schema
ValidateArtifacts(runDir, verbose);

if (verbose) Console.WriteLine($"  RNG seed: {finalSeed} ({(rngSeed.HasValue ? "provided" : "generated")})");

Console.WriteLine($"Wrote artifacts to {runDir}");
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
	Console.WriteLine("  --out <dir>             Output directory (default: out/run)");
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

static void ValidateArtifacts(string runDir, bool verbose)
{
	try
	{
		var schemasDir = Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas");
		if (!Directory.Exists(schemasDir))
		{
			if (verbose) Console.WriteLine("Schema validation skipped: docs/schemas/ not found");
			return;
		}

		// Load schemas
		var runSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(schemasDir, "run.schema.json")));
		var manifestSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(schemasDir, "manifest.schema.json")));
		var indexSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(schemasDir, "series-index.schema.json")));

		// Validate run.json
		var runJsonDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "run.json")));
		var runResult = runSchema.Evaluate(runJsonDoc.RootElement);
		if (!runResult.IsValid)
		{
			Console.WriteLine("❌ run.json schema validation failed:");
			foreach (var error in runResult.Details.Where(d => d.Errors != null && d.Errors.Count > 0))
				Console.WriteLine($"  - {error.InstanceLocation}: {string.Join(", ", error.Errors!.Select(e => e.Value))}");
		}
		else if (verbose) Console.WriteLine("✅ run.json schema validation passed");

		// Validate manifest.json
		var manifestJsonDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "manifest.json")));
		var manifestResult = manifestSchema.Evaluate(manifestJsonDoc.RootElement);
		if (!manifestResult.IsValid)
		{
			Console.WriteLine("❌ manifest.json schema validation failed:");
			foreach (var error in manifestResult.Details.Where(d => d.Errors != null && d.Errors.Count > 0))
				Console.WriteLine($"  - {error.InstanceLocation}: {string.Join(", ", error.Errors!.Select(e => e.Value))}");
		}
		else if (verbose) Console.WriteLine("✅ manifest.json schema validation passed");

		// Validate series/index.json
		var indexPath = Path.Combine(runDir, "series", "index.json");
		if (File.Exists(indexPath))
		{
			var indexJsonDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
			var indexResult = indexSchema.Evaluate(indexJsonDoc.RootElement);
			if (!indexResult.IsValid)
			{
				Console.WriteLine("❌ series/index.json schema validation failed:");
				foreach (var error in indexResult.Details.Where(d => d.Errors != null && d.Errors.Count > 0))
					Console.WriteLine($"  - {error.InstanceLocation}: {string.Join(", ", error.Errors!.Select(e => e.Value))}");
			}
			else if (verbose) Console.WriteLine("✅ series/index.json schema validation passed");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Schema validation error: {ex.Message}");
	}
}

	// Internal artifact DTOs (M1 minimal set)
	static string ComputeScenarioHash(string yamlText)
	{
		// 1. Normalize line endings already done prior. Trim trailing whitespace lines & collapse blank runs.
		var lines = yamlText.Split('\n');
		var sbClean = new System.Text.StringBuilder();
		bool lastBlank = false;
		foreach (var raw in lines)
		{
			var trimmedEnd = raw.TrimEnd();
			bool isBlank = trimmedEnd.Length == 0;
			if (isBlank)
			{
				if (lastBlank) continue; // collapse multiple blanks
				lastBlank = true;
			}
			else lastBlank = false;
			sbClean.Append(trimmedEnd).Append('\n');
		}
		var normalized = sbClean.ToString();
		// 2. Parse YAML and produce key-order-insensitive canonical string
		var stream = new YamlStream();
		using (var reader = new System.IO.StringReader(normalized)) stream.Load(reader);
		if (stream.Documents.Count == 0) return "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(""))).ToLowerInvariant();
		var root = stream.Documents[0].RootNode;
		var sb = new System.Text.StringBuilder();
		void WriteNode(YamlNode node)
		{
			switch (node)
			{
				case YamlMappingNode map:
					// sort keys lexicographically (ordinal)
					var children = map.Children
						.OrderBy(k => (k.Key as YamlScalarNode)?.Value, StringComparer.Ordinal);
					bool first = true;
					sb.Append('{');
					foreach (var kv in children)
					{
						if (!first) sb.Append(',');
						first = false;
						var key = (kv.Key as YamlScalarNode)?.Value ?? "";
						sb.Append('"').Append(Escape(key.Trim())).Append('"').Append(':');
						WriteNode(kv.Value);
					}
					sb.Append('}');
					break;
				case YamlSequenceNode seq:
					sb.Append('[');
					for (int i = 0; i < seq.Children.Count; i++)
					{
						if (i > 0) sb.Append(',');
						WriteNode(seq.Children[i]);
					}
					sb.Append(']');
					break;
				case YamlScalarNode scalar:
					var v = scalar.Value ?? string.Empty;
					// normalize numeric formatting if parseable
					if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
					{
						sb.Append(d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
					}
					else
					{
						sb.Append('"').Append(Escape(v.Trim())).Append('"');
					}
					break;
			}
		}
		static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
		WriteNode(root);
		var canonical = sb.ToString();
		var bytes = System.Text.Encoding.UTF8.GetBytes(canonical);
		var hash = System.Security.Cryptography.SHA256.HashData(bytes);
		return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
	}
	file static class JsonOpts
	{
		public static readonly System.Text.Json.JsonSerializerOptions Value = new(System.Text.Json.JsonSerializerDefaults.Web)
		{
			WriteIndented = true
		};
	}
	file sealed record RunJson
	{
		public int SchemaVersion { get; set; }
		public string RunId { get; set; } = "";
		public string EngineVersion { get; set; } = "";
		public string Source { get; set; } = "engine";
		public GridJson Grid { get; set; } = new();
		public string? ModelHash { get; set; }
		public string ScenarioHash { get; set; } = "";
		public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
		public string[] Warnings { get; set; } = Array.Empty<string>();
		public List<RunSeriesEntry> Series { get; set; } = new();
		public EventsJson Events { get; set; } = new();
	}
	file sealed record GridJson { public int Bins { get; set; } public int BinMinutes { get; set; } public string Timezone { get; set; } = "UTC"; public string Align { get; set; } = "left"; }
	file sealed record RunSeriesEntry { public string Id { get; set; } = ""; public string Path { get; set; } = ""; public string Unit { get; set; } = ""; }
	file sealed record EventsJson { public int SchemaVersion { get; set; } public string[] FieldsReserved { get; set; } = Array.Empty<string>(); }
	file sealed record ManifestJson { public int SchemaVersion { get; set; } public string ScenarioHash { get; set; } = ""; public RngJson Rng { get; set; } = new(); public Dictionary<string,string> SeriesHashes { get; set; } = new(); public int EventCount { get; set; } public string CreatedUtc { get; set; } = ""; public string? ModelHash { get; set; } }
	file sealed record RngJson { public string Kind { get; set; } = "pcg32"; public int Seed { get; set; } }
	file sealed record SeriesIndexJson { public int SchemaVersion { get; set; } public IndexGridJson Grid { get; set; } = new(); public List<SeriesMeta> Series { get; set; } = new(); public FormatsJson Formats { get; set; } = new(); }
	file sealed record IndexGridJson { public int Bins { get; set; } public int BinMinutes { get; set; } public string Timezone { get; set; } = "UTC"; }
	file sealed record SeriesMeta { public string Id { get; set; } = ""; public string Kind { get; set; } = "flow"; public string Path { get; set; } = ""; public string Unit { get; set; } = ""; public string ComponentId { get; set; } = ""; public string Class { get; set; } = "DEFAULT"; public int Points { get; set; } public string Hash { get; set; } = ""; }
	file sealed record FormatsJson { public GoldTableJson GoldTable { get; set; } = new(); }
	file sealed record GoldTableJson { public string Path { get; set; } = "gold/node_time_bin.parquet"; public string[] Dimensions { get; set; } = Array.Empty<string>(); public string[] Measures { get; set; } = Array.Empty<string>(); }

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
