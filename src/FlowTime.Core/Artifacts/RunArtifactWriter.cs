using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Core;

#pragma warning disable CS8602 // Dereference of a possibly null reference - suppressed for dynamic access patterns

namespace FlowTime.Core.Artifacts;

public static class RunArtifactWriter
{
    public record WriteRequest
    {
        public required object Model { get; init; }
        public required object Grid { get; init; }
        public required IReadOnlyDictionary<NodeId, double[]> Context { get; init; }
        public required string SpecText { get; init; }
        public int? RngSeed { get; init; }
        public double? StartTimeBias { get; init; }
        public bool DeterministicRunId { get; init; }
        public required string OutputDirectory { get; init; }
        public bool Verbose { get; init; }
        public string? ProvenanceJson { get; init; } // Optional provenance metadata as JSON string
    }
    
    public record WriteResult
    {
        public required string RunDirectory { get; init; }
        public required string RunId { get; init; }
        public required int FinalSeed { get; init; }
        public required string ScenarioHash { get; init; }
    }

    public static async Task<WriteResult> WriteArtifactsAsync(WriteRequest request)
    {
        var scenarioHash = ComputeScenarioHash(request.SpecText, request.RngSeed, request.StartTimeBias);
        
        // M1 artifact layout: runs/<runId>/...
        var runId = request.DeterministicRunId ? 
            $"run_deterministic_{scenarioHash[7..15]}" : // use first 8 chars of hash for deterministic case
            $"run_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{Guid.NewGuid().ToString("N")[..8]}";
        
        var runDir = Path.Combine(request.OutputDirectory, runId);
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(seriesDir);
        Directory.CreateDirectory(Path.Combine(runDir, "gold")); // placeholder

        await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), request.SpecText, Encoding.UTF8);

        // Write provenance.json if provided
        if (!string.IsNullOrWhiteSpace(request.ProvenanceJson))
        {
            await File.WriteAllTextAsync(Path.Combine(runDir, "provenance.json"), request.ProvenanceJson, Encoding.UTF8);
        }

        var seriesMetas = new List<SeriesMeta>();
        var seriesHashes = new Dictionary<string, string>();

        // Write per-series CSVs 
        var modelDto = (dynamic)request.Model;
        var outputs = modelDto.Outputs;
        
        // If no explicit outputs defined, create outputs for all series in context
        if (outputs == null || (outputs is System.Collections.IEnumerable enumerable && !enumerable.Cast<object>().Any()))
        {
            var allOutputs = new List<dynamic>();
            foreach (var nodeId in request.Context.Keys)
            {
                allOutputs.Add(new { Series = nodeId.Value });
            }
            outputs = allOutputs;
        }
        
        foreach (var output in outputs)
        {
            if (output == null) continue; // Skip null outputs
            
            try
            {
                var seriesValue = output.Series;
                if (seriesValue == null) continue; // Skip outputs without Series
                
                var nodeId = new NodeId(seriesValue);
                if (!request.Context.ContainsKey(nodeId))
                {
                    continue; // Skip if series not found in context
                }
                
                var s = request.Context[nodeId];
                var measure = (string)seriesValue; // the measure name (e.g., "served", "arrivals")
            var componentId = nodeId.Value.ToUpperInvariant(); // component ID (e.g., "SERVED")
            var seriesId = $"{measure}@{componentId}@DEFAULT"; // measure@componentId@class format per contracts
            var csvName = seriesId + ".csv";
            var path = Path.Combine(seriesDir, csvName);
            
            await using (var w = new StreamWriter(path, false, Encoding.UTF8, 4096))
            {
                w.NewLine = "\n";
                await w.WriteLineAsync("t,value");
                for (int t = 0; t < s.Length; t++)
                {
                    await w.WriteAsync(t.ToString());
                    await w.WriteAsync(',');
                    await w.WriteAsync(s[t].ToString(CultureInfo.InvariantCulture));
                    await w.WriteAsync('\n');
                }
            }
            
            var bytes = await File.ReadAllBytesAsync(path);
            var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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
            if (request.Verbose) Console.WriteLine($"  Wrote {csvName} ({s.Length} rows)");
            }
            catch (Exception)
            {
                // Skip outputs that cause errors during dynamic access
                continue;
            }
        }

        // Build run.json
        var gridDto = (dynamic)request.Grid;
        var runJson = new RunJson
        {
            SchemaVersion = 1,
            RunId = runId,
            EngineVersion = "0.1.0", // TODO: derive from assembly
            Source = "engine",
            Grid = new GridJson { Bins = gridDto.Bins, BinMinutes = gridDto.BinMinutes, Timezone = "UTC", Align = "left" },
            ScenarioHash = scenarioHash,
            ModelHash = scenarioHash, // engine MAY emit modelHash; using same canonical hash for now
            Warnings = Array.Empty<string>(),
            Series = seriesMetas.Select(m => new RunSeriesEntry { Id = m.Id, Path = m.Path, Unit = m.Unit }).ToList()
        };
        
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        await File.WriteAllTextAsync(Path.Combine(runDir, "run.json"), JsonSerializer.Serialize(runJson, jsonOptions), Encoding.UTF8);

        // Build series/index.json
        var index = new SeriesIndexJson
        {
            SchemaVersion = 1,
            Grid = new IndexGridJson { Bins = gridDto.Bins, BinMinutes = gridDto.BinMinutes, Timezone = "UTC" },
            Series = seriesMetas,
            Formats = new FormatsJson { GoldTable = new GoldTableJson { Path = "gold/node_time_bin.parquet", Dimensions = new[]{"time_bin","component_id","class"}, Measures = new[]{"arrivals","served","errors"} } }
        };
        await File.WriteAllTextAsync(Path.Combine(seriesDir, "index.json"), JsonSerializer.Serialize(index, jsonOptions), Encoding.UTF8);

        // Build manifest.json
        var finalSeed = request.RngSeed ?? Random.Shared.Next(0, int.MaxValue); // use provided seed or generate random
        
        // Extract provenance reference for manifest
        ProvenanceRef? provenanceRef = null;
        if (!string.IsNullOrWhiteSpace(request.ProvenanceJson))
        {
            try
            {
                var provenanceDoc = JsonSerializer.Deserialize<JsonElement>(request.ProvenanceJson);
                provenanceRef = new ProvenanceRef
                {
                    HasProvenance = true,
                    ModelId = provenanceDoc.TryGetProperty("model_id", out var modelId) ? modelId.GetString() : null,
                    TemplateId = provenanceDoc.TryGetProperty("template_id", out var templateId) ? templateId.GetString() : null
                };
            }
            catch
            {
                // If parsing fails, just indicate has_provenance without details
                provenanceRef = new ProvenanceRef { HasProvenance = true };
            }
        }
        
        var manifest = new ManifestJson
        {
            SchemaVersion = 1,
            ScenarioHash = runJson.ScenarioHash,
            ModelHash = runJson.ModelHash,
            Rng = new RngJson { Kind = "pcg32", Seed = finalSeed },
            SeriesHashes = seriesHashes,
            EventCount = 0,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            Provenance = provenanceRef
        };
        await File.WriteAllTextAsync(Path.Combine(runDir, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions), Encoding.UTF8);

        return new WriteResult
        {
            RunDirectory = runDir,
            RunId = runId,
            FinalSeed = finalSeed,
            ScenarioHash = scenarioHash
        };
    }

    private static string ComputeScenarioHash(string modelText, int? seed, double? startTimeBias)
    {
        // Hash over the normalized spec/scenario params for deterministic behavior
        var canonicalInput = $"{modelText.Trim()}\n{(seed?.ToString() ?? "null")}\n{(startTimeBias?.ToString(CultureInfo.InvariantCulture) ?? "null")}";
        var bytes = Encoding.UTF8.GetBytes(canonicalInput);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

// DTOs for JSON artifacts
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
}

