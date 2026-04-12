using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.TimeMachine.Models;

namespace FlowTime.TimeMachine.Artifacts;

/// <summary>
/// Emits the telemetry manifest describing captured files.
/// </summary>
public static class CaptureManifestWriter
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync(string manifestPath, TelemetryManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var json = JsonSerializer.Serialize(manifest, jsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record TelemetryManifest(
    int SchemaVersion,
    TelemetryManifestWindow Window,
    TelemetryManifestGrid Grid,
    IReadOnlyList<TelemetryManifestFile> Files,
    IReadOnlyList<CaptureWarning> Warnings,
    TelemetryManifestProvenance Provenance,
    bool SupportsClassMetrics = false,
    IReadOnlyList<string>? Classes = null,
    string? ClassCoverage = null);

public sealed record TelemetryManifestWindow(
    string? StartTimeUtc,
    int? DurationMinutes);

public sealed record TelemetryManifestGrid(
    int Bins,
    int BinSize,
    string BinUnit);

public sealed record TelemetryManifestFile(
    string NodeId,
    TelemetryMetricKind Metric,
    string Path,
    string Hash,
    int Points,
    string? ClassId = null);

public sealed record TelemetryManifestProvenance(
    string RunId,
    string ScenarioHash,
    string? ModelHash,
    string CapturedAtUtc);
