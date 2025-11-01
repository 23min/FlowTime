using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.DataSources;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

public sealed class MetricsService
{
    private const int defaultWindowBins = 12;
    private const double slaThreshold = 0.95d;
    private readonly IConfiguration configuration;
    private readonly ILogger<MetricsService> logger;
    private readonly StateQueryService stateQueryService;
    private readonly RunManifestReader manifestReader;

    public MetricsService(
        IConfiguration configuration,
        ILogger<MetricsService> logger,
        StateQueryService stateQueryService,
        RunManifestReader manifestReader)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.stateQueryService = stateQueryService ?? throw new ArgumentNullException(nameof(stateQueryService));
        this.manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
    }

    public async Task<MetricsResponse> GetMetricsAsync(string runId, int? requestedStartBin, int? requestedEndBin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new MetricsQueryException(400, "runId must be provided.");
        }

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var runDirectory = Path.Combine(runsRoot, runId);
        if (!Directory.Exists(runDirectory))
        {
            throw new MetricsQueryException(404, $"Run '{runId}' not found.");
        }

        RunManifest manifest;
        try
        {
            var reader = new FileSeriesReader();
            manifest = await reader.ReadRunInfoAsync(runDirectory);
        }
        catch (FileNotFoundException)
        {
            throw new MetricsQueryException(404, $"run.json missing for run '{runId}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read run manifest for run {RunId}", runId);
            throw new MetricsQueryException(500, $"Failed to read run manifest: {ex.Message}");
        }

        var totalBins = manifest.Grid.Bins;
        if (totalBins <= 0)
        {
            throw new MetricsQueryException(409, $"Run '{runId}' does not define a positive bin count.");
        }

        var (startBin, endBin) = NormalizeRange(requestedStartBin, requestedEndBin, totalBins);

        var resolution = await ResolveMetricsResolutionAsync(runId, manifest, startBin, endBin, cancellationToken);

        var services = ComputeServiceMetrics(resolution);

        if (services.Count == 0)
        {
            logger.LogDebug("No service nodes with arrivals/served semantics found for run {RunId}", runId);
        }

        return new MetricsResponse
        {
            Window = new MetricsWindow
            {
                Start = resolution.WindowStart,
                Timezone = resolution.Timezone
            },
            Grid = new MetricsGrid
            {
                BinMinutes = resolution.BinMinutes,
                Bins = resolution.BinCount
            },
            Services = services
        };
    }

    private static (int StartBin, int EndBin) NormalizeRange(int? startBin, int? endBin, int totalBins)
    {
        int resolvedStart;
        int resolvedEnd;

        if (startBin.HasValue && startBin.Value < 0)
        {
            throw new MetricsQueryException(400, "startBin must be greater than or equal to zero.");
        }

        if (endBin.HasValue && endBin.Value < 0)
        {
            throw new MetricsQueryException(400, "endBin must be greater than or equal to zero.");
        }

        if (startBin.HasValue && startBin.Value >= totalBins)
        {
            throw new MetricsQueryException(400, $"startBin must be less than total bins ({totalBins}).");
        }

        if (endBin.HasValue && endBin.Value >= totalBins)
        {
            throw new MetricsQueryException(400, $"endBin must be less than total bins ({totalBins}).");
        }

        if (startBin.HasValue && endBin.HasValue)
        {
            resolvedStart = startBin.Value;
            resolvedEnd = endBin.Value;
        }
        else if (startBin.HasValue)
        {
            resolvedStart = startBin.Value;
            resolvedEnd = totalBins - 1;
        }
        else if (endBin.HasValue)
        {
            resolvedEnd = endBin.Value;
            var desired = defaultWindowBins - 1;
            resolvedStart = Math.Max(0, resolvedEnd - desired);
        }
        else
        {
            resolvedEnd = totalBins - 1;
            resolvedStart = Math.Max(0, totalBins - defaultWindowBins);
        }

        if (resolvedEnd < resolvedStart)
        {
            throw new MetricsQueryException(400, "endBin must be greater than or equal to startBin.");
        }

        return (resolvedStart, resolvedEnd);
    }

    private async Task<MetricsResolution> ResolveMetricsResolutionAsync(
        string runId,
        RunManifest manifest,
        int startBin,
        int endBin,
        CancellationToken cancellationToken)
    {
        var runDirectory = Path.Combine(Program.ServiceHelpers.RunsRoot(configuration), runId);
        var modelDirectory = Path.Combine(runDirectory, "model");
        RunManifestMetadata manifestMetadata;
        try
        {
            manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read manifest metadata for run {RunId}", runId);
            manifestMetadata = new RunManifestMetadata
            {
                TemplateId = manifest.RunId,
                TemplateTitle = manifest.RunId,
                TemplateVersion = "1.0.0",
                Mode = "simulation",
                Schema = new RunSchemaMetadata { Id = "time-travel/v1", Version = "1", Hash = manifest.ModelHash ?? string.Empty },
                ProvenanceHash = manifest.ModelHash ?? string.Empty,
                Storage = new RunStorageDescriptor { ModelPath = Path.Combine(modelDirectory, "model.yaml") }
            };
        }

        var binCount = endBin - startBin + 1;

        if (!string.Equals(manifestMetadata.Mode, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var window = await stateQueryService.GetStateWindowAsync(
                    runId,
                    startBin,
                    endBin,
                    GraphQueryMode.Operational,
                    cancellationToken).ConfigureAwait(false);
                return ConvertFromStateWindow(window, manifest, binCount);
            }
            catch (StateQueryException ex)
            {
                logger.LogWarning(ex, "State window resolution failed for run {RunId}, falling back to model evaluation", runId);
                if (ex.StatusCode != 404 && !ex.Message.Contains("Unsupported URI scheme", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MetricsQueryException(ex.StatusCode, ex.Message);
                }
            }
        }

        return await ResolveViaModelAsync(runId, manifest, manifestMetadata, startBin, endBin, binCount, cancellationToken).ConfigureAwait(false);
    }

    private MetricsResolution ConvertFromStateWindow(StateWindowResponse window, RunManifest manifest, int binCount)
    {
        var nodes = new List<ResolvedNodeSeries>(window.Nodes.Count);
        foreach (var node in window.Nodes)
        {
            node.Series.TryGetValue("arrivals", out var arrivals);
            node.Series.TryGetValue("served", out var served);
            node.Series.TryGetValue("errors", out var errors);
            node.Series.TryGetValue("queue", out var queue);
            node.Series.TryGetValue("capacity", out var capacity);

            nodes.Add(new ResolvedNodeSeries(
                node.Id,
                node.Kind,
                ClipSeries(arrivals, binCount),
                ClipSeries(served, binCount),
                ClipSeries(errors, binCount),
                ClipSeries(queue, binCount),
                ClipSeries(capacity, binCount)));
        }

        var windowStart = window.TimestampsUtc.FirstOrDefault();

        return new MetricsResolution(
            windowStart,
            manifest.Grid.Timezone,
            manifest.Grid.BinMinutes,
            binCount,
            nodes);
    }

    private static double?[]? ClipSeries(double?[]? source, int binCount)
    {
        if (source is null)
        {
            return null;
        }

        if (source.Length == binCount)
        {
            return source;
        }

        var length = Math.Min(binCount, source.Length);
        var result = new double?[binCount];
        Array.Copy(source, result, length);
        return result;
    }

    private async Task<MetricsResolution> ResolveViaModelAsync(
        string runId,
        RunManifest manifest,
        RunManifestMetadata manifestMetadata,
        int startBin,
        int endBin,
        int binCount,
        CancellationToken cancellationToken)
    {
        var runDirectory = Path.Combine(Program.ServiceHelpers.RunsRoot(configuration), runId);
        var modelPath = Path.Combine(runDirectory, "model", "model.yaml");

        if (!File.Exists(modelPath))
        {
            throw new MetricsQueryException(404, $"Model for run '{runId}' was not found.");
        }

        string modelYaml = await File.ReadAllTextAsync(modelPath, cancellationToken).ConfigureAwait(false);

        ModelDefinition modelDefinition;
        try
        {
            modelDefinition = ModelService.ParseAndConvert(modelYaml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse model for metrics evaluation {RunId}", runId);
            throw new MetricsQueryException(409, $"Model for run '{runId}' could not be parsed: {ex.Message}");
        }

        if (modelDefinition.Topology is null)
        {
            throw new MetricsQueryException(412, $"Run '{runId}' does not include topology information required for metrics.");
        }

        var (grid, graph) = ModelParser.ParseModel(modelDefinition);
        var evaluation = graph.Evaluate(grid);

        var nodes = new List<ResolvedNodeSeries>(modelDefinition.Topology.Nodes.Count);
        foreach (var topologyNode in modelDefinition.Topology.Nodes)
        {
            var semantics = topologyNode.Semantics;
            var arrivals = ResolveSeriesSlice(semantics.Arrivals, evaluation, modelPath, startBin, endBin, grid.Bins);
            var served = ResolveSeriesSlice(semantics.Served, evaluation, modelPath, startBin, endBin, grid.Bins);
            var errors = ResolveSeriesSlice(semantics.Errors, evaluation, modelPath, startBin, endBin, grid.Bins);
            var queue = ResolveSeriesSlice(semantics.QueueDepth, evaluation, modelPath, startBin, endBin, grid.Bins);
            var capacity = ResolveSeriesSlice(semantics.Capacity, evaluation, modelPath, startBin, endBin, grid.Bins);

            nodes.Add(new ResolvedNodeSeries(
                topologyNode.Id,
                topologyNode.Kind ?? "service",
                arrivals,
                served,
                errors,
                queue,
                capacity));
        }

        DateTimeOffset? windowStart = null;
        if (modelDefinition.Grid?.StartTimeUtc is not null &&
            DateTimeOffset.TryParse(modelDefinition.Grid.StartTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedStart))
        {
            windowStart = parsedStart.ToUniversalTime();
        }

        return new MetricsResolution(
            windowStart,
            manifest.Grid.Timezone,
            manifest.Grid.BinMinutes,
            binCount,
            nodes);
    }

    private static double?[]? ResolveSeriesSlice(
        string? semantics,
        IReadOnlyDictionary<NodeId, Series> evaluation,
        string modelPath,
        int startBin,
        int endBin,
        int totalBins)
    {
        if (string.IsNullOrWhiteSpace(semantics))
        {
            return null;
        }

        semantics = semantics.Trim();
        double[]? values = null;

        if (semantics.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var modelDirectory = Path.GetDirectoryName(modelPath);
            if (modelDirectory is null)
            {
                return null;
            }

            try
            {
                var resolvedPath = UriResolver.ResolveFilePath(semantics, modelDirectory);
                values = CsvReader.ReadTimeSeries(resolvedPath, totalBins);
            }
            catch
            {
                return null;
            }
        }
        else
        {
            var nodeId = new NodeId(semantics);
            if (evaluation.TryGetValue(nodeId, out var series))
            {
                values = series.ToArray();
            }
        }

        if (values is null)
        {
            return null;
        }

        return Slice(values, startBin, endBin);
    }

    private static double?[] Slice(double[] source, int startBin, int endBin)
    {
        var length = endBin - startBin + 1;
        var result = new double?[length];
        for (var i = 0; i < length; i++)
        {
            var value = source[startBin + i];
            result[i] = double.IsNaN(value) ? null : value;
        }

        return result;
    }

    private static List<ServiceMetrics> ComputeServiceMetrics(MetricsResolution resolution)
    {
        var services = new List<ServiceMetrics>(resolution.Nodes.Count);

        foreach (var node in resolution.Nodes)
        {
            if (node.Arrivals is null || node.Served is null)
            {
                continue;
            }

            var binCount = resolution.BinCount;
            var ratios = new double[binCount];
            var binsMet = 0;
            var binsEvaluated = 0;

            for (var i = 0; i < binCount; i++)
            {
                var arrivals = node.Arrivals[i];
                var served = node.Served[i];

                if (!arrivals.HasValue || !served.HasValue)
                {
                    ratios[i] = 0d;
                    continue;
                }

                double ratio = arrivals.Value <= 0 ? 1d : served.Value / arrivals.Value;
                ratio = Math.Max(0d, double.IsFinite(ratio) ? ratio : 0d);
                ratio = Math.Min(ratio, 1d);

                ratios[i] = ratio;
                binsEvaluated++;

                if (ratio >= slaThreshold)
                {
                    binsMet++;
                }
            }

            IReadOnlyList<double> mini = ratios;

            var slaPct = binsEvaluated > 0 ? binsMet / (double)binsEvaluated : 1d;
            services.Add(new ServiceMetrics
            {
                Id = node.Id,
                SlaPct = Math.Clamp(slaPct, 0d, 1d),
                BinsMet = binsMet,
                BinsTotal = binsEvaluated,
                Mini = mini
            });
        }

        return services;
    }
}

internal sealed record MetricsResolution(
    DateTimeOffset? WindowStart,
    string? Timezone,
    int BinMinutes,
    int BinCount,
    IReadOnlyList<ResolvedNodeSeries> Nodes);

internal sealed record ResolvedNodeSeries(
    string Id,
    string Kind,
    double?[]? Arrivals,
    double?[]? Served,
    double?[]? Errors,
    double?[]? Queue,
    double?[]? Capacity);

public sealed class MetricsQueryException : Exception
{
    public int StatusCode { get; }

    public MetricsQueryException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
