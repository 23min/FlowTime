using System.Collections.Concurrent;

namespace FlowTime.UI.Services;

public interface IRunDiscoveryService
{
    Task<RunDiscoveryResult> LoadRunsAsync(CancellationToken cancellationToken = default);
}

public sealed class RunDiscoveryService : IRunDiscoveryService
{
    private const int DefaultPageSize = 100;
    private const int MaxConcurrentRequests = 4;

    private readonly IFlowTimeApiClient apiClient;
    private readonly ILogger<RunDiscoveryService> logger;

    public RunDiscoveryService(IFlowTimeApiClient apiClient, ILogger<RunDiscoveryService> logger)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RunDiscoveryResult> LoadRunsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summariesResult = await apiClient.GetRunSummariesAsync(pageSize: DefaultPageSize, ct: cancellationToken).ConfigureAwait(false);
        if (!summariesResult.Success || summariesResult.Value is null)
        {
            var error = summariesResult.Error ?? "Failed to retrieve run summaries.";
            logger.LogWarning("Run discovery failed: {StatusCode} {Error}", summariesResult.StatusCode, error);
            return RunDiscoveryResult.Failure(error, summariesResult.StatusCode);
        }

        var summaries = summariesResult.Value.Items ?? Array.Empty<RunSummaryDto>();
        if (summaries.Count == 0)
        {
            logger.LogInformation("Run discovery completed — no runs found.");
        return RunDiscoveryResult.CreateSuccess(Array.Empty<RunListEntry>(), Array.Empty<RunDiagnostic>(), summariesResult.Value.TotalCount);
        }

        var runs = new ConcurrentBag<RunListEntry>();
        var diagnostics = new ConcurrentBag<RunDiagnostic>();

