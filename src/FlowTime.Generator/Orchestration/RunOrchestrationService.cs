using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.Configuration;
using FlowTime.Core.Execution;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Generator.Orchestration;

public sealed class RunOrchestrationService
{
    private static readonly Meter meter = new("FlowTime.RunOrchestration", "1.0.0");
    private static readonly Counter<long> runsCreatedCounter = meter.CreateCounter<long>("run_created_total");
    private static readonly Counter<long> runsFailedCounter = meter.CreateCounter<long>("run_failed_total");
    private static readonly Histogram<double> simulationEvaluationDuration = meter.CreateHistogram<double>("simulation_evaluation_duration_ms");

    private static readonly EventId runStartEvent = new(4001, "RunOrchestrationStart");
    private static readonly EventId runValidationEvent = new(4002, "RunOrchestrationValidation");
    private static readonly EventId runEvaluationEvent = new(4003, "RunOrchestrationEvaluation");
    private static readonly EventId runArtifactsEvent = new(4004, "RunOrchestrationArtifacts");
    private static readonly EventId runCompletedEvent = new(4005, "RunOrchestrationCompleted");
    private static readonly EventId runFailedEvent = new(4500, "RunOrchestrationFailed");

    private const int DefaultSeed = 123;

    private readonly ITemplateService templateService;
    private readonly TelemetryBundleBuilder bundleBuilder;
    private readonly ILogger<RunOrchestrationService> logger;
    private readonly string? telemetryRoot;
    private readonly RunManifestReader manifestReader = new();
    private readonly IDeserializer simModelDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public RunOrchestrationService(
        ITemplateService templateService,
        TelemetryBundleBuilder bundleBuilder,
        ILogger<RunOrchestrationService> logger,
        IConfiguration? configuration = null)
    {
        this.templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        this.bundleBuilder = bundleBuilder ?? throw new ArgumentNullException(nameof(bundleBuilder));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configuredRoot = configuration?["TelemetryRoot"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            if (solutionRoot is not null)
            {
                configuredRoot = Path.Combine(solutionRoot, "examples", "time-travel");
            }
        }

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            try
            {
                telemetryRoot = Path.GetFullPath(configuredRoot);
                Directory.CreateDirectory(telemetryRoot);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to initialize telemetry root from {ConfiguredRoot}. Falling back to legacy absolute path handling.", configuredRoot);
                telemetryRoot = null;
            }
        }
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

        var mode = TemplateModeExtensions.Parse(request.Mode ?? "telemetry");
        var modeValue = mode.ToSerializedValue();
        var captureDirectory = mode == TemplateMode.Telemetry
            ? ResolveCaptureDirectory(request.CaptureDirectory, templateId)
            : null;
        if (mode == TemplateMode.Telemetry && string.IsNullOrWhiteSpace(captureDirectory))
        {
            throw new InvalidOperationException("Capture directory must be provided for telemetry runs.");
        }
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var effectiveParameters = BuildParameters(request, captureDirectory);

        var resolvedRng = await ResolveRngOptionsAsync(templateId, request.Rng, cancellationToken).ConfigureAwait(false);
        request = request with { Rng = resolvedRng };

        logger.LogInformation(
            runStartEvent,
            "Starting run orchestration for template {TemplateId} (mode={Mode}, dryRun={DryRun})",
            templateId,
            modeValue,
            request.DryRun);

