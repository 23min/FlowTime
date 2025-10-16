using System.Text.Json;
using System.Linq;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging;

namespace FlowTime.Generator.Orchestration;

public sealed class RunOrchestrationService
{
    private readonly ITemplateService templateService;
    private readonly TelemetryBundleBuilder bundleBuilder;
    private readonly ILogger<RunOrchestrationService> logger;
    private readonly RunManifestReader manifestReader = new();

    public RunOrchestrationService(
        ITemplateService templateService,
        TelemetryBundleBuilder bundleBuilder,
        ILogger<RunOrchestrationService> logger)
    {
        this.templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        this.bundleBuilder = bundleBuilder ?? throw new ArgumentNullException(nameof(bundleBuilder));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RunOrchestrationOutcome> CreateRunAsync(RunOrchestrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var templateId = request.TemplateId ?? throw new ArgumentException("TemplateId must be provided.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.OutputRoot))
        {
            throw new ArgumentException("Output root must be provided.", nameof(request));
        }

        var outputRoot = Path.GetFullPath(request.OutputRoot);

        var effectiveParameters = BuildParameters(request);

        var mode = TemplateModeExtensions.Parse(request.Mode ?? "telemetry");
        logger.LogInformation("{Operation} {Mode} run for template {TemplateId}", request.DryRun ? "Planning" : "Creating", mode, templateId);

        var modelYaml = await templateService.GenerateEngineModelAsync(templateId, effectiveParameters, mode).ConfigureAwait(false);
        var captureDirectory = request.CaptureDirectory is null ? null : Path.GetFullPath(request.CaptureDirectory);

        if (string.Equals(mode.ToSerializedValue(), "telemetry", StringComparison.OrdinalIgnoreCase) && captureDirectory is null)
        {
            throw new InvalidOperationException("Capture directory must be provided for telemetry runs.");
        }

        if (request.DryRun)
        {
            var telemetryManifest = captureDirectory is not null
                ? await ReadCaptureManifestAsync(captureDirectory, cancellationToken).ConfigureAwait(false)
                : new TelemetryManifest(
                    SchemaVersion: 1,
                    Window: new TelemetryManifestWindow(null, null),
                    Grid: new TelemetryManifestGrid(0, 0, "minutes"),
                    Files: Array.Empty<TelemetryManifestFile>(),
                    Warnings: Array.Empty<CaptureWarning>(),
                    Provenance: new TelemetryManifestProvenance(string.Empty, string.Empty, null, DateTime.UtcNow.ToString("O")));

            var plan = new RunOrchestrationPlan(
                templateId,
                mode.ToSerializedValue(),
                outputRoot,
                captureDirectory,
                request.DeterministicRunId,
                request.RunId,
                new Dictionary<string, object?>(effectiveParameters, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(request.TelemetryBindings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                telemetryManifest);

            return new RunOrchestrationOutcome(true, null, plan);
        }

        Directory.CreateDirectory(outputRoot);
        var tempModelPath = await WriteTemporaryModelAsync(modelYaml, cancellationToken).ConfigureAwait(false);

        try
        {
            var bundleOptions = new TelemetryBundleOptions
            {
                CaptureDirectory = captureDirectory!,
                ModelPath = tempModelPath,
                OutputRoot = outputRoot,
                ProvenancePath = null,
                RunId = request.RunId,
                DeterministicRunId = request.DeterministicRunId,
                Overwrite = request.OverwriteExisting
            };

            var bundleResult = await bundleBuilder.BuildAsync(bundleOptions, cancellationToken).ConfigureAwait(false);
            var runDirectory = bundleResult.RunDirectory;
            var modelDirectory = Path.Combine(runDirectory, "model");

            var manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
            var runDocument = await RunDirectoryUtilities.LoadRunDocumentAsync(runDirectory, cancellationToken).ConfigureAwait(false);

            var telemetryResolved = manifestMetadata.TelemetrySources.Count > 0 &&
                (bundleResult.TelemetryManifest.Warnings is null ||
                 bundleResult.TelemetryManifest.Warnings.All(w => !string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase)));

            var result = new RunOrchestrationResult(
                bundleResult.RunDirectory,
                bundleResult.RunId,
                manifestMetadata,
                runDocument,
                telemetryResolved,
                bundleResult.TelemetryManifest);

            return new RunOrchestrationOutcome(false, result, null);
        }
        finally
        {
            TryDeleteTemporaryFile(tempModelPath);
        }
    }

    public async Task<RunOrchestrationResult?> TryLoadRunAsync(string runDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runDirectory) || !Directory.Exists(runDirectory))
        {
            return null;
        }

        var modelDirectory = Path.Combine(runDirectory, "model");
        if (!Directory.Exists(modelDirectory))
        {
            return null;
        }

        var manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
        var runDocument = await RunDirectoryUtilities.LoadRunDocumentAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        var telemetryManifest = await LoadTelemetryManifestAsync(runDirectory, cancellationToken).ConfigureAwait(false);

        var telemetryResolved = manifestMetadata.TelemetrySources.Count > 0 &&
            (telemetryManifest.Warnings is null || telemetryManifest.Warnings.All(w => !string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase)));

        var runId = runDocument.RunId ?? Path.GetFileName(runDirectory);
        if (string.IsNullOrWhiteSpace(runId))
        {
            runId = Path.GetFileName(runDirectory);
        }

        return new RunOrchestrationResult(
            runDirectory,
            runId!,
            manifestMetadata,
            runDocument,
            telemetryResolved,
            telemetryManifest);
    }

