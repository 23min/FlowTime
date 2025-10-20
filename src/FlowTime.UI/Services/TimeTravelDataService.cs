using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlowTime.UI.Services;

public interface ITimeTravelDataService
{
    Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetStateAsync(string runId, int binIndex, CancellationToken ct = default);
    Task<ApiCallResult<TimeTravelStateWindowDto>> GetStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default);
    Task<ApiCallResult<SeriesIndex>> GetSeriesIndexAsync(string runId, CancellationToken ct = default);
    Task<ApiCallResult<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default);
}

public sealed class TimeTravelDataService : ITimeTravelDataService
{
    private readonly IFlowTimeApiClient apiClient;
    private readonly ILogger<TimeTravelDataService> logger;

    public TimeTravelDataService(IFlowTimeApiClient apiClient, ILogger<TimeTravelDataService> logger)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetStateAsync(string runId, int binIndex, CancellationToken ct = default)
    {
        var validationError = ValidateRunId(runId);
        if (validationError is not null)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateSnapshotDto>.Fail(400, validationError));
        }

        if (binIndex < 0)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateSnapshotDto>.Fail(400, "binIndex must be zero or greater."));
        }

        return SendAndInspect(runId, "state", () => apiClient.GetRunStateAsync(runId, binIndex, ct));
    }

    public Task<ApiCallResult<TimeTravelStateWindowDto>> GetStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default)
    {
        var validationError = ValidateRunId(runId);
        if (validationError is not null)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(400, validationError));
        }

        if (startBin < 0)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(400, "startBin must be zero or greater."));
        }

        if (endBin < 0)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(400, "endBin must be zero or greater."));
        }

        if (endBin < startBin)
        {
            return Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(400, "endBin must be greater than or equal to startBin."));
        }

        return SendAndInspect(runId, "state_window", () => apiClient.GetRunStateWindowAsync(runId, startBin, endBin, ct));
    }

    public Task<ApiCallResult<SeriesIndex>> GetSeriesIndexAsync(string runId, CancellationToken ct = default)
    {
        var validationError = ValidateRunId(runId);
        if (validationError is not null)
        {
            return Task.FromResult(ApiCallResult<SeriesIndex>.Fail(400, validationError));
        }

        return SendAndInspect(runId, "index", () => apiClient.GetRunIndexAsync(runId, ct));
    }

    public Task<ApiCallResult<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
    {
        var validationError = ValidateRunId(runId) ?? ValidateSeriesId(seriesId);
        if (validationError is not null)
        {
            return Task.FromResult(ApiCallResult<Stream>.Fail(400, validationError));
        }

        return SendAndInspect(runId, $"series:{seriesId}", () => apiClient.GetRunSeriesAsync(runId, seriesId, ct));
    }

    private async Task<ApiCallResult<T>> SendAndInspect<T>(string runId, string operation, Func<Task<ApiCallResult<T>>> action)
    {
        var result = await action().ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Time-travel {Operation} request failed for run {RunId}: status={StatusCode}, error={Error}",
                operation, runId, result.StatusCode, result.Error ?? "unknown");
            return result;
        }

        if (result.Value is null)
        {
            logger.LogWarning("Time-travel {Operation} request returned empty payload for run {RunId}.", operation, runId);
            return ApiCallResult<T>.Fail(result.StatusCode, $"{operation} response was empty.");
        }

        return result;
    }

    private static string? ValidateRunId(string runId) =>
        string.IsNullOrWhiteSpace(runId) ? "runId is required." : null;

    private static string? ValidateSeriesId(string seriesId) =>
        string.IsNullOrWhiteSpace(seriesId) ? "seriesId is required." : null;
}
