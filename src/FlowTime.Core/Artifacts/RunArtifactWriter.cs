using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Core;
using FlowTime.Core.Nodes;
using YamlDotNet.Serialization;

#pragma warning disable CS8602 // Dereference of a possibly null reference - suppressed for dynamic access patterns

namespace FlowTime.Core.Artifacts;

public static class RunArtifactWriter
{
    private static readonly IDeserializer metadataDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

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

    private sealed record MetadataContext(
        string? TemplateId,
        string? TemplateTitle,
        string? TemplateVersion,
        int? SchemaVersion,
        string? Mode,
        string? Source,
        string? Generator,
        string? ModelId,
        string? GeneratedAtUtc,
        Dictionary<string, object?>? Parameters);

    private sealed record EngineMetadataDocument
    {
        public int SchemaVersion { get; set; }
        public string TemplateId { get; set; } = "adhoc-model";
        public string TemplateTitle { get; set; } = "Ad Hoc Model";
        public string TemplateVersion { get; set; } = "0.0.0";
        public string Mode { get; set; } = "simulation";
        public string ModelHash { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? Generator { get; set; }
        public string? ModelId { get; set; }
        public string? GeneratedAtUtc { get; set; }
        public string ReceivedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
        public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.Ordinal);
    }

    public static async Task<WriteResult> WriteArtifactsAsync(WriteRequest request)
    {
        var scenarioHash = ComputeScenarioHash(request.SpecText, request.RngSeed, request.StartTimeBias);

        var runId = request.DeterministicRunId
            ? $"run_deterministic_{scenarioHash[7..15]}"
            : $"run_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{Guid.NewGuid().ToString("N")[..8]}";

        var runDir = Path.Combine(request.OutputDirectory, runId);
        var seriesDir = Path.Combine(runDir, "series");
        var modelDir = Path.Combine(runDir, "model");
        var goldDir = Path.Combine(runDir, "gold");

        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(seriesDir);
        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(goldDir);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

        await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), request.SpecText, Encoding.UTF8);
        var canonicalModelPath = Path.Combine(modelDir, "model.yaml");
        await File.WriteAllTextAsync(canonicalModelPath, request.SpecText, Encoding.UTF8);

        var metadataContext = ExtractMetadataContext(request.SpecText, request.ProvenanceJson);
        var modelHash = await ComputeFileHashAsync(canonicalModelPath);
        await WriteMetadataAsync(Path.Combine(modelDir, "metadata.json"), metadataContext, modelHash, jsonOptions);

        if (!string.IsNullOrWhiteSpace(request.ProvenanceJson))
        {
            await File.WriteAllTextAsync(Path.Combine(modelDir, "provenance.json"), request.ProvenanceJson, Encoding.UTF8);
        }

        var seriesMetas = new List<SeriesMeta>();
        var seriesHashes = new Dictionary<string, string>(StringComparer.Ordinal);

        var modelDto = (dynamic)request.Model;
        var resolvedSeries = ResolveSeries(modelDto.Outputs, request.Context);

        foreach (var nodeId in resolvedSeries)
        {
            if (!request.Context.TryGetValue(nodeId, out double[] seriesData))
            {
                continue;
            }

            var measure = nodeId.Value;
            var componentId = nodeId.Value.ToUpperInvariant();
            var seriesId = $"{measure}@{componentId}@DEFAULT";
            var csvName = $"{seriesId}.csv";
            var path = Path.Combine(seriesDir, csvName);

            await using (var writer = new StreamWriter(path, false, Encoding.UTF8, 4096))
            {
                writer.NewLine = "\n";
                await writer.WriteLineAsync("t,value");
                for (var t = 0; t < seriesData.Length; t++)
                {
                    await writer.WriteAsync(t.ToString(CultureInfo.InvariantCulture));
                    await writer.WriteAsync(',');
                    await writer.WriteAsync(seriesData[t].ToString(CultureInfo.InvariantCulture));
                    await writer.WriteAsync('\n');
                }
            }

            var hash = await ComputeFileHashAsync(path);
            seriesHashes[seriesId] = hash;
            seriesMetas.Add(new SeriesMeta
            {
                Id = seriesId,
                Kind = "flow",
                Path = $"series/{csvName}",
                Unit = "entities/bin",
                ComponentId = componentId,
                Class = "DEFAULT",
                Points = seriesData.Length,
                Hash = hash
            });

            if (request.Verbose)
            {
                Console.WriteLine($"  Wrote {csvName} ({seriesData.Length} rows)");
            }
        }

        var gridDto = (dynamic)request.Grid;
        var runJson = new RunJson
        {
            SchemaVersion = 1,
            RunId = runId,
            EngineVersion = "0.1.0",
            Source = "engine",
            Grid = new GridJson
            {
                Bins = gridDto.Bins,
                BinSize = gridDto.BinSize,
                BinUnit = gridDto.BinUnit.ToString().ToLowerInvariant(),
                Timezone = "UTC",
                Align = "left"
            },
            ScenarioHash = scenarioHash,
            ModelHash = modelHash,
            Warnings = Array.Empty<string>(),
            Series = seriesMetas.Select(m => new RunSeriesEntry { Id = m.Id, Path = m.Path, Unit = m.Unit }).ToList()
        };

        await File.WriteAllTextAsync(Path.Combine(runDir, "run.json"), JsonSerializer.Serialize(runJson, jsonOptions), Encoding.UTF8);

        var index = new SeriesIndexJson
        {
            SchemaVersion = 1,
            Grid = new IndexGridJson
            {
                Bins = gridDto.Bins,
                BinSize = gridDto.BinSize,
                BinUnit = gridDto.BinUnit.ToString().ToLowerInvariant(),
                Timezone = "UTC"
            },
            Series = seriesMetas,
            Formats = new FormatsJson
            {
                GoldTable = new GoldTableJson
                {
                    Path = "gold/node_time_bin.parquet",
                    Dimensions = new[] { "time_bin", "component_id", "class" },
                    Measures = new[] { "arrivals", "served", "errors" }
                }
            }
        };

        await File.WriteAllTextAsync(Path.Combine(seriesDir, "index.json"), JsonSerializer.Serialize(index, jsonOptions), Encoding.UTF8);

        var finalSeed = request.RngSeed ?? Random.Shared.Next(0, int.MaxValue);

        ProvenanceRef? provenanceRef = null;
        if (!string.IsNullOrWhiteSpace(request.ProvenanceJson))
        {
            try
            {
                var provenanceDoc = JsonSerializer.Deserialize<JsonElement>(request.ProvenanceJson);
                provenanceRef = new ProvenanceRef
                {
                    HasProvenance = true,
                    ModelId = provenanceDoc.TryGetProperty("modelId", out var modelId) ? modelId.GetString() : null,
                    TemplateId = provenanceDoc.TryGetProperty("templateId", out var templateId) ? templateId.GetString() : null
                };
            }
            catch
            {
                provenanceRef = new ProvenanceRef { HasProvenance = true };
            }
        }

        var manifest = new ManifestJson
        {
            SchemaVersion = 1,
            ScenarioHash = runJson.ScenarioHash,
            ModelHash = modelHash,
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

    private static async Task<string> ComputeFileHashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, CancellationToken.None);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<NodeId> ResolveSeries(object? outputsObj, IReadOnlyDictionary<NodeId, double[]> context)
    {
        var ordered = new List<NodeId>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var contextKeys = context.Keys.Select(k => k.Value).ToArray();
        var sawOutputs = false;

        void AddSeries(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var trimmed = candidate.Trim();
            var nodeId = new NodeId(trimmed);
            if (!context.ContainsKey(nodeId))
            {
                return;
            }

            if (seen.Add(nodeId.Value))
            {
                ordered.Add(nodeId);
            }
        }

        if (outputsObj is System.Collections.IEnumerable enumerable)
        {
            foreach (var output in enumerable)
            {
                sawOutputs = true;
                if (output == null)
                {
                    continue;
                }

                string? seriesValue;
                try
                {
                    seriesValue = ((dynamic)output).Series;
                }
                catch
                {
                    seriesValue = null;
                }

                if (string.IsNullOrWhiteSpace(seriesValue))
                {
                    continue;
                }

                var trimmed = seriesValue.Trim();
                if (trimmed == "*")
                {
                    foreach (var key in contextKeys)
                    {
                        AddSeries(key);
                    }
                    continue;
                }

                if (trimmed.EndsWith("/*", StringComparison.Ordinal))
                {
                    var prefix = trimmed[..^2];
                    foreach (var key in contextKeys)
                    {
                        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            AddSeries(key);
                        }
                    }
                    continue;
                }

                AddSeries(trimmed);
            }
        }

        if (!sawOutputs || ordered.Count == 0)
        {
            foreach (var key in contextKeys)
            {
                AddSeries(key);
            }
        }

        return ordered;
    }

    private static MetadataContext ExtractMetadataContext(string yaml, string? provenanceJson)
    {
        string? templateId = null;
        string? templateTitle = null;
        string? templateVersion = null;
        int? schemaVersion = null;
        string? modeFromYaml = null;
        string? source = null;
        string? generator = null;
        string? modelId = null;
        string? generatedAt = null;
        Dictionary<string, object?>? parameters = null;

        try
        {
            var rootObject = metadataDeserializer.Deserialize<object?>(yaml);
            if (rootObject is Dictionary<object, object?> rawMap)
            {
                var root = NormalizeYamlDictionary(rawMap);
                schemaVersion = GetInt(root, "schemaVersion");

                if (root.TryGetValue("metadata", out var metadataObj) && metadataObj is Dictionary<object, object?> metadataRaw)
                {
                    var metadata = NormalizeYamlDictionary(metadataRaw);
                    templateId ??= GetString(metadata, "id");
                    templateTitle ??= GetString(metadata, "title");
                    templateVersion ??= GetString(metadata, "version");
                }

                if (root.TryGetValue("mode", out var modeValue) && modeValue != null)
                {
                    modeFromYaml = modeValue.ToString();
                }
                else if (root.TryGetValue("provenance", out var provenanceObj) && provenanceObj is Dictionary<object, object?> provRaw)
                {
                    var provenance = NormalizeYamlDictionary(provRaw);
                    modeFromYaml ??= GetString(provenance, "mode");
                }
            }
        }
        catch
        {
            // ignore malformed metadata blocks
        }

        string? modeFromProvenance = null;

        if (!string.IsNullOrWhiteSpace(provenanceJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(provenanceJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("templateId", out var templateIdProp) && string.IsNullOrWhiteSpace(templateId))
                {
                    templateId = templateIdProp.GetString();
                }

                if (root.TryGetProperty("templateTitle", out var templateTitleProp) && string.IsNullOrWhiteSpace(templateTitle))
                {
                    templateTitle = templateTitleProp.GetString();
                }

                if (root.TryGetProperty("templateVersion", out var templateVersionProp) && string.IsNullOrWhiteSpace(templateVersion))
                {
                    templateVersion = templateVersionProp.GetString();
                }

                if (root.TryGetProperty("mode", out var modeProp))
                {
                    modeFromProvenance = modeProp.GetString();
                }

                if (root.TryGetProperty("source", out var sourceProp))
                {
                    source = sourceProp.GetString();
                }

                if (root.TryGetProperty("generator", out var generatorProp))
                {
                    generator = generatorProp.GetString();
                }

                if (root.TryGetProperty("modelId", out var modelIdProp))
                {
                    modelId = modelIdProp.GetString();
                }

                if (root.TryGetProperty("generatedAt", out var generatedAtProp))
                {
                    generatedAt = generatedAtProp.GetString();
                }
                else if (root.TryGetProperty("generatedAtUtc", out var generatedAtUtcProp))
                {
                    generatedAt = generatedAtUtcProp.GetString();
                }

                if (root.TryGetProperty("parameters", out var parametersProp) && parametersProp.ValueKind == JsonValueKind.Object)
                {
                    parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var parameter in parametersProp.EnumerateObject())
                    {
                        parameters[parameter.Name] = JsonElementToObject(parameter.Value);
                    }
                }
            }
            catch
            {
                // ignore malformed provenance
            }
        }

        return new MetadataContext(
            TemplateId: templateId,
            TemplateTitle: templateTitle,
            TemplateVersion: templateVersion,
            SchemaVersion: schemaVersion,
            Mode: modeFromProvenance ?? modeFromYaml,
            Source: source,
            Generator: generator,
            ModelId: modelId,
            GeneratedAtUtc: generatedAt,
            Parameters: parameters
        );
    }

    private static Dictionary<string, object?> NormalizeYamlDictionary(Dictionary<object, object?> raw)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in raw)
        {
            if (kvp.Key is null)
            {
                continue;
            }

            var key = kvp.Key.ToString();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = kvp.Value;
            }
        }

        return result;
    }

    private static string? GetString(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static int? GetInt(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is int i)
        {
            return i;
        }

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.Ordinal),
            _ => element.GetRawText()
        };
    }

    private static async Task WriteMetadataAsync(string metadataPath, MetadataContext metadata, string modelHash, JsonSerializerOptions options)
    {
        var templateId = string.IsNullOrWhiteSpace(metadata.TemplateId) ? "adhoc-model" : metadata.TemplateId!;
        var templateTitle = string.IsNullOrWhiteSpace(metadata.TemplateTitle) ? templateId : metadata.TemplateTitle!;
        var templateVersion = string.IsNullOrWhiteSpace(metadata.TemplateVersion) ? "0.0.0" : metadata.TemplateVersion!;
        var mode = string.IsNullOrWhiteSpace(metadata.Mode) ? "simulation" : metadata.Mode!.ToLowerInvariant();

        var document = new EngineMetadataDocument
        {
            SchemaVersion = metadata.SchemaVersion ?? 1,
            TemplateId = templateId,
            TemplateTitle = templateTitle,
            TemplateVersion = templateVersion,
            Mode = mode,
            ModelHash = modelHash,
            Source = metadata.Source,
            Generator = metadata.Generator,
            ModelId = metadata.ModelId,
            GeneratedAtUtc = metadata.GeneratedAtUtc,
            ReceivedAtUtc = DateTime.UtcNow.ToString("o"),
            Parameters = metadata.Parameters != null
                ? new Dictionary<string, object?>(metadata.Parameters, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal)
        };

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(document, options), Encoding.UTF8);
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

