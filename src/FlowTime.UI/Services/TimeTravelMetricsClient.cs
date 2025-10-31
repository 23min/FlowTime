using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlowTime.UI.Services;

public interface ITimeTravelMetricsClient
{
    Task<ApiCallResult<TimeTravelMetricsResult>> GetMetricsAsync(string runId, CancellationToken ct = default);
}

public sealed record TimeTravelMetricsResult(
    TimeTravelMetricsResponseDto Payload,
    TimeTravelMetricsSource Source,
    TimeTravelMetricsContext Context);

public sealed record TimeTravelMetricsContext(
    string RunId,
    string? TemplateId,
    string? TemplateTitle,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd,
    int BinMinutes,
    int BinCount);

public enum TimeTravelMetricsSource
{
    Api,
    StateWindowFallback
}

public sealed class TimeTravelMetricsClient : ITimeTravelMetricsClient
{
    private const double slaThreshold = 0.95d;
    private readonly IFlowTimeApiClient apiClient;
    private readonly ITimeTravelDataService dataService;
    private readonly ILogger<TimeTravelMetricsClient> logger;

    public TimeTravelMetricsClient(
        IFlowTimeApiClient apiClient,
        ITimeTravelDataService dataService,
        ILogger<TimeTravelMetricsClient> logger)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApiCallResult<TimeTravelMetricsResult>> GetMetricsAsync(string runId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return ApiCallResult<TimeTravelMetricsResult>.Fail(400, "runId is required.");
        }