file sealed record GridJson { public int Bins { get; set; } public int BinMinutes { get; set; } public string Timezone { get; set; } = "UTC"; public string Align { get; set; } = "left"; }
file sealed record RunSeriesEntry { public string Id { get; set; } = ""; public string Path { get; set; } = ""; public string Unit { get; set; } = ""; }
file sealed record ManifestJson 
{ 
    [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("scenarioHash")] public string ScenarioHash { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("rng")] public RngJson Rng { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("seriesHashes")] public Dictionary<string,string> SeriesHashes { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("eventCount")] public int EventCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("createdUtc")] public string CreatedUtc { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("modelHash")] public string? ModelHash { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("provenance")] public ProvenanceRef? Provenance { get; set; }
}
file sealed record RngJson 
{ 
    [System.Text.Json.Serialization.JsonPropertyName("kind")] public string Kind { get; set; } = "pcg32";
    [System.Text.Json.Serialization.JsonPropertyName("seed")] public int Seed { get; set; }
}
file sealed record ProvenanceRef 
{ 
    [System.Text.Json.Serialization.JsonPropertyName("has_provenance")] public bool HasProvenance { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("model_id")] public string? ModelId { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("template_id")] public string? TemplateId { get; set; }
}
file sealed record SeriesIndexJson { public int SchemaVersion { get; set; } public IndexGridJson Grid { get; set; } = new(); public List<SeriesMeta> Series { get; set; } = new(); public FormatsJson Formats { get; set; } = new(); }
file sealed record IndexGridJson { public int Bins { get; set; } public int BinMinutes { get; set; } public string Timezone { get; set; } = "UTC"; }
file sealed record SeriesMeta { public string Id { get; set; } = ""; public string Kind { get; set; } = "flow"; public string Path { get; set; } = ""; public string Unit { get; set; } = ""; public string ComponentId { get; set; } = ""; public string Class { get; set; } = "DEFAULT"; public int Points { get; set; } public string Hash { get; set; } = ""; }
file sealed record FormatsJson { public GoldTableJson GoldTable { get; set; } = new(); }
file sealed record GoldTableJson { public string Path { get; set; } = "gold/node_time_bin.parquet"; public string[] Dimensions { get; set; } = Array.Empty<string>(); public string[] Measures { get; set; } = Array.Empty<string>(); }
