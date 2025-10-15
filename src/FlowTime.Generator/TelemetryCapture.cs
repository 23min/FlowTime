using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FlowTime.Core.Models;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Capture;
using FlowTime.Generator.Models;
using FlowTime.Generator.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Generator;

/// <summary>
/// High-level orchestration entry point for telemetry capture.
/// </summary>
public sealed class TelemetryCapture
{
    private static readonly EventId CaptureStartEvent = new(2001, "TelemetryCaptureStart");
    private static readonly EventId CaptureFileEvent = new(2002, "TelemetryCaptureFile");
    private static readonly EventId CaptureCompletedEvent = new(2003, "TelemetryCaptureCompleted");
    private static readonly EventId CaptureWarningEvent = new(2004, "TelemetryCaptureWarning");

    private readonly RunArtifactReader artifactReader;
    private readonly ILogger<TelemetryCapture> logger;

    public TelemetryCapture(RunArtifactReader? artifactReader = null, ILogger<TelemetryCapture>? logger = null)
    {
        this.artifactReader = artifactReader ?? new RunArtifactReader();
        this.logger = logger ?? NullLogger<TelemetryCapture>.Instance;
    }

    public async Task<TelemetryCapturePlan> ExecuteAsync(TelemetryCaptureOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var context = await artifactReader.ReadAsync(options.RunDirectory, cancellationToken).ConfigureAwait(false);
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        var plannedFiles = new List<PlannedCaptureFile>();
        var warnings = new List<CaptureWarning>();
        var captureItems = new List<CapturedSeries>();
        var injector = new GapInjector(options.GapOptions);

        logger.LogInformation(
            CaptureStartEvent,
            "Telemetry capture started for run {RunId} (output={OutputDirectory})",
            context.Manifest.RunId,
            outputDirectory);

        foreach (var binding in context.SeriesBindings)
        {
            var sourcePath = Path.Combine(context.RunDirectory, NormalizeRelativePath(binding.SourcePath));
            var (series, readWarnings) = await ReadSeriesAsync(sourcePath, binding.Points, cancellationToken).ConfigureAwait(false);
            warnings.AddRange(readWarnings);
            foreach (var warning in readWarnings)
            {
                logger.LogWarning(
                    CaptureWarningEvent,
                    "Capture warning {Code} for {NodeId}:{Metric} - {Message}",
                    warning.Code,
                    binding.NodeId,
                    binding.Metric,
                    warning.Message);
            }

            var gapResult = injector.Process(binding.NodeId, binding.Metric, series);
            warnings.AddRange(gapResult.Warnings);
            foreach (var warning in gapResult.Warnings)
            {
                logger.LogWarning(
                    CaptureWarningEvent,
                    "Capture warning {Code} for {NodeId}:{Metric} - {Message}",
                    warning.Code,
                    binding.NodeId,
                    binding.Metric,
                    warning.Message);
            }

            plannedFiles.Add(new PlannedCaptureFile(
                binding.NodeId,
                binding.Metric,
                binding.SourceSeriesId,
                binding.TargetFileName));

            captureItems.Add(new CapturedSeries(binding, gapResult.Series));

            logger.LogInformation(
                CaptureFileEvent,
                "Captured {NodeId}:{Metric} -> {TargetFile}",
                binding.NodeId,
                binding.Metric,
                binding.TargetFileName);
        }

        var plan = new TelemetryCapturePlan(
            context.Manifest.RunId,
            outputDirectory,
            plannedFiles,
            warnings.ToArray());

        if (options.DryRun)
        {
            logger.LogInformation(
                CaptureCompletedEvent,
                "Telemetry capture dry-run complete for run {RunId} (files={FileCount}, warnings={WarningCount})",
                context.Manifest.RunId,
                plannedFiles.Count,
                warnings.Count);
            return plan;
        }

        Directory.CreateDirectory(outputDirectory);

        var manifestFiles = new List<TelemetryManifestFile>();

        foreach (var item in captureItems)
        {
            var targetPath = Path.Combine(outputDirectory, item.Binding.TargetFileName);
            var formattedSeries = item.Series;

            await WriteTelemetryCsvAsync(targetPath, formattedSeries, cancellationToken).ConfigureAwait(false);
            var hash = await ComputeSha256Async(targetPath, cancellationToken).ConfigureAwait(false);

            manifestFiles.Add(new TelemetryManifestFile(
                NodeId: item.Binding.NodeId,
                Metric: item.Binding.Metric,
                Path: item.Binding.TargetFileName.Replace(Path.DirectorySeparatorChar, '/'),
                Hash: hash,
                Points: item.Binding.Points));
        }

        var manifest = new TelemetryManifest(
            SchemaVersion: 1,
            Window: BuildWindow(context.Model),
            Grid: new TelemetryManifestGrid(
                context.SeriesIndex.Grid.Bins,
                context.SeriesIndex.Grid.BinSize,
                context.SeriesIndex.Grid.BinUnit),
            Files: manifestFiles,
            Warnings: plan.Warnings,
            Provenance: new TelemetryManifestProvenance(
                context.Manifest.RunId,
                context.Manifest.ScenarioHash,
                context.Manifest.ModelHash,
                CapturedAtUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        await CaptureManifestWriter.WriteAsync(manifestPath, manifest, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            CaptureCompletedEvent,
            "Telemetry capture completed for run {RunId} (files={FileCount}, warnings={WarningCount})",
            context.Manifest.RunId,
            plannedFiles.Count,
            warnings.Count);

        return plan;
    }

    private static TelemetryManifestWindow BuildWindow(ModelDefinition model)
    {
        var start = model.Grid?.StartTimeUtc;
        var durationMinutes = ComputeDurationMinutes(model.Grid);
        return new TelemetryManifestWindow(start, durationMinutes);
    }

    private static int? ComputeDurationMinutes(GridDefinition? grid)
    {
        if (grid is null)
        {
            return null;
        }

        var unit = grid.BinUnit?.Trim().ToLowerInvariant();
        return unit switch
        {
            "minutes" => grid.Bins * grid.BinSize,
            "hours" => grid.Bins * grid.BinSize * 60,
            "days" => grid.Bins * grid.BinSize * 1440,
            _ => null
        };
    }

    private static async Task<(IReadOnlyList<double?> Series, IReadOnlyList<CaptureWarning> Warnings)> ReadSeriesAsync(
        string filePath,
        int expectedPoints,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Series file not found: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

        var warnings = new List<CaptureWarning>();
        var values = new List<double?>(capacity: Math.Max(expectedPoints, 0));
        var header = await reader.ReadLineAsync().ConfigureAwait(false);
        if (header is null)
        {
            throw new InvalidDataException($"Series file {filePath} is empty.");
        }

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',', 2);
            if (parts.Length < 2)
            {
                throw new InvalidDataException($"Invalid CSV row '{line}' in {filePath}.");
            }

            var rawValue = parts[1].Trim();

            if (rawValue.Length == 0)
            {
                values.Add(null);
                continue;
            }

            if (rawValue.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                values.Add(double.NaN);
                continue;
            }

            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
            else
            {
                throw new InvalidDataException($"Unable to parse value '{rawValue}' from {filePath}.");
            }
        }

        if (expectedPoints > 0 && values.Count != expectedPoints)
        {
            warnings.Add(new CaptureWarning(
                Code: "length_mismatch",
                Message: $"Series '{Path.GetFileName(filePath)}' contained {values.Count} points but expected {expectedPoints}."));
        }

        return (values.ToArray(), warnings);
    }

    private static async Task WriteTelemetryCsvAsync(string targetPath, IReadOnlyList<double?> series, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var stream = File.Create(targetPath);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            NewLine = "\n"
        };

        await writer.WriteLineAsync("bin_index,value").ConfigureAwait(false);
        for (var i = 0; i < series.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var value = series[i];
            var formatted = FormatValue(value);
            await writer.WriteLineAsync(FormattableString.Invariant($"{i},{formatted}")).ConfigureAwait(false);
        }
    }

    private static string FormatValue(double? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        if (double.IsNaN(value.Value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value.Value))
        {
            return value.Value > 0 ? "Infinity" : "-Infinity";
        }

        return value.Value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private sealed record CapturedSeries(
        TelemetrySeriesBinding Binding,
        IReadOnlyList<double?> Series);
}
