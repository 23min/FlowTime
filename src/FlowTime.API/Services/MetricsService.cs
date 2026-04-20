using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.Storage;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.DataSources;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

public sealed class MetricsService
{
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
        string runDirectory;
        try
        {
            runDirectory = RunPathResolver.GetSafeRunDirectory(runsRoot, runId);
        }
        catch (ArgumentException)
        {
            throw new MetricsQueryException(404, $"Run '{runId}' not found.");
        }

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
            resolvedStart = 0;
        }
        else
        {
            resolvedStart = 0;
            resolvedEnd = totalBins - 1;
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
        var binCount = endBin - startBin + 1;

        try
        {
            var window = await stateQueryService.GetStateWindowAsync(
                runId,
                startBin,
                endBin,
                GraphQueryMode.Operational,
                null,
                cancellationToken).ConfigureAwait(false);
            return ConvertFromStateWindow(window, manifest, binCount);
        }
        catch (StateQueryException ex)
        {
            logger.LogWarning(ex, "State window resolution failed for run {RunId}", runId);
            throw new MetricsQueryException(ex.StatusCode, ex.Message);
        }
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
                node.Category,
                node.Analytical,
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

    private static List<ServiceMetrics> ComputeServiceMetrics(MetricsResolution resolution)
    {
        var services = new List<ServiceMetrics>(resolution.Nodes.Count);

        foreach (var node in resolution.Nodes)
        {
            if (!node.Analytical.HasServiceSemantics && !node.Analytical.HasQueueSemantics)
            {
                continue;
            }

            if (node.Arrivals is null || node.Served is null)
            {
                continue;
            }

            var binCount = resolution.BinCount;
            var ratios = new double?[binCount];
            var binsMet = 0;
            var binsEvaluated = 0;

            for (var i = 0; i < binCount; i++)
            {
                var arrivals = node.Arrivals[i];
                var served = node.Served[i];

                if (!arrivals.HasValue || !served.HasValue)
                {
                    ratios[i] = null;
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

            IReadOnlyList<double?> mini = ratios;

            var slaPct = binsEvaluated > 0 ? binsMet / (double)binsEvaluated : 0d;
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
    string Category,
    NodeAnalyticalFacts Analytical,
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
