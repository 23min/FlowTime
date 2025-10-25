using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

internal static class MetricsArtifactWriter
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task TryWriteAsync(
        MetricsService metricsService,
        string runId,
        string runDirectory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await metricsService.GetMetricsAsync(runId, 0, null, cancellationToken).ConfigureAwait(false);
            var payload = JsonSerializer.Serialize(metrics, jsonOptions);
            var path = Path.Combine(runDirectory, "metrics.json");
            await File.WriteAllTextAsync(path, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (MetricsQueryException ex)
        {
            logger.LogWarning("Skipping metrics.json for run {RunId}: {Message}", runId, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write metrics.json for run {RunId}", runId);
        }
    }
}