        try
        {
            var apiResult = await apiClient.GetRunMetricsAsync(runId, ct).ConfigureAwait(false);
            if (apiResult.Success && apiResult.Value is not null)
            {
                var context = await BuildContextAsync(runId, apiResult.Value, fallbackMetadata: null, ct).ConfigureAwait(false);
                return ApiCallResult<TimeTravelMetricsResult>.Ok(
                    new TimeTravelMetricsResult(apiResult.Value, TimeTravelMetricsSource.Api, context),
                    apiResult.StatusCode);
            }

            logger.LogWarning("Metrics endpoint unavailable for run {RunId}: status={Status} message={Message}",
                runId, apiResult.StatusCode, apiResult.Error ?? "unknown");

            var fallbackResult = await ComputeFromStateWindowAsync(runId, ct).ConfigureAwait(false);
            return fallbackResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error resolving metrics for run {RunId}", runId);
            return ApiCallResult<TimeTravelMetricsResult>.Fail(500, ex.Message);
        }
    }

    private async Task<ApiCallResult<TimeTravelMetricsResult>> ComputeFromStateWindowAsync(string runId, CancellationToken ct)
    {
        var indexResult = await apiClient.GetRunIndexAsync(runId, ct).ConfigureAwait(false);
        if (!indexResult.Success || indexResult.Value?.Grid is null)
        {
            var error = indexResult.Error ?? "Series index unavailable.";
            logger.LogWarning("Metrics fallback failed to load index for run {RunId}: {Error}", runId, error);
            return ApiCallResult<TimeTravelMetricsResult>.Fail(indexResult.StatusCode, error);
        }

        var grid = indexResult.Value.Grid;
        int totalBins;
        try
        {
            totalBins = grid.Bins;
            if (totalBins <= 0)
            {
                return ApiCallResult<TimeTravelMetricsResult>.Fail(409, "Run grid did not define any bins.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Metrics fallback failed to interpret grid metadata for run {RunId}", runId);
            return ApiCallResult<TimeTravelMetricsResult>.Fail(409, "Invalid grid metadata.");
        }

        var stateResult = await dataService.GetStateWindowAsync(runId, 0, totalBins - 1, mode: null, ct).ConfigureAwait(false);
        if (!stateResult.Success || stateResult.Value is null)
        {
            var error = stateResult.Error ?? "state_window unavailable.";
            logger.LogWarning("Metrics fallback failed to load state window for run {RunId}: {Error}", runId, error);
            return ApiCallResult<TimeTravelMetricsResult>.Fail(stateResult.StatusCode, error);
        }

        var payload = BuildMetricsResponse(stateResult.Value, grid.BinMinutes, stateResult.Value.Window.BinCount);
        var context = await BuildContextAsync(runId, payload, stateResult.Value.Metadata, ct).ConfigureAwait(false);
        return ApiCallResult<TimeTravelMetricsResult>.Ok(
            new TimeTravelMetricsResult(payload, TimeTravelMetricsSource.StateWindowFallback, context),
            200);
    }

    private static TimeTravelMetricsResponseDto BuildMetricsResponse(TimeTravelStateWindowDto window, int binMinutes, int windowBinCount)
    {
        var services = new List<TimeTravelServiceMetricsDto>(window.Nodes.Count);
        foreach (var node in window.Nodes)
        {
            if (!IsServiceLike(node.Kind))
            {
                continue;
            }

            if (!TryGetSeries(node.Series, "arrivals", out var arrivals) ||
                !TryGetSeries(node.Series, "served", out var served))
            {
                continue;
            }

            var metrics = ComputeServiceMetrics(node.Id, arrivals, served, windowBinCount);
            if (metrics is not null)
            {
                services.Add(metrics);
            }
        }

        var start = window.TimestampsUtc.Count > 0 ? window.TimestampsUtc[0] : (DateTimeOffset?)null;

        return new TimeTravelMetricsResponseDto
        {
            Window = new TimeTravelMetricsWindowDto
            {
                Start = start,
                Timezone = start?.Offset == TimeSpan.Zero ? "UTC" : start?.Offset.ToString()
            },
            Grid = new TimeTravelMetricsGridDto
            {
                BinMinutes = binMinutes,
                Bins = Math.Max(0, windowBinCount)
            },
            Services = services
        };
    }

    private static TimeTravelServiceMetricsDto? ComputeServiceMetrics(string nodeId, double?[] arrivals, double?[] served, int windowBinCount)
    {
        var clampCount = Math.Min(Math.Min(arrivals.Length, served.Length), windowBinCount > 0 ? windowBinCount : int.MaxValue);
        if (clampCount <= 0)
        {
            return null;
        }

        var ratios = new double[clampCount];
        var binsMet = 0;
        var binsEvaluated = 0;

        for (var i = 0; i < clampCount; i++)
        {
            var arrivalsValue = arrivals[i];
            var servedValue = served[i];

            if (!arrivalsValue.HasValue || !servedValue.HasValue)
            {
                ratios[i] = 0d;
                continue;
            }

            var ratio = arrivalsValue.Value <= 0 ? 1d : servedValue.Value / arrivalsValue.Value;
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

        return new TimeTravelServiceMetricsDto
        {
            Id = nodeId,
            SlaPct = Math.Clamp(slaPct, 0d, 1d),
            BinsMet = binsMet,
            BinsTotal = binsEvaluated,
            Mini = mini
        };
    }

    private static bool TryGetSeries(IReadOnlyDictionary<string, double?[]> series, string key, out double?[] values)
    {
        foreach (var entry in series)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                values = entry.Value;
                return true;
            }
        }

        values = Array.Empty<double?>();
        return false;
    }

    private static bool IsServiceLike(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "flow", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<TimeTravelMetricsContext> BuildContextAsync(
        string runId,
        TimeTravelMetricsResponseDto payload,
        TimeTravelStateMetadataDto? fallbackMetadata,
        CancellationToken ct)
    {
        string? templateId = null;
        string? templateTitle = null;

        if (fallbackMetadata is not null)
        {
            templateId = fallbackMetadata.TemplateId;
            templateTitle = fallbackMetadata.TemplateTitle ?? fallbackMetadata.TemplateId;
        }
        else
        {
            var runResult = await apiClient.GetRunAsync(runId, ct).ConfigureAwait(false);
            if (runResult.Success && runResult.Value?.Metadata is not null)
            {
                templateId = runResult.Value.Metadata.TemplateId;
                templateTitle = runResult.Value.Metadata.TemplateTitle ?? runResult.Value.Metadata.TemplateId;
            }
        }

        DateTimeOffset? windowStart = payload.Window.Start;
        DateTimeOffset? windowEnd = null;
        if (windowStart.HasValue && payload.Grid.BinMinutes > 0 && payload.Grid.Bins > 0)
        {
            try
            {
                windowEnd = windowStart.Value.AddMinutes(payload.Grid.BinMinutes * payload.Grid.Bins);
            }
            catch
            {
                windowEnd = null;
            }
        }

        return new TimeTravelMetricsContext(
            runId,
            templateId,
            templateTitle,
            windowStart,
            windowEnd,
            payload.Grid.BinMinutes,
            payload.Grid.Bins);
    }
}
