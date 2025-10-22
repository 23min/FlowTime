using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using Microsoft.Extensions.Logging;

namespace FlowTime.Generator;

public sealed class TelemetryGenerationService
{
    private static readonly JsonSerializerOptions metadataSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<TelemetryGenerationService> logger;
    private readonly TelemetryCapture telemetryCapture;
    private readonly RunManifestReader manifestReader = new();

    public TelemetryGenerationService(ILogger<TelemetryGenerationService> logger, ILoggerFactory loggerFactory)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        telemetryCapture = new TelemetryCapture(logger: loggerFactory.CreateLogger<TelemetryCapture>());
    }

    public async Task<TelemetryGenerationResult> GenerateAsync(
        string runId,
        TelemetryGenerationOutput output,
        string runsRoot,
        string? telemetryRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("runId must be provided.", nameof(runId));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var runDirectory = Path.Combine(runsRoot, runId);
        if (!Directory.Exists(runDirectory))
        {
            throw new DirectoryNotFoundException($"Run directory '{runDirectory}' was not found.");
        }

        var captureDirectory = ResolveCaptureDirectory(output, telemetryRoot);
        var manifestPath = Path.Combine(captureDirectory, "manifest.json");

        if (File.Exists(manifestPath) && !output.Overwrite)
        {
            logger.LogInformation("Telemetry bundle already exists for run {RunId} at {CaptureDirectory}", runId, captureDirectory);
            return TelemetryGenerationResult.CreateAlreadyExists(runId, await TryReadMetadataAsync(captureDirectory, cancellationToken).ConfigureAwait(false));
        }

        if (Directory.Exists(captureDirectory))
        {
            Directory.Delete(captureDirectory, recursive: true);
        }

        Directory.CreateDirectory(captureDirectory);

        logger.LogInformation("Generating telemetry bundle for run {RunId} into {CaptureDirectory}", runId, captureDirectory);

        var plan = await telemetryCapture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = runDirectory,
            OutputDirectory = captureDirectory,
            DryRun = false
        }, cancellationToken).ConfigureAwait(false);

        var warnings = plan.Warnings ?? Array.Empty<CaptureWarning>();

        var modelDirectory = Path.Combine(runDirectory, "model");
        var manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
        var generatedAtUtc = DateTime.UtcNow.ToString("O");

        var metadata = new TelemetryGenerationMetadata(
            TemplateId: manifestMetadata.TemplateId,
            CaptureKey: output.CaptureKey,
            SourceRunId: runId,
            GeneratedAtUtc: generatedAtUtc,
            ParametersHash: string.Empty);

        var metadataPath = Path.Combine(captureDirectory, "autocapture.json");
        var metadataJson = JsonSerializer.Serialize(metadata, metadataSerializerOptions);
        await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken).ConfigureAwait(false);

        return TelemetryGenerationResult.CreateGenerated(
            runId,
            generatedAtUtc,
            warnings);
    }

    private static string ResolveCaptureDirectory(TelemetryGenerationOutput output, string? telemetryRoot)
    {
        if (!string.IsNullOrWhiteSpace(output.CaptureKey))
        {
            if (string.IsNullOrWhiteSpace(telemetryRoot))
            {
                throw new InvalidOperationException("TelemetryRoot is not configured; cannot resolve captureKey.");
            }

            var directory = Path.Combine(telemetryRoot!, output.CaptureKey);
            return Path.GetFullPath(directory);
        }

        if (!string.IsNullOrWhiteSpace(output.Directory))
        {
            var full = Path.GetFullPath(output.Directory);
            return full;
        }

        throw new InvalidOperationException("Either output.captureKey or output.directory must be provided.");
    }

    private static async Task<TelemetryGenerationMetadata?> TryReadMetadataAsync(string captureDirectory, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(captureDirectory, "autocapture.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<TelemetryGenerationMetadata>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

public sealed record TelemetryGenerationOutput(string? CaptureKey, string? Directory, bool Overwrite);

public sealed record TelemetryGenerationResult(bool Generated, bool AlreadyExists, string? GeneratedAtUtc, IReadOnlyList<CaptureWarning> Warnings, string SourceRunId)
{
    public static TelemetryGenerationResult CreateGenerated(string sourceRunId, string generatedAtUtc, IReadOnlyList<CaptureWarning> warnings) =>
        new(true, false, generatedAtUtc, warnings, sourceRunId);

    public static TelemetryGenerationResult CreateAlreadyExists(string sourceRunId, TelemetryGenerationMetadata? metadata) =>
        new(false, true, metadata?.GeneratedAtUtc, Array.Empty<CaptureWarning>(), sourceRunId);
}

public sealed record TelemetryGenerationMetadata(
    string TemplateId,
    string? CaptureKey,
    string SourceRunId,
    string GeneratedAtUtc,
    string ParametersHash);
