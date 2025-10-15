using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts.Services;
using FlowTime.Core.Models;
using FlowTime.Generator.Models;

namespace FlowTime.Generator.Capture;

/// <summary>
/// Aggregates canonical run artifacts and maps them to telemetry capture descriptors.
/// </summary>
public sealed class RunArtifactReader
{
    private static readonly (string Suffix, TelemetryMetricKind Metric)[] fallbackSuffixes =
    [
        ("_arrivals", TelemetryMetricKind.Arrivals),
        ("_served", TelemetryMetricKind.Served),
        ("_errors", TelemetryMetricKind.Errors),
        ("_queue_depth", TelemetryMetricKind.QueueDepth),
        ("_queue", TelemetryMetricKind.QueueDepth),
        ("_external_demand", TelemetryMetricKind.ExternalDemand),
        ("_capacity", TelemetryMetricKind.Capacity)
    ];

    private readonly ISeriesReader seriesReader;

    public RunArtifactReader(ISeriesReader? seriesReader = null)
    {
        this.seriesReader = seriesReader ?? new FileSeriesReader();
    }

    public async Task<RunCaptureContext> ReadAsync(string runDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runDirectory))
        {
            throw new ArgumentException("Run directory must be provided.", nameof(runDirectory));
        }

        var absoluteRunDir = Path.GetFullPath(runDirectory);
        if (!Directory.Exists(absoluteRunDir))
        {
            throw new DirectoryNotFoundException($"Run directory '{absoluteRunDir}' was not found.");
        }

        var adapter = new RunArtifactAdapter(seriesReader, absoluteRunDir);
        var manifest = await adapter.GetManifestAsync();
        var index = await adapter.GetIndexAsync();
        var specPath = Path.Combine(absoluteRunDir, "spec.yaml");
        if (!File.Exists(specPath))
        {
            throw new FileNotFoundException($"Canonical spec.yaml not found in run directory '{absoluteRunDir}'.", specPath);
        }

        var specYaml = await File.ReadAllTextAsync(specPath, cancellationToken).ConfigureAwait(false);
        var modelDefinition = ModelService.ParseAndConvert(specYaml);
        var bindings = BuildSeriesBindings(modelDefinition, index);

        return new RunCaptureContext(
            absoluteRunDir,
            manifest,
            index,
            modelDefinition,
            bindings);
    }

    private static IReadOnlyList<TelemetrySeriesBinding> BuildSeriesBindings(ModelDefinition model, SeriesIndex index)
    {
        var result = new List<TelemetrySeriesBinding>();

        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in model.Outputs)
        {
            if (string.IsNullOrWhiteSpace(output.Series))
            {
                continue;
            }

            var alias = string.IsNullOrWhiteSpace(output.As)
                ? $"{output.Series}.csv"
                : output.As.Trim();

            aliasMap[output.Series.Trim()] = EnsureCsvExtension(alias);
        }

        var seriesByNodeId = new Dictionary<string, SeriesMetadata>(StringComparer.OrdinalIgnoreCase);
        var seriesCollection = index.Series ?? Array.Empty<SeriesMetadata>();
        foreach (var series in seriesCollection)
        {
            if (string.IsNullOrWhiteSpace(series.Id))
            {
                continue;
            }

            var nodeId = ExtractNodeId(series.Id);
            if (!seriesByNodeId.ContainsKey(nodeId))
            {
                seriesByNodeId[nodeId] = series;
            }
        }

        if (model.Topology?.Nodes is { Count: > 0 } nodes)
        {
            foreach (var node in nodes)
            {
                var semantics = node.Semantics;
                if (!string.IsNullOrWhiteSpace(semantics.Arrivals))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.Arrivals, semantics.Arrivals);
                }

                if (!string.IsNullOrWhiteSpace(semantics.Served))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.Served, semantics.Served);
                }

                if (!string.IsNullOrWhiteSpace(semantics.Errors))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.Errors, semantics.Errors);
                }

                if (!string.IsNullOrWhiteSpace(semantics.ExternalDemand))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.ExternalDemand, semantics.ExternalDemand);
                }

                if (!string.IsNullOrWhiteSpace(semantics.QueueDepth))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.QueueDepth, semantics.QueueDepth);
                }

                if (!string.IsNullOrWhiteSpace(semantics.Capacity))
                {
                    TryAddBinding(result, node.Id, TelemetryMetricKind.Capacity, semantics.Capacity);
                }
            }
        }

        if (result.Count == 0)
        {
            result.AddRange(BuildFallbackBindings(model, seriesByNodeId, aliasMap));
        }

        return result;

        void TryAddBinding(List<TelemetrySeriesBinding> bindings, string nodeId, TelemetryMetricKind metric, string rawSeriesId)
        {
            var seriesNodeId = rawSeriesId.Trim();
            if (!seriesByNodeId.TryGetValue(seriesNodeId, out var seriesMeta))
            {
                return;
            }

            var alias = aliasMap.TryGetValue(seriesNodeId, out var mappedAlias)
                ? mappedAlias
                : BuildDefaultFileName(nodeId, metric);

            bindings.Add(new TelemetrySeriesBinding(
                nodeId,
                metric,
                seriesNodeId,
                alias,
                seriesMeta.Id,
                seriesMeta.Path,
                seriesMeta.Points));
        }
    }

    private static IEnumerable<TelemetrySeriesBinding> BuildFallbackBindings(
        ModelDefinition model,
        IReadOnlyDictionary<string, SeriesMetadata> seriesByNodeId,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        foreach (var output in model.Outputs)
        {
            if (string.IsNullOrWhiteSpace(output.Series))
            {
                continue;
            }

            var seriesId = output.Series.Trim();
            if (!seriesByNodeId.TryGetValue(seriesId, out var meta))
            {
                continue;
            }

            var alias = aliasMap.TryGetValue(seriesId, out var mappedAlias)
                ? mappedAlias
                : EnsureCsvExtension(seriesId);

            if (!TryInferMetric(alias, out var metric, out var nodeName))
            {
                continue;
            }

            yield return new TelemetrySeriesBinding(
                nodeName,
                metric,
                seriesId,
                alias,
                meta.Id,
                meta.Path,
                meta.Points);
        }
    }

    private static string ExtractNodeId(string seriesId)
    {
        var separatorIndex = seriesId.IndexOf('@');
        return separatorIndex < 0 ? seriesId : seriesId[..separatorIndex];
    }

    private static bool TryInferMetric(string targetFileName, out TelemetryMetricKind metric, out string nodeName)
    {
        var name = Path.GetFileNameWithoutExtension(targetFileName) ?? string.Empty;
        var lowered = name.ToLowerInvariant();

        foreach (var (suffix, candidate) in fallbackSuffixes)
        {
            if (lowered.EndsWith(suffix, StringComparison.Ordinal))
            {
                var trimmed = name[..^suffix.Length];
                trimmed = trimmed.TrimEnd('_');
                if (trimmed.Length == 0)
                {
                    continue;
                }

                metric = candidate;
                nodeName = trimmed;
                return true;
            }
        }

        metric = default;
        nodeName = string.Empty;
        return false;
    }

    private static string EnsureCsvExtension(string alias)
    {
        if (alias.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return alias;
        }

        return $"{alias}.csv";
    }

    private static string BuildDefaultFileName(string nodeId, TelemetryMetricKind metric)
    {
        var sanitizedNode = string.IsNullOrWhiteSpace(nodeId) ? "node" : nodeId.Replace(' ', '_');
        return $"{sanitizedNode}_{metric.ToString().ToLowerInvariant()}.csv";
    }
}

public sealed record RunCaptureContext(
    string RunDirectory,
    RunManifest Manifest,
    SeriesIndex SeriesIndex,
    ModelDefinition Model,
    IReadOnlyList<TelemetrySeriesBinding> SeriesBindings);

public sealed record TelemetrySeriesBinding(
    string NodeId,
    TelemetryMetricKind Metric,
    string SeriesNodeId,
    string TargetFileName,
    string SourceSeriesId,
    string SourcePath,
    int Points);