    private Dictionary<string, object> BuildParameters(RunOrchestrationRequest request)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (request.Parameters is not null)
        {
            foreach (var kvp in request.Parameters)
            {
                if (kvp.Value is not null)
                {
                    parameters[kvp.Key] = kvp.Value;
                }
            }
        }

        if (request.TelemetryBindings is not null)
        {
            if (string.IsNullOrWhiteSpace(request.CaptureDirectory))
            {
                throw new InvalidOperationException("Telemetry bindings were supplied but capture directory is missing.");
            }

            var captureRoot = Path.GetFullPath(request.CaptureDirectory);
            foreach (var binding in request.TelemetryBindings)
            {
                var parameterName = binding.Key;
                var relativePath = binding.Value;
                var absolutePath = Path.GetFullPath(Path.Combine(captureRoot, relativePath));

                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException($"Telemetry file for parameter '{parameterName}' not found at '{absolutePath}'.", absolutePath);
                }

                var uri = new Uri(absolutePath);
                parameters[parameterName] = uri.AbsoluteUri;
            }
        }

        return parameters;
    }

    private static async Task<TelemetryManifest> ReadCaptureManifestAsync(string captureDir, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(captureDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Telemetry manifest not found: {manifestPath}");
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<TelemetryManifest>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken).ConfigureAwait(false);
        return manifest ?? throw new InvalidOperationException($"Telemetry manifest at '{manifestPath}' was empty.");
    }

    private static async Task<TelemetryManifest> LoadTelemetryManifestAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDirectory, "model", "telemetry", "telemetry-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new TelemetryManifest(
                SchemaVersion: 1,
                Window: new TelemetryManifestWindow(null, null),
                Grid: new TelemetryManifestGrid(0, 0, "minutes"),
                Files: Array.Empty<TelemetryManifestFile>(),
                Warnings: Array.Empty<CaptureWarning>(),
                Provenance: new TelemetryManifestProvenance(string.Empty, string.Empty, null, DateTime.UtcNow.ToString("O")));
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<TelemetryManifest>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken).ConfigureAwait(false);
        return manifest ?? new TelemetryManifest(
            SchemaVersion: 1,
            Window: new TelemetryManifestWindow(null, null),
            Grid: new TelemetryManifestGrid(0, 0, "minutes"),
            Files: Array.Empty<TelemetryManifestFile>(),
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(string.Empty, string.Empty, null, DateTime.UtcNow.ToString("O")));
    }

    private static async Task<string> WriteTemporaryModelAsync(string modelYaml, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flowtime-model-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(tempFile, modelYaml, cancellationToken).ConfigureAwait(false);
        return tempFile;
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }
}