        using var semaphore = new SemaphoreSlim(MaxConcurrentRequests);
        var tasks = summaries.Select(summary => ProcessRunAsync(summary, runs, diagnostics, semaphore, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var orderedRuns = runs
            .OrderByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var orderedDiagnostics = diagnostics
            .OrderBy(d => d.RunId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation("Run discovery completed — {RunCount} runs, {DiagnosticCount} diagnostics.", orderedRuns.Length, orderedDiagnostics.Length);
        return RunDiscoveryResult.CreateSuccess(orderedRuns, orderedDiagnostics, summariesResult.Value.TotalCount);
    }

    private async Task ProcessRunAsync(
        RunSummaryDto summary,
        ConcurrentBag<RunListEntry> runs,
        ConcurrentBag<RunDiagnostic> diagnostics,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detailTask = apiClient.GetRunAsync(summary.RunId, cancellationToken);
            var indexTask = apiClient.GetRunIndexAsync(summary.RunId, cancellationToken);

            var detailResult = await detailTask.ConfigureAwait(false);
            var indexResult = await indexTask.ConfigureAwait(false);

            var warnings = ExtractWarnings(detailResult);
            var firstWarning = warnings.FirstOrDefault()?.Message;

            bool hasManifest = detailResult.Success && detailResult.Value?.Metadata?.Storage?.MetadataPath is { Length: > 0 };
            bool hasModel = detailResult.Success && detailResult.Value?.Metadata?.Storage?.ModelPath is { Length: > 0 };
            bool canReplay = detailResult.Value?.CanReplay ?? false;

            var gridSummary = indexResult.Success && indexResult.Value is not null
                ? BuildGridSummary(indexResult.Value)
                : GridSummary.Missing;

            bool hasIndex = indexResult.Success && indexResult.Value is not null;
            bool hasSeries = hasIndex && indexResult.Value!.Series.Count > 0;

            if (!detailResult.Success)
            {
                diagnostics.Add(new RunDiagnostic(summary.RunId, detailResult.Error ?? "Run metadata unavailable."));
            }

            if (!hasIndex)
            {
                diagnostics.Add(new RunDiagnostic(summary.RunId, indexResult.Error ?? "Series index is missing."));
            }

            if (!hasManifest)
            {
                diagnostics.Add(new RunDiagnostic(summary.RunId, "manifest.json not reported by API; run may be incomplete."));
            }

            var entry = new RunListEntry(
                RunId: summary.RunId,
                TemplateId: summary.TemplateId,
                TemplateTitle: detailResult.Value?.Metadata?.TemplateTitle ?? summary.TemplateTitle,
                TemplateVersion: detailResult.Value?.Metadata?.TemplateVersion ?? summary.TemplateVersion,
                Source: summary.Mode,
                CreatedUtc: summary.CreatedUtc,
                WarningCount: summary.WarningCount,
                FirstWarningMessage: firstWarning,
                Warnings: warnings,
                Grid: gridSummary,
                Presence: new ArtifactPresence(hasModel, hasManifest, hasIndex, hasSeries),
                CanReplay: canReplay,
                CanOpen: hasIndex && hasManifest);

            runs.Add(entry);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new RunDiagnostic(summary.RunId, $"Failed to process run: {ex.Message}"));
            logger.LogError(ex, "Failed to process run {RunId}", summary.RunId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static IReadOnlyList<RunWarningInfo> ExtractWarnings(ApiCallResult<RunCreateResponseDto> detailResult)
    {
        if (!detailResult.Success || detailResult.Value?.Warnings is null || detailResult.Value.Warnings.Count == 0)
        {
            return Array.Empty<RunWarningInfo>();
        }

        var buffer = new RunWarningInfo[detailResult.Value.Warnings.Count];
        for (var i = 0; i < detailResult.Value.Warnings.Count; i++)
        {
            var warning = detailResult.Value.Warnings[i];
            buffer[i] = new RunWarningInfo(
                Code: warning.Code,
                Message: warning.Message,
                Severity: warning.Severity ?? "warning",
                NodeId: warning.NodeId);
        }

        return buffer;
    }

    private static GridSummary BuildGridSummary(SeriesIndex index)
    {
        if (index.Grid is null)
        {
            return GridSummary.Missing;
        }

        try
        {
            return new GridSummary(index.Grid.Bins, index.Grid.BinMinutes);
        }
        catch
        {
            return GridSummary.Missing;
        }
    }
}

public sealed record RunDiscoveryResult(
    bool Success,
    string? ErrorMessage,
    int StatusCode,
    IReadOnlyList<RunListEntry> Runs,
    IReadOnlyList<RunDiagnostic> Diagnostics,
    int TotalCount)
{
    public static RunDiscoveryResult CreateSuccess(IReadOnlyList<RunListEntry> runs, IReadOnlyList<RunDiagnostic> diagnostics, int totalCount) =>
        new(true, null, 200, runs, diagnostics, totalCount);

    public static RunDiscoveryResult Failure(string errorMessage, int statusCode) =>
        new(false, errorMessage, statusCode, Array.Empty<RunListEntry>(), Array.Empty<RunDiagnostic>(), 0);
}

public sealed record RunListEntry(
    string RunId,
    string TemplateId,
    string? TemplateTitle,
    string? TemplateVersion,
    string Source,
    DateTimeOffset? CreatedUtc,
    int WarningCount,
    string? FirstWarningMessage,
    IReadOnlyList<RunWarningInfo> Warnings,
    GridSummary Grid,
    ArtifactPresence Presence,
    bool CanReplay,
    bool CanOpen);

public sealed record RunWarningInfo(string Code, string Message, string Severity, string? NodeId);

public sealed record GridSummary(int Bins, int BinMinutes)
{
    public static GridSummary Missing { get; } = new(0, 0);
    public bool IsAvailable => Bins > 0 && BinMinutes > 0;
}

public sealed record ArtifactPresence(bool HasModel, bool HasManifest, bool HasIndex, bool HasSeries);

public sealed record RunDiagnostic(string RunId, string Message);