        return mode switch
        {
            TemplateMode.Telemetry => await CreateTelemetryRunAsync(request, templateId, outputRoot, modeValue, effectiveParameters, captureDirectory!, cancellationToken).ConfigureAwait(false),
            TemplateMode.Simulation => await CreateSimulationRunAsync(request, templateId, outputRoot, effectiveParameters, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported template mode '{modeValue}'.")
        };
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
        var rngSeed = await TryReadRngSeedAsync(runDirectory, cancellationToken).ConfigureAwait(false);

        var isSimulation = string.Equals(manifestMetadata.Mode, TemplateMode.Simulation.ToSerializedValue(), StringComparison.OrdinalIgnoreCase);
        var telemetryResolved = isSimulation ||
            (manifestMetadata.TelemetrySources.Count > 0 &&
             (telemetryManifest.Warnings is null || telemetryManifest.Warnings.All(w => !string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase))));

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
            telemetryManifest,
            rngSeed);
    }

    private Dictionary<string, object> BuildParameters(RunOrchestrationRequest request, string? resolvedCaptureDirectory)
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
            if (string.IsNullOrWhiteSpace(resolvedCaptureDirectory))
            {
                throw new InvalidOperationException("Telemetry bindings were supplied but capture directory is missing.");
            }

            var captureRoot = resolvedCaptureDirectory;
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

    private async Task<RunOrchestrationOutcome> CreateTelemetryRunAsync(
        RunOrchestrationRequest request,
        string templateId,
        string outputRoot,
        string modeValue,
        Dictionary<string, object> effectiveParameters,
        string captureDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            runValidationEvent,
            "Validated telemetry inputs for template {TemplateId} (captureDir={CaptureDir}, captureKey={CaptureKey}, root={TelemetryRoot})",
            templateId,
            captureDirectory,
            request.CaptureDirectory,
            telemetryRoot);

        if (request.DryRun)
        {
            var telemetryManifest = await ReadCaptureManifestAsync(captureDirectory, cancellationToken).ConfigureAwait(false);
            var plan = new RunOrchestrationPlan(
                templateId,
                modeValue,
                outputRoot,
                captureDirectory,
                request.DeterministicRunId,
                request.RunId,
                CloneParameters(effectiveParameters),
                new Dictionary<string, string>(request.TelemetryBindings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
                telemetryManifest);

            logger.LogInformation(
                runCompletedEvent,
                "Telemetry dry-run planned for template {TemplateId}; files={FileCount}, warnings={WarningCount}",
                templateId,
                telemetryManifest.Files?.Count ?? 0,
                telemetryManifest.Warnings?.Count ?? 0);

            return new RunOrchestrationOutcome(true, null, plan);
        }

        Directory.CreateDirectory(outputRoot);
        var modelYaml = await templateService.GenerateEngineModelAsync(templateId, effectiveParameters, TemplateMode.Telemetry).ConfigureAwait(false);
        var tempModelPath = await WriteTemporaryModelAsync(modelYaml, cancellationToken).ConfigureAwait(false);

        try
        {
            logger.LogInformation(
                runEvaluationEvent,
                "Building telemetry bundle for template {TemplateId} (mode={Mode})",
                templateId,
                modeValue);

            var bundleOptions = new TelemetryBundleOptions
            {
                CaptureDirectory = captureDirectory,
                ModelPath = tempModelPath,
                OutputRoot = outputRoot,
                ProvenancePath = null,
                RunId = request.RunId,
                DeterministicRunId = request.DeterministicRunId,
                Overwrite = request.OverwriteExisting
            };

            var bundleResult = await bundleBuilder.BuildAsync(bundleOptions, cancellationToken).ConfigureAwait(false);
            var modelDirectory = Path.Combine(bundleResult.RunDirectory, "model");

            var manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
            var runDocument = await RunDirectoryUtilities.LoadRunDocumentAsync(bundleResult.RunDirectory, cancellationToken).ConfigureAwait(false);
            var telemetryResolved = manifestMetadata.TelemetrySources.Count > 0 &&
                (bundleResult.TelemetryManifest.Warnings is null ||
                 bundleResult.TelemetryManifest.Warnings.All(w => !string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase)));

            logger.LogInformation(
                runArtifactsEvent,
                "Telemetry artifacts written for run {RunId} (template {TemplateId})",
                bundleResult.RunId,
                templateId);

            runsCreatedCounter.Add(1, CreateMetricTags(templateId, modeValue));

            logger.LogInformation(
                runCompletedEvent,
                "Completed telemetry run {RunId} for template {TemplateId}",
                bundleResult.RunId,
                templateId);

            var result = new RunOrchestrationResult(
                bundleResult.RunDirectory,
                bundleResult.RunId,
                manifestMetadata,
                runDocument,
                telemetryResolved,
                bundleResult.TelemetryManifest,
                request.Rng?.Seed ?? DefaultSeed);

            return new RunOrchestrationOutcome(false, result, null);
        }
        catch (FileNotFoundException ex)
        {
            runsFailedCounter.Add(1, CreateMetricTags(templateId, modeValue));
            logger.LogWarning(runFailedEvent, ex, "Telemetry capture missing for template {TemplateId}", templateId);
            throw;
        }
        catch (Exception ex)
        {
            runsFailedCounter.Add(1, CreateMetricTags(templateId, modeValue));
            logger.LogError(runFailedEvent, ex, "Telemetry run creation failed for template {TemplateId}", templateId);
            throw;
        }
        finally
        {
            TryDeleteTemporaryFile(tempModelPath);
        }
    }

    private string? ResolveCaptureDirectory(string? captureDirectory, string templateId)
    {
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            logger.LogWarning(runValidationEvent, "Telemetry run requested without capture directory for template {TemplateId}", templateId);
            return null;
        }

        if (Path.IsPathFullyQualified(captureDirectory))
        {
            return Path.GetFullPath(captureDirectory);
        }

        if (!string.IsNullOrWhiteSpace(telemetryRoot))
        {
            var combined = Path.Combine(telemetryRoot, captureDirectory);
            return Path.GetFullPath(combined);
        }

        return Path.GetFullPath(captureDirectory);
    }

    private async Task<RunRngOptions> ResolveRngOptionsAsync(string templateId, RunRngOptions? requestedRng, CancellationToken cancellationToken)
    {
        const string expectedKind = "pcg32";

        var template = await templateService.GetTemplateAsync(templateId).ConfigureAwait(false)
            ?? throw new ArgumentException($"Template not found: {templateId}");
        logger.LogInformation("Template {TemplateId} rng info: kind={Kind}, seed={Seed}", templateId, template.Rng?.Kind, template.Rng?.Seed);
        var templateRequiresRng = template.Rng is not null &&
            (!string.IsNullOrWhiteSpace(template.Rng.Kind) || !string.IsNullOrWhiteSpace(template.Rng.Seed));

        if (requestedRng is null)
        {
            logger.LogDebug("Template {TemplateId} rng requirement: {Requires}", templateId, templateRequiresRng);
            if (templateRequiresRng)
            {
                throw new TemplateValidationException($"Template '{templateId}' declares an rng block; provide rng.seed in the run request.");
            }

            var defaultOptions = new RunRngOptions { Kind = expectedKind, Seed = DefaultSeed };
            logger.LogInformation("Resolved rng for template {TemplateId}: kind={Kind}, seed={Seed}", templateId, defaultOptions.Kind, defaultOptions.Seed);
            return defaultOptions;
        }

        logger.LogDebug("Template {TemplateId} rng request received. Requested seed={Seed}", templateId, requestedRng.Seed);
        if (string.IsNullOrWhiteSpace(requestedRng.Kind))
        {
            throw new TemplateValidationException("rng.kind must be provided when rng is specified.");
        }

        if (!string.Equals(requestedRng.Kind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw new TemplateValidationException($"Unsupported rng.kind '{requestedRng.Kind}'. Expected '{expectedKind}'.");
        }

        var resolved = new RunRngOptions { Kind = expectedKind, Seed = requestedRng.Seed };
        logger.LogInformation("Resolved rng for template {TemplateId}: kind={Kind}, seed={Seed}", templateId, resolved.Kind, resolved.Seed);
        return resolved;
    }

    private static async Task<int> TryReadRngSeedAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return 0;
        }

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("rng", out var rngElement) &&
                rngElement.ValueKind == JsonValueKind.Object &&
                rngElement.TryGetProperty("seed", out var seedElement) &&
                seedElement.TryGetInt32(out var seed))
            {
                return seed;
            }
        }
        catch
        {
            // ignore malformed manifest; default to zero
        }

        return 0;
    }

    private async Task<RunOrchestrationOutcome> CreateSimulationRunAsync(
        RunOrchestrationRequest request,
        string templateId,
        string outputRoot,
        Dictionary<string, object> effectiveParameters,
        CancellationToken cancellationToken)
    {
        var modelYaml = await templateService.GenerateEngineModelAsync(templateId, effectiveParameters, TemplateMode.Simulation).ConfigureAwait(false);
        var simArtifact = simModelDeserializer.Deserialize<SimModelArtifact>(modelYaml) ?? throw new InvalidOperationException("Generated simulation model artifact could not be deserialized.");

        ValidateSimulationArtifact(simArtifact, templateId);

        if (request.DryRun)
        {
            var planManifest = BuildSimulationPlanManifest(simArtifact);
            var plan = new RunOrchestrationPlan(
                templateId,
                TemplateMode.Simulation.ToSerializedValue(),
                outputRoot,
                null,
                request.DeterministicRunId,
                request.RunId,
                CloneParameters(effectiveParameters),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                planManifest);

            logger.LogInformation(
                runCompletedEvent,
                "Simulation dry-run planned for template {TemplateId}; bins={Bins}, warnings={WarningCount}",
                templateId,
                planManifest.Grid.Bins,
                planManifest.Warnings.Count);

            return new RunOrchestrationOutcome(true, null, plan);
        }

        try
        {
            Directory.CreateDirectory(outputRoot);
            var canonicalModel = ModelService.ParseAndConvert(modelYaml);
            var (grid, graph) = ModelParser.ParseModel(canonicalModel);

            logger.LogInformation(
                runEvaluationEvent,
                "Evaluating simulation model for template {TemplateId} (bins={Bins}, binSize={BinSize})",
                templateId,
                grid.Bins,
                grid.BinSize);

            var evaluationStopwatch = Stopwatch.StartNew();
            var evaluationContext = graph.Evaluate(grid);
            evaluationStopwatch.Stop();

            var evaluationDurationMs = evaluationStopwatch.Elapsed.TotalMilliseconds;
            simulationEvaluationDuration.Record(evaluationDurationMs, CreateMetricTags(templateId, TemplateMode.Simulation.ToSerializedValue()));

            logger.LogInformation(
                runEvaluationEvent,
                "Simulation evaluation completed for template {TemplateId} in {ElapsedMs:F2} ms",
                templateId,
                evaluationDurationMs);

            cancellationToken.ThrowIfCancellationRequested();

            var context = new Dictionary<NodeId, double[]>(evaluationContext.Count);
            foreach (var (nodeId, series) in evaluationContext)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context[nodeId] = series.ToArray();
            }

            var writeRequest = new RunArtifactWriter.WriteRequest
            {
                Model = canonicalModel,
                Grid = grid,
                Context = context,
                SpecText = modelYaml,
                RngSeed = request.Rng?.Seed ?? DefaultSeed,
                StartTimeBias = null,
                DeterministicRunId = request.DeterministicRunId,
                OutputDirectory = outputRoot,
                Verbose = false,
                ProvenanceJson = null
            };

            logger.LogInformation(
                runArtifactsEvent,
                "Using rng seed {Seed} for simulation run {TemplateId}",
                writeRequest.RngSeed,
                templateId);

            logger.LogInformation(
                runArtifactsEvent,
                "Writing simulation artifacts for template {TemplateId}",
                templateId);

            var writeResult = await RunArtifactWriter.WriteArtifactsAsync(writeRequest).ConfigureAwait(false);
            var finalRunId = writeResult.RunId;
            var finalRunDirectory = writeResult.RunDirectory;

            if (!string.IsNullOrWhiteSpace(request.RunId))
            {
                var explicitDirectory = Path.Combine(outputRoot, request.RunId);
                if (Directory.Exists(explicitDirectory))
                {
                    if (!request.OverwriteExisting)
                    {
                        throw new InvalidOperationException($"Run directory '{explicitDirectory}' already exists. Enable Overwrite to replace it.");
                    }

                    Directory.Delete(explicitDirectory, recursive: true);
                }

                Directory.Move(writeResult.RunDirectory, explicitDirectory);
                finalRunDirectory = explicitDirectory;
                finalRunId = request.RunId!;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var modelDirectory = Path.Combine(finalRunDirectory, "model");
            var manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
            var telemetryManifest = BuildSimulationTelemetryManifest(simArtifact, manifestMetadata, finalRunId, writeResult.ScenarioHash);
            await WriteSimulationTelemetryManifestAsync(finalRunDirectory, telemetryManifest, cancellationToken).ConfigureAwait(false);
            var runDocument = await RunDirectoryUtilities.LoadRunDocumentAsync(finalRunDirectory, cancellationToken).ConfigureAwait(false);

            runsCreatedCounter.Add(1, CreateMetricTags(templateId, TemplateMode.Simulation.ToSerializedValue()));

            logger.LogInformation(
                runCompletedEvent,
                "Completed simulation run {RunId} for template {TemplateId}",
                finalRunId,
                templateId);

            var result = new RunOrchestrationResult(
                finalRunDirectory,
                finalRunId,
                manifestMetadata,
                runDocument,
                true,
                telemetryManifest,
                writeResult.FinalSeed);

            logger.LogInformation(
                runCompletedEvent,
                "Simulation run {RunId} resolved rng seed {Seed}",
                finalRunId,
                result.RngSeed);

            return new RunOrchestrationOutcome(false, result, null);
        }
        catch (TemplateValidationException ex)
        {
            runsFailedCounter.Add(1, CreateMetricTags(templateId, TemplateMode.Simulation.ToSerializedValue()));
            logger.LogWarning(runFailedEvent, ex, "Simulation template validation failed for {TemplateId}", templateId);
            throw;
        }
        catch (Exception ex)
        {
            runsFailedCounter.Add(1, CreateMetricTags(templateId, TemplateMode.Simulation.ToSerializedValue()));
            logger.LogError(runFailedEvent, ex, "Simulation run creation failed for template {TemplateId}", templateId);
            throw;
        }
    }

    private void ValidateSimulationArtifact(SimModelArtifact artifact, string templateId)
    {
        if (artifact.Window is null || string.IsNullOrWhiteSpace(artifact.Window.Start) || string.IsNullOrWhiteSpace(artifact.Window.Timezone))
        {
            throw new TemplateValidationException($"Simulation template '{templateId}' must define window.start and window.timezone.");
        }

        if (artifact.Grid is null || artifact.Grid.Bins <= 0 || artifact.Grid.BinSize <= 0)
        {
            throw new TemplateValidationException($"Simulation template '{templateId}' must define grid.bins and grid.binSize greater than zero.");
        }

        if (artifact.Topology?.Nodes is null || artifact.Topology.Nodes.Count == 0)
        {
            throw new TemplateValidationException($"Simulation template '{templateId}' must define at least one topology node.");
        }

        logger.LogInformation(
            runValidationEvent,
            "Simulation template {TemplateId} validated (windowStart={WindowStart}, topologyNodes={NodeCount})",
            templateId,
            artifact.Window.Start,
            artifact.Topology.Nodes.Count);
    }

    private static TelemetryManifest BuildSimulationPlanManifest(SimModelArtifact artifact)
    {
        var durationMinutes = TryComputeDurationMinutes(artifact.Grid);

        return new TelemetryManifest(
            SchemaVersion: 1,
            Window: new TelemetryManifestWindow(
                artifact.Window?.Start,
                durationMinutes),
            Grid: new TelemetryManifestGrid(
                artifact.Grid?.Bins ?? 0,
                artifact.Grid?.BinSize ?? 0,
                NormalizeBinUnit(artifact.Grid?.BinUnit)),
            Files: Array.Empty<TelemetryManifestFile>(),
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(
                string.Empty,
                "pending",
                null,
                DateTime.UtcNow.ToString("O")));
    }

    private static TelemetryManifest BuildSimulationTelemetryManifest(
        SimModelArtifact artifact,
        RunManifestMetadata manifestMetadata,
        string runId,
        string scenarioHash)
    {
        var durationMinutes = TryComputeDurationMinutes(artifact.Grid);

        return new TelemetryManifest(
            SchemaVersion: 1,
            Window: new TelemetryManifestWindow(
                artifact.Window?.Start,
                durationMinutes),
            Grid: new TelemetryManifestGrid(
                artifact.Grid?.Bins ?? 0,
                artifact.Grid?.BinSize ?? 0,
                NormalizeBinUnit(artifact.Grid?.BinUnit)),
            Files: Array.Empty<TelemetryManifestFile>(),
            Warnings: Array.Empty<CaptureWarning>(),
            Provenance: new TelemetryManifestProvenance(
                runId,
                scenarioHash,
                manifestMetadata.Schema.Hash,
                DateTime.UtcNow.ToString("O")));
    }

    private static async Task WriteSimulationTelemetryManifestAsync(string runDirectory, TelemetryManifest manifest, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(runDirectory, "model", "telemetry", "telemetry-manifest.json");
        await CaptureManifestWriter.WriteAsync(manifestPath, manifest, cancellationToken).ConfigureAwait(false);
    }

    private static int? TryComputeDurationMinutes(TemplateGrid? grid)
    {
        if (grid is null || grid.Bins <= 0 || grid.BinSize <= 0)
        {
            return null;
        }

        try
        {
            checked
            {
                return grid.Bins * grid.BinSize;
            }
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static string NormalizeBinUnit(string? binUnit)
    {
        return string.IsNullOrWhiteSpace(binUnit) ? "minutes" : binUnit!;
    }

    private static KeyValuePair<string, object?>[] CreateMetricTags(string templateId, string mode) =>
        new[]
        {
            new KeyValuePair<string, object?>("templateId", templateId),
            new KeyValuePair<string, object?>("mode", mode)
        };

    private static Dictionary<string, object?> CloneParameters(Dictionary<string, object> source)
    {
        var clone = new Dictionary<string, object?>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            clone[pair.Key] = pair.Value;
        }

        return clone;
    }


}
