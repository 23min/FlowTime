
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Core.TimeTravel;

/// <summary>
/// Reads canonical time-travel model artifacts emitted by FlowTime.Sim and extracts metadata needed by the Engine.
/// </summary>
public sealed class RunManifestReader
{
    private static readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task<RunManifestMetadata> ReadAsync(string modelDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelDirectory))
        {
            throw new ArgumentException("Model directory must be provided.", nameof(modelDirectory));
        }

        var directoryInfo = new DirectoryInfo(modelDirectory);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Model directory '{modelDirectory}' was not found.");
        }

        var modelPath = Path.Combine(modelDirectory, "model.yaml");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Canonical model.yaml is required for time-travel runs.", modelPath);
        }

        var metadataPath = Path.Combine(modelDirectory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            throw new InvalidOperationException($"metadata.json not found alongside model at '{modelDirectory}'.");
        }

        ModelDocument modelDoc;
        string yamlContent;
        await using (var modelStream = File.OpenRead(modelPath))
        using (var reader = new StreamReader(modelStream))
        {
            yamlContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            modelDoc = yamlDeserializer.Deserialize<ModelDocument>(yamlContent) ?? new ModelDocument();
        }

        MetadataDocument metadataDoc;
        await using (var stream = File.OpenRead(metadataPath))
        {
            metadataDoc = await JsonSerializer.DeserializeAsync<MetadataDocument>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"metadata.json at '{metadataPath}' is empty or invalid.");
        }

        var provenancePath = Path.Combine(modelDirectory, "provenance.json");
        var provenanceHash = metadataDoc.ModelHash ?? ComputeModelHashFallback(modelPath, cancellationToken);

        var schemaVersion = metadataDoc.SchemaVersion ?? modelDoc.SchemaVersion;
        if (schemaVersion <= 0)
        {
            throw new InvalidOperationException("Schema version must be present in metadata.json or model.yaml.");
        }

        var templateId = metadataDoc.TemplateId ?? modelDoc.Metadata?.Id
            ?? throw new InvalidOperationException("TemplateId missing from metadata.json and model.yaml metadata block.");

        var templateTitle = metadataDoc.TemplateTitle ?? modelDoc.Metadata?.Title ?? templateId;
        var templateVersion = metadataDoc.TemplateVersion ?? modelDoc.Metadata?.Version ?? "0.0.0";
        var mode = (metadataDoc.Mode ?? modelDoc.Mode ?? modelDoc.Provenance?.Mode ?? "simulation").ToLowerInvariant();

        var telemetrySources = ExtractTelemetrySources(modelDoc, out var nodeSources);
        var fallbackSources = ExtractTelemetrySourcesFromText(yamlContent);
        if (fallbackSources.Count > 0)
        {
            var merged = new HashSet<string>(telemetrySources, StringComparer.OrdinalIgnoreCase);
            foreach (var source in fallbackSources)
            {
                merged.Add(source);
            }

            telemetrySources = merged.Count == 0 ? Array.Empty<string>() : merged.ToArray();
        }

        return new RunManifestMetadata
        {
            TemplateId = templateId,
            TemplateTitle = templateTitle,
            TemplateVersion = templateVersion,
            Mode = mode,
            Schema = new RunSchemaMetadata
            {
                Id = MapSchemaId(schemaVersion),
                Version = MapSchemaVersion(schemaVersion),
                Hash = provenanceHash
            },
            ProvenanceHash = provenanceHash,
            TelemetrySources = telemetrySources,
            NodeSources = nodeSources,
            Storage = new RunStorageDescriptor
            {
                ModelPath = modelPath,
                MetadataPath = metadataPath,
                ProvenancePath = File.Exists(provenancePath) ? provenancePath : null
            }
        };
    }

    private static string ComputeModelHashFallback(string modelPath, CancellationToken cancellationToken)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(modelPath);
        cancellationToken.ThrowIfCancellationRequested();
        var hash = sha.ComputeHash(stream);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static IReadOnlyList<string> ExtractTelemetrySources(ModelDocument document, out IReadOnlyDictionary<string, string> nodeSources)
    {
        if (document.Nodes is null || document.Nodes.Count == 0)
        {
            nodeSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return Array.Empty<string>();
        }

        var sourcesByNode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var telemetry = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Source))
            {
                continue;
            }

            var source = node.Source.Trim();
            if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(node.Id))
            {
                sourcesByNode[node.Id] = source;
                telemetry.Add(source);
            }
        }

        if (document.Topology?.Nodes != null)
        {
            foreach (var topoNode in document.Topology.Nodes)
            {
                if (topoNode.Semantics == null)
                {
                    continue;
                }

                AddIfTelemetry(topoNode.Semantics.Arrivals);
                AddIfTelemetry(topoNode.Semantics.Served);
                AddIfTelemetry(topoNode.Semantics.Errors);
                AddIfTelemetry(topoNode.Semantics.ExternalDemand);
                AddIfTelemetry(topoNode.Semantics.Queue);
                AddIfTelemetry(topoNode.Semantics.QueueDepth);
                AddIfTelemetry(topoNode.Semantics.Capacity);
            }
        }

        nodeSources = sourcesByNode;
        return telemetry.Count == 0 ? Array.Empty<string>() : telemetry.ToArray();

        void AddIfTelemetry(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                telemetry.Add(trimmed);
            }
        }
    }

    private static IReadOnlyList<string> ExtractTelemetrySourcesFromText(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return Array.Empty<string>();
        }

        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(yamlContent, "file:[^\\s\"']+"))
        {
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                sources.Add(match.Value);
            }
        }

        return sources.Count == 0 ? Array.Empty<string>() : sources.ToArray();
    }

    private static string MapSchemaId(int version) => version switch
    {
        1 => "time-travel/v1",
        _ => $"time-travel/v{version}"
    };

    private static string MapSchemaVersion(int version) => version switch
    {
        1 => "1",
        _ => version.ToString(CultureInfo.InvariantCulture)
    };

    private sealed class ModelDocument
    {
        public int SchemaVersion { get; set; }
        public string? Mode { get; set; }
        public ModelMetadata? Metadata { get; set; }
        public ModelProvenance? Provenance { get; set; }
        public List<ModelNode> Nodes { get; set; } = new();
        public TopologyDocument? Topology { get; set; }
    }

    private sealed class ModelMetadata
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Version { get; set; }
    }

    private sealed class ModelProvenance
    {
        public string? Mode { get; set; }
    }

    private sealed class ModelNode
    {
        public string? Id { get; set; }
        public string? Source { get; set; }
    }

    private sealed class TopologyDocument
    {
        public List<TopologyNodeDocument> Nodes { get; set; } = new();
    }

    private sealed class TopologyNodeDocument
    {
        public TopologyNodeSemanticsDocument Semantics { get; set; } = new();
    }

    private sealed class TopologyNodeSemanticsDocument
    {
        public string? Arrivals { get; set; }
        public string? Served { get; set; }
        public string? Errors { get; set; }
        public string? ExternalDemand { get; set; }
        public string? Queue { get; set; }
        public string? QueueDepth { get; set; }
        public string? Capacity { get; set; }
    }

    private sealed class MetadataDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("templateId")]
        public string? TemplateId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("templateTitle")]
        public string? TemplateTitle { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("templateVersion")]
        public string? TemplateVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("modelHash")]
        public string? ModelHash { get; set; }
    }
}

public sealed class RunManifestMetadata
{
    public required string TemplateId { get; init; }
    public required string TemplateTitle { get; init; }
    public required string TemplateVersion { get; init; }
    public required string Mode { get; init; }
    public required RunSchemaMetadata Schema { get; init; }
    public required string ProvenanceHash { get; init; }
    public required RunStorageDescriptor Storage { get; init; }
    public IReadOnlyList<string> TelemetrySources { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> NodeSources { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RunSchemaMetadata
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Hash { get; init; }
}

public sealed class RunStorageDescriptor
{
    public required string ModelPath { get; init; }
    public string? MetadataPath { get; init; }
    public string? ProvenancePath { get; init; }
}
