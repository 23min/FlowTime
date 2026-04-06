using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Core.TimeTravel;

public sealed class TelemetrySourceMetadata
{
    public static TelemetrySourceMetadata Empty { get; } = new(
        Array.Empty<string>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public TelemetrySourceMetadata(IReadOnlyList<string> telemetrySources, IReadOnlyDictionary<string, string> nodeSources)
    {
        TelemetrySources = telemetrySources ?? throw new ArgumentNullException(nameof(telemetrySources));
        NodeSources = nodeSources ?? throw new ArgumentNullException(nameof(nodeSources));
    }

    public IReadOnlyList<string> TelemetrySources { get; }

    public IReadOnlyDictionary<string, string> NodeSources { get; }
}

public static class TelemetrySourceMetadataExtractor
{
    private static readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static TelemetrySourceMetadata Extract(string modelYaml)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
        {
            return TelemetrySourceMetadata.Empty;
        }

        ModelDocument document;
        try
        {
            document = yamlDeserializer.Deserialize<ModelDocument>(modelYaml) ?? new ModelDocument();
        }
        catch
        {
            return TelemetrySourceMetadata.Empty;
        }

        var nodeSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var telemetrySources = new List<string>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (document.Nodes is not null)
        {
            foreach (var node in document.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Source))
                {
                    continue;
                }

                var source = node.Source.Trim();
                if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(node.Id))
                {
                    nodeSources[node.Id.Trim()] = source;
                }

                AddTelemetrySource(source);
            }
        }

        if (document.Topology?.Nodes is not null)
        {
            foreach (var topoNode in document.Topology.Nodes)
            {
                if (topoNode.Semantics is null)
                {
                    continue;
                }

                AddTelemetrySource(topoNode.Semantics.Arrivals);
                AddTelemetrySource(topoNode.Semantics.Served);
                AddTelemetrySource(topoNode.Semantics.Errors);
                AddTelemetrySource(topoNode.Semantics.Attempts);
                AddTelemetrySource(topoNode.Semantics.Failures);
                AddTelemetrySource(topoNode.Semantics.ExhaustedFailures);
                AddTelemetrySource(topoNode.Semantics.RetryEcho);
                AddTelemetrySource(topoNode.Semantics.RetryBudgetRemaining);
                AddTelemetrySource(topoNode.Semantics.ExternalDemand);
                AddTelemetrySource(topoNode.Semantics.Queue);
                AddTelemetrySource(topoNode.Semantics.QueueDepth);
                AddTelemetrySource(topoNode.Semantics.Capacity);
                AddTelemetrySource(topoNode.Semantics.ProcessingTimeMsSum);
                AddTelemetrySource(topoNode.Semantics.ServedCount);
            }
        }

        return telemetrySources.Count == 0 && nodeSources.Count == 0
            ? TelemetrySourceMetadata.Empty
            : new TelemetrySourceMetadata(telemetrySources, nodeSources);

        void AddTelemetrySource(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (!trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (seenSources.Add(trimmed))
            {
                telemetrySources.Add(trimmed);
            }
        }
    }

    private sealed class ModelDocument
    {
        public List<ModelNode> Nodes { get; set; } = new();

        public TopologyDocument? Topology { get; set; }
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
        public TopologyNodeSemanticsDocument? Semantics { get; set; }
    }

    private sealed class TopologyNodeSemanticsDocument
    {
        public string? Arrivals { get; set; }

        public string? Served { get; set; }

        public string? Errors { get; set; }

        public string? Attempts { get; set; }

        public string? Failures { get; set; }

        public string? ExhaustedFailures { get; set; }

        public string? RetryEcho { get; set; }

        public string? RetryBudgetRemaining { get; set; }

        public string? ExternalDemand { get; set; }

        public string? Queue { get; set; }

        public string? QueueDepth { get; set; }

        public string? Capacity { get; set; }

        public string? ProcessingTimeMsSum { get; set; }

        public string? ServedCount { get; set; }
    }
}