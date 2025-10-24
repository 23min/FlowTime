using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private const int DefaultSeed = 123;

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

        var captureDirectory = ResolveCaptureDirectory(output, runDirectory, telemetryRoot);
        var runScopedDirectory = Path.Combine(runDirectory, "model", "telemetry");
        var isRunScopedCapture = string.Equals(
            Path.GetFullPath(captureDirectory),
            Path.GetFullPath(runScopedDirectory),
            StringComparison.OrdinalIgnoreCase);
        var manifestPath = Path.Combine(captureDirectory, "manifest.json");

        if (File.Exists(manifestPath) && !output.Overwrite)
        {
            logger.LogInformation("Telemetry bundle already exists for run {RunId} at {CaptureDirectory}", runId, captureDirectory);
            return TelemetryGenerationResult.CreateAlreadyExists(runId, await TryReadMetadataAsync(captureDirectory, cancellationToken).ConfigureAwait(false));
        }

        Directory.CreateDirectory(captureDirectory);

        if (output.Overwrite)
        {
            foreach (var file in Directory.EnumerateFiles(captureDirectory))
            {
                var name = Path.GetFileName(file);
                if (isRunScopedCapture && string.Equals(name, "telemetry-manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }
        }

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

        var executionSeed = await TryReadRunSeedAsync(runDirectory, cancellationToken).ConfigureAwait(false) ?? DefaultSeed;
        var parameters = await ReadRunParametersAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
        var parametersHash = ComputeParametersHash(manifestMetadata.TemplateId, manifestMetadata.TemplateVersion, parameters, executionSeed);
        var scenarioHash = await TryReadScenarioHashAsync(runDirectory, cancellationToken).ConfigureAwait(false) ?? string.Empty;

        var metadata = new TelemetryGenerationMetadata(
            TemplateId: manifestMetadata.TemplateId,
            CaptureKey: output.CaptureKey,
            SourceRunId: runId,
            GeneratedAtUtc: generatedAtUtc,
            RngSeed: executionSeed,
            ParametersHash: parametersHash,
            ScenarioHash: scenarioHash);

        var metadataPath = Path.Combine(captureDirectory, "autocapture.json");
        var metadataJson = JsonSerializer.Serialize(metadata, metadataSerializerOptions);
        await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken).ConfigureAwait(false);

        // Update the canonical telemetry manifest under model/telemetry so API/UI can reflect availability
        var captureManifestPath = Path.Combine(captureDirectory, "manifest.json");
        if (File.Exists(captureManifestPath))
        {
            await using var stream = File.OpenRead(captureManifestPath);
            var captureManifest = await JsonSerializer.DeserializeAsync<TelemetryManifest>(
                stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken).ConfigureAwait(false);
            if (captureManifest is not null)
            {
                var telemetryManifestPath = Path.Combine(modelDirectory, "telemetry", "telemetry-manifest.json");
                Directory.CreateDirectory(Path.GetDirectoryName(telemetryManifestPath)!);
                await CaptureManifestWriter.WriteAsync(telemetryManifestPath, captureManifest, cancellationToken).ConfigureAwait(false);
            }
        }

        return TelemetryGenerationResult.CreateGenerated(
            runId,
            generatedAtUtc,
            warnings);
    }

    private static string ResolveCaptureDirectory(TelemetryGenerationOutput output, string runDirectory, string? telemetryRoot)
    {
        if (!string.IsNullOrWhiteSpace(output.Directory))
        {
            return Path.GetFullPath(output.Directory);
        }

        if (!string.IsNullOrWhiteSpace(output.CaptureKey))
        {
            if (string.IsNullOrWhiteSpace(telemetryRoot))
            {
                throw new InvalidOperationException("TelemetryRoot is not configured; cannot resolve captureKey.");
            }

            var directory = Path.Combine(telemetryRoot!, output.CaptureKey);
            return Path.GetFullPath(directory);
        }

        return Path.Combine(runDirectory, "model", "telemetry");
    }

    private static async Task<int?> TryReadRunSeedAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("rng", out var rngElement) &&
            rngElement.ValueKind == JsonValueKind.Object &&
            rngElement.TryGetProperty("seed", out var seedElement) &&
            seedElement.TryGetInt32(out var seed))
        {
            return seed;
        }

        return null;
    }

    private static async Task<IDictionary<string, JsonNode?>> ReadRunParametersAsync(string modelDirectory, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(modelDirectory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(metadataPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("parameters", out var parametersElement) || parametersElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        }

        var parameters = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var property in parametersElement.EnumerateObject())
        {
            parameters[property.Name] = ConvertJsonElement(property.Value);
        }

        return parameters;
    }

    private static string ComputeParametersHash(string templateId, string templateVersion, IDictionary<string, JsonNode?> parameters, int rngSeed)
    {
        var root = new JsonObject
        {
            ["templateId"] = templateId,
            ["templateVersion"] = templateVersion,
            ["rngSeed"] = rngSeed
        };

        var parametersObject = new JsonObject();
        foreach (var kvp in parameters.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            parametersObject[kvp.Key] = kvp.Value?.DeepClone();
        }

        root["parameters"] = parametersObject;

        var json = root.ToJsonString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string?> TryReadScenarioHashAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("scenarioHash", out var scenarioElement) && scenarioElement.ValueKind == JsonValueKind.String)
        {
            return scenarioElement.GetString();
        }

        return null;
    }

    private static JsonNode? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => JsonValue.Create(element.GetString()),
            JsonValueKind.Number when element.TryGetInt64(out var l) => JsonValue.Create(l),
            JsonValueKind.Number => JsonValue.Create(element.GetDouble()),
            JsonValueKind.True => JsonValue.Create(true),
            JsonValueKind.False => JsonValue.Create(false),
            JsonValueKind.Null => null,
            JsonValueKind.Array or JsonValueKind.Object => JsonNode.Parse(element.GetRawText()),
            _ => JsonValue.Create(element.GetRawText())
        };
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
    int? RngSeed,
    string ParametersHash,
    string ScenarioHash);