file sealed record GridJson { public int Bins { get; set; } public int BinSize { get; set; } public string BinUnit { get; set; } = "minutes"; public string Timezone { get; set; } = "UTC"; public string Align { get; set; } = "left"; }
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
    [System.Text.Json.Serialization.JsonPropertyName("provenance")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProvenanceRef? Provenance { get; set; }
}
file sealed record RngJson 
{ 
    [System.Text.Json.Serialization.JsonPropertyName("kind")] public string Kind { get; set; } = "pcg32";
    [System.Text.Json.Serialization.JsonPropertyName("seed")] public int Seed { get; set; }
}
file sealed record ProvenanceRef 
{ 
    [System.Text.Json.Serialization.JsonPropertyName("hasProvenance")] public bool HasProvenance { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("modelId")] public string? ModelId { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("templateId")] public string? TemplateId { get; set; }
}
file sealed record SeriesIndexJson { public int SchemaVersion { get; set; } public IndexGridJson Grid { get; set; } = new(); public List<SeriesMeta> Series { get; set; } = new(); public FormatsJson Formats { get; set; } = new(); }
file sealed record IndexGridJson { public int Bins { get; set; } public int BinSize { get; set; } public string BinUnit { get; set; } = "minutes"; public string Timezone { get; set; } = "UTC"; }
file sealed record SeriesMeta { public string Id { get; set; } = ""; public string Kind { get; set; } = "flow"; public string Path { get; set; } = ""; public string Unit { get; set; } = ""; public string ComponentId { get; set; } = ""; public string Class { get; set; } = "DEFAULT"; public int Points { get; set; } public string Hash { get; set; } = ""; }
file sealed record FormatsJson { public GoldTableJson GoldTable { get; set; } = new(); }
file sealed record GoldTableJson { public string Path { get; set; } = "gold/node_time_bin.parquet"; public string[] Dimensions { get; set; } = Array.Empty<string>(); public string[] Measures { get; set; } = Array.Empty<string>(); }
