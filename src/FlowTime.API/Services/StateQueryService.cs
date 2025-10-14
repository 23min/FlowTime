using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.DataSources;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

public sealed class StateQueryService
{
    private const int maxWindowBins = 500;
    private static readonly EventId stateSnapshotEvent = new(3001, "StateSnapshotObservability");
    private static readonly EventId stateWindowEvent = new(3002, "StateWindowObservability");
    private readonly IConfiguration configuration;
    private readonly ILogger<StateQueryService> logger;
    private readonly RunManifestReader manifestReader;
    private readonly ModeValidator modeValidator;

    public StateQueryService(
        IConfiguration configuration,
        ILogger<StateQueryService> logger,
        RunManifestReader manifestReader,
        ModeValidator modeValidator)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
        this.modeValidator = modeValidator ?? throw new ArgumentNullException(nameof(modeValidator));
    }

    public async Task<StateSnapshotResponse> GetStateAsync(string runId, int binIndex, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(runId, cancellationToken);

        if (binIndex < 0 || binIndex >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"binIndex must be between 0 and {context.Window.Bins - 1}.");
        }

        var binStart = context.Window.GetBinStartTime(binIndex);
        var binEnd = binStart?.Add(context.Window.BinDuration);

        var validation = ValidateContext(context);
        if (validation.HasErrors)
        {
            throw new StateQueryException(422, validation.ErrorMessage ?? "Mode validation failed.", validation.ErrorCode ?? "mode_validation_failed");
        }

        var nodeSnapshots = new List<NodeSnapshot>(capacity: context.Topology.Nodes.Count);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            nodeSnapshots.Add(BuildNodeSnapshot(topologyNode, data, context, binIndex, GetNodeWarnings(validation, topologyNode.Id)));
        }

        logger.LogInformation(
            stateSnapshotEvent,
            "Resolved state snapshot for run {RunId} (mode={Mode}) at bin {BinIndex} of {TotalBins}",
            runId,
            context.ManifestMetadata.Mode,
            binIndex,
            context.Window.Bins);

        return new StateSnapshotResponse
        {
            Metadata = BuildMetadata(context),
            Bin = new BinDetail
            {
                Index = binIndex,
                StartUtc = ToOffset(binStart),
                EndUtc = ToOffset(binEnd),
                DurationMinutes = context.Window.BinDuration.TotalMinutes
            },
            Nodes = nodeSnapshots,
            Warnings = BuildWarnings(context, validation.Warnings)
        };
    }

    public async Task<StateWindowResponse> GetStateWindowAsync(string runId, int startBin, int endBin, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(runId, cancellationToken);

        var validation = ValidateContext(context);
        if (validation.HasErrors)
        {
            throw new StateQueryException(422, validation.ErrorMessage ?? "Mode validation failed.", validation.ErrorCode ?? "mode_validation_failed");
        }

        if (startBin < 0 || startBin >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"startBin must be between 0 and {context.Window.Bins - 1}.");
        }

        if (endBin < 0 || endBin >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"endBin must be between 0 and {context.Window.Bins - 1}.");
        }

        if (endBin < startBin)
        {
            throw new StateQueryException(400, "endBin must be greater than or equal to startBin.");
        }

        var count = endBin - startBin + 1;
        if (count > maxWindowBins)
        {
            throw new StateQueryException(413, $"Requested bin range {count} exceeds maximum supported window size of {maxWindowBins}.");
        }

        var timestamps = new List<DateTimeOffset>(count);
        for (var idx = startBin; idx <= endBin; idx++)
        {
            var timestamp = context.Window.GetBinStartTime(idx);
            if (!timestamp.HasValue)
            {
                throw new StateQueryException(409, "Run is missing window.startTimeUtc required for time-travel responses.");
            }

            timestamps.Add(ToOffset(timestamp)!.Value);
        }

        var seriesList = new List<NodeSeries>(capacity: context.Topology.Nodes.Count);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            seriesList.Add(BuildNodeSeries(topologyNode, data, context, startBin, count, GetNodeWarnings(validation, topologyNode.Id)));
        }

        logger.LogInformation(
            stateWindowEvent,
            "Resolved state window for run {RunId} (mode={Mode}) from bin {StartBin} to {EndBin} ({RequestedBins} of {TotalBins})",
            runId,
            context.ManifestMetadata.Mode,
            startBin,
            endBin,
            count,
            context.Window.Bins);

        return new StateWindowResponse
        {
            Metadata = BuildMetadata(context),
            Window = new WindowSlice
            {
                StartBin = startBin,
                EndBin = endBin,
                BinCount = count
            },
            TimestampsUtc = timestamps,
            Nodes = seriesList,
            Warnings = BuildWarnings(context, validation.Warnings)
        };
    }

    private async Task<StateRunContext> LoadContextAsync(string runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new StateQueryException(400, "runId must be provided.");
        }

        var artifactsDirectory = Program.GetArtifactsDirectory(configuration);
        var runDirectory = Path.Combine(artifactsDirectory, runId);

        if (!Directory.Exists(runDirectory))
        {
            throw new StateQueryException(404, $"Run '{runId}' not found.");
        }

        try
        {
            var reader = new FileSeriesReader();
            var adapter = new RunArtifactAdapter(reader, runDirectory);
            var manifest = await adapter.GetManifestAsync();

            var modelPath = ResolveModelPath(runDirectory);
            var modelYaml = await File.ReadAllTextAsync(modelPath, cancellationToken);

            ModelDefinition modelDefinition;
            try
            {
                modelDefinition = ModelService.ParseAndConvert(modelYaml);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse model for run {RunId}", runId);
                throw new StateQueryException(409, $"Model for run '{runId}' could not be parsed: {ex.Message}");
            }

            ModelMetadata metadata;
            try
            {
                metadata = ModelParser.ParseMetadata(modelDefinition, Path.GetDirectoryName(modelPath));
            }
            catch (ModelParseException ex)
            {
                logger.LogError(ex, "Invalid model metadata for run {RunId}", runId);
                throw new StateQueryException(409, $"Model metadata for run '{runId}' is invalid: {ex.Message}");
            }

            if (metadata.Topology is null)
            {
                throw new StateQueryException(412, $"Run '{runId}' does not include topology information required for state queries.");
            }

            var modelDirectory = Path.GetDirectoryName(modelPath) ?? throw new InvalidOperationException("model.yaml directory could not be determined.");
            RunManifestMetadata manifestMetadata;
            try
            {
                manifestMetadata = await manifestReader.ReadAsync(modelDirectory, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Manifest metadata missing for run {RunId}", runId);
                throw new StateQueryException(409, $"Manifest metadata for run '{runId}' is incomplete: {ex.Message}");
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                logger.LogError(ex, "Manifest metadata files missing for run {RunId}", runId);
                throw new StateQueryException(404, $"Manifest metadata for run '{runId}' not found: {ex.Message}");
            }

            if (!string.IsNullOrWhiteSpace(manifest.ModelHash) &&
                !string.IsNullOrWhiteSpace(manifestMetadata.ProvenanceHash) &&
                !string.Equals(manifest.ModelHash, manifestMetadata.ProvenanceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new StateQueryException(409,
                    $"Provenance hash mismatch for run '{runId}'. Expected '{manifest.ModelHash}' but storage reported '{manifestMetadata.ProvenanceHash}'.",
                    "provenance_mismatch");
            }

            var loader = new SemanticLoader(modelDirectory);
            var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal);
            var preValidationWarnings = new List<ModeValidationWarning>();
            var preValidationNodeWarnings = new Dictionary<string, List<ModeValidationWarning>>(StringComparer.OrdinalIgnoreCase);
            var appendedGlobalMissingWarning = false;

            foreach (var node in metadata.Topology.Nodes)
            {
                try
                {
                    var data = loader.LoadNodeData(node, metadata.Window.Bins);
                    nodeData[node.Id] = data;
                }
                catch (Exception ex)
                {
                    if (manifestMetadata.Mode.Equals("telemetry", StringComparison.OrdinalIgnoreCase) &&
                        ex is FileNotFoundException or DirectoryNotFoundException)
                    {
                        logger.LogWarning(ex, "Telemetry source missing for node {NodeId} in run {RunId}", node.Id, runId);

                        nodeData[node.Id] = CreateEmptyNodeData(node, metadata.Window.Bins);

                        if (!preValidationNodeWarnings.TryGetValue(node.Id, out var nodeList))
                        {
                            nodeList = new List<ModeValidationWarning>();
                            preValidationNodeWarnings[node.Id] = nodeList;
                        }

                        nodeList.Add(new ModeValidationWarning
                        {
                            Code = "telemetry_sources_unresolved",
                            Message = $"Telemetry source '{node.Semantics.Served}' could not be resolved for node '{node.Id}'.",
                            NodeId = node.Id
                        });

                        if (!appendedGlobalMissingWarning)
                        {
                            preValidationWarnings.Add(new ModeValidationWarning
                            {
                                Code = "telemetry_sources_missing",
                                Message = "One or more telemetry sources could not be resolved for this run."
                            });
                            appendedGlobalMissingWarning = true;
                        }

                        continue;
                    }

                    logger.LogError(ex, "Failed to load series for node {NodeId} in run {RunId}", node.Id, runId);
                    throw new StateQueryException(500, $"Failed to load data for node '{node.Id}' in run '{runId}': {ex.Message}");
                }
            }

            var readonlyNodeWarnings = preValidationNodeWarnings.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ModeValidationWarning>)kvp.Value,
                StringComparer.OrdinalIgnoreCase);

            return new StateRunContext(manifest, manifestMetadata, metadata.Window, metadata.Topology, nodeData, preValidationWarnings, readonlyNodeWarnings);
        }
        catch (StateQueryException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError(ex, "Missing artifact for run {RunId}", runId);
            throw new StateQueryException(404, $"Required artifact missing for run '{runId}': {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error loading run {RunId}", runId);
            throw new StateQueryException(500, $"Unexpected error loading run '{runId}': {ex.Message}");
        }
    }

    private static NodeSnapshot BuildNodeSnapshot(Node node, NodeData data, StateRunContext context, int binIndex, IReadOnlyList<ModeValidationWarning> nodeWarnings)
    {
        var kind = NormalizeKind(node.Kind);
        var arrivals = GetValue(data.Arrivals, binIndex);
        var served = GetValue(data.Served, binIndex);
        var errors = GetValue(data.Errors, binIndex);
        var externalDemand = GetOptionalValue(data.ExternalDemand, binIndex);
        var queue = GetOptionalValue(data.QueueDepth, binIndex);
        var capacity = GetOptionalValue(data.Capacity, binIndex);

        var rawUtilization = served.HasValue
            ? UtilizationComputer.Calculate(served.Value, capacity)
            : null;
        var utilization = rawUtilization.HasValue ? Normalize(rawUtilization.Value) : null;

        double? latencyMinutes = null;
        if (string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase) && queue.HasValue && served.HasValue)
        {
            var rawLatency = LatencyComputer.Calculate(queue.Value, served.Value, context.Window.BinDuration.TotalMinutes);
            latencyMinutes = rawLatency.HasValue ? Normalize(rawLatency.Value) : null;
        }

        var throughputRatioValue = ComputeThroughputRatio(arrivals, served);
        var throughputRatio = throughputRatioValue.HasValue ? Normalize(throughputRatioValue.Value) : null;

        var color = string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase)
            ? ColoringRules.PickQueueColor(latencyMinutes, node.Semantics.SlaMinutes)
            : ColoringRules.PickServiceColor(utilization);

        return new NodeSnapshot
        {
            Id = node.Id,
            Kind = kind,
            Metrics = new NodeMetrics
            {
                Arrivals = arrivals,
                Served = served,
                Errors = errors,
                Queue = queue,
                Capacity = capacity,
                ExternalDemand = externalDemand
            },
            Derived = new NodeDerivedMetrics
            {
                Utilization = utilization,
                LatencyMinutes = latencyMinutes,
                ThroughputRatio = throughputRatio,
                Color = color
            },
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, nodeWarnings)
        };
    }

    private static NodeData CreateEmptyNodeData(Node node, int bins)
    {
        double[] CreateSeries() => new double[bins];

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = CreateSeries(),
            Served = CreateSeries(),
            Errors = CreateSeries(),
            ExternalDemand = node.Semantics.ExternalDemand != null ? CreateSeries() : null,
            QueueDepth = node.Semantics.QueueDepth != null ? CreateSeries() : null,
            Capacity = node.Semantics.Capacity != null ? CreateSeries() : null
        };
    }

    private static NodeSeries BuildNodeSeries(Node node, NodeData data, StateRunContext context, int startBin, int count, IReadOnlyList<ModeValidationWarning> nodeWarnings)
    {
        var kind = NormalizeKind(node.Kind);
        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = ExtractSlice(data.Arrivals, startBin, count),
            ["served"] = ExtractSlice(data.Served, startBin, count),
            ["errors"] = ExtractSlice(data.Errors, startBin, count)
        };

        if (data.ExternalDemand != null)
        {
            series["externalDemand"] = ExtractSlice(data.ExternalDemand, startBin, count);
        }

        if (data.QueueDepth != null)
        {
            series["queue"] = ExtractSlice(data.QueueDepth, startBin, count);
        }

        if (data.Capacity != null)
        {
            series["capacity"] = ExtractSlice(data.Capacity, startBin, count);
            var utilizationSeries = ComputeUtilizationSeries(data, startBin, count);
            if (utilizationSeries != null)
            {
                series["utilization"] = utilizationSeries;
            }
        }

        if (string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase) && data.QueueDepth != null)
        {
            var latency = ComputeLatencySeries(data, context.Window, startBin, count);
            if (latency != null)
            {
                series["latencyMinutes"] = latency;
            }
        }

        var throughputSeries = ComputeThroughputSeries(data, startBin, count);
        if (throughputSeries != null)
        {
            series["throughputRatio"] = throughputSeries;
        }

        return new NodeSeries
        {
            Id = node.Id,
            Kind = kind,
            Series = series,
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, nodeWarnings)
        };
    }

    private static NodeTelemetryInfo BuildTelemetryInfo(Node node, RunManifestMetadata manifestMetadata, IReadOnlyList<ModeValidationWarning> nodeWarnings)
    {
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? seriesId)
        {
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return;
            }

            var identifier = seriesId.Trim();

            if (manifestMetadata.NodeSources.TryGetValue(identifier, out var source) && !string.IsNullOrWhiteSpace(source))
            {
                sources.Add(source);
                return;
            }

            if (identifier.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(identifier);
            }
        }

        TryAdd(node.Semantics.Arrivals);
        TryAdd(node.Semantics.Served);
        TryAdd(node.Semantics.Errors);
        TryAdd(node.Semantics.ExternalDemand);
        TryAdd(node.Semantics.QueueDepth);
        TryAdd(node.Semantics.Capacity);

        if (nodeWarnings.Count > 0)
        {
            return new NodeTelemetryInfo
            {
                Sources = Array.Empty<string>(),
                Warnings = ConvertNodeWarnings(nodeWarnings)
            };
        }

        if (manifestMetadata.TelemetrySources.Count > 0)
        {
            for (var i = 0; i < manifestMetadata.TelemetrySources.Count; i++)
            {
                var globalSource = manifestMetadata.TelemetrySources[i];
                if (!string.IsNullOrWhiteSpace(globalSource))
                {
                    sources.Add(globalSource.Trim());
                }
            }
        }

        return new NodeTelemetryInfo
        {
            Sources = sources.Count == 0 ? Array.Empty<string>() : sources.ToArray(),
            Warnings = ConvertNodeWarnings(nodeWarnings)
        };
    }

    private static double?[] ExtractSlice(double[] source, int start, int count)
    {
        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = Normalize(source[start + i]);
        }
        return result;
    }

    private static double?[]? ComputeUtilizationSeries(NodeData data, int start, int count)
    {
        if (data.Capacity == null)
        {
            return null;
        }

        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var served = data.Served[start + i];
            var capacity = data.Capacity[start + i];
            var raw = UtilizationComputer.Calculate(served, capacity);
            result[i] = raw.HasValue ? Normalize(raw.Value) : null;
        }

        return result;
    }

    private static double?[]? ComputeLatencySeries(NodeData data, Window window, int start, int count)
    {
        if (data.QueueDepth == null)
        {
            return null;
        }

        var result = new double?[count];
        var binMinutes = window.BinDuration.TotalMinutes;
        for (var i = 0; i < count; i++)
        {
            var queue = data.QueueDepth[start + i];
            var served = data.Served[start + i];
            var raw = LatencyComputer.Calculate(queue, served, binMinutes);
            result[i] = raw.HasValue ? Normalize(raw.Value) : null;
        }

        return result;
    }

    private static double?[]? ComputeThroughputSeries(NodeData data, int start, int count)
    {
        if (data.Arrivals == null || data.Arrivals.Length == 0)
        {
            return null;
        }

        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var arrivals = data.Arrivals[start + i];
            var served = data.Served[start + i];
            var ratio = ComputeThroughputRatio(arrivals, served);
            result[i] = ratio.HasValue ? Normalize(ratio.Value) : null;
        }

        return result;
    }

    private static double? ComputeThroughputRatio(double? arrivals, double? served)
    {
        if (!arrivals.HasValue || !served.HasValue)
        {
            return null;
        }

        if (Math.Abs(arrivals.Value) < double.Epsilon)
        {
            return null;
        }

        return served.Value / arrivals.Value;
    }

    private ModeValidationResult ValidateContext(StateRunContext context) =>
        modeValidator.Validate(new ModeValidationContext(
            context.ManifestMetadata,
            context.Window,
            context.Topology,
            context.NodeData,
            context.InitialWarnings,
            context.InitialNodeWarnings));

    private static IReadOnlyList<ModeValidationWarning> GetNodeWarnings(ModeValidationResult validation, string nodeId) =>
        validation.NodeWarnings.TryGetValue(nodeId, out var warnings)
            ? warnings
            : Array.Empty<ModeValidationWarning>();

    private static IReadOnlyList<NodeTelemetryWarning> ConvertNodeWarnings(IReadOnlyList<ModeValidationWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return Array.Empty<NodeTelemetryWarning>();
        }

        var result = new NodeTelemetryWarning[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
        {
            result[i] = new NodeTelemetryWarning
            {
                Code = warnings[i].Code,
                Message = warnings[i].Message,
                Severity = "warning"
            };
        }

        return result;
    }

    private static StateMetadata BuildMetadata(StateRunContext context)
    {
        var metadata = context.ManifestMetadata;
        return new StateMetadata
        {
            RunId = context.Manifest.RunId,
            TemplateId = metadata.TemplateId,
            TemplateTitle = metadata.TemplateTitle,
            TemplateVersion = metadata.TemplateVersion,
            Mode = metadata.Mode,
            ProvenanceHash = metadata.ProvenanceHash,
            TelemetrySourcesResolved = metadata.TelemetrySources.Count > 0,
            Schema = new SchemaMetadata
            {
                Id = metadata.Schema.Id,
                Version = metadata.Schema.Version,
                Hash = metadata.Schema.Hash
            },
            Storage = new StorageDescriptor
            {
                ModelPath = metadata.Storage.ModelPath,
                MetadataPath = metadata.Storage.MetadataPath,
                ProvenancePath = metadata.Storage.ProvenancePath
            }
        };
    }

    private static IReadOnlyList<StateWarning> BuildWarnings(StateRunContext context, IReadOnlyList<ModeValidationWarning> additionalWarnings)
    {
        var warnings = new List<StateWarning>();

        if (context.Manifest.Warnings != null)
        {
            for (var i = 0; i < context.Manifest.Warnings.Length; i++)
            {
                warnings.Add(new StateWarning
                {
                    Code = "run_warning",
                    Message = context.Manifest.Warnings[i],
                    Severity = "info"
                });
            }
        }

        if (additionalWarnings.Count > 0)
        {
            foreach (var warning in additionalWarnings)
            {
                warnings.Add(new StateWarning
                {
                    Code = warning.Code,
                    Message = warning.Message,
                    NodeId = warning.NodeId
                });
            }
        }

        return warnings.Count == 0 ? Array.Empty<StateWarning>() : warnings;
    }

    private static DateTimeOffset? ToOffset(DateTime? timestamp)
    {
        if (!timestamp.HasValue)
        {
            return null;
        }

        var value = timestamp.Value;
        if (value.Kind == DateTimeKind.Unspecified)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return new DateTimeOffset(value);
    }

    private static string ResolveModelPath(string runDirectory)
    {
        var explicitModelPath = Path.Combine(runDirectory, "model", "model.yaml");
        if (File.Exists(explicitModelPath))
        {
            return explicitModelPath;
        }

        var specPath = Path.Combine(runDirectory, "spec.yaml");
        if (File.Exists(specPath))
        {
            return specPath;
        }

        throw new FileNotFoundException("Run is missing model/model.yaml or spec.yaml");
    }

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "service";
        }

        return kind.Trim();
    }

    private static double? GetValue(double[] source, int index)
    {
        if (index < 0 || index >= source.Length)
        {
            return null;
        }

        return Normalize(source[index]);
    }

    private static double? GetOptionalValue(double[]? source, int index)
    {
        if (source == null)
        {
            return null;
        }

        if (index < 0 || index >= source.Length)
        {
            return null;
        }

        return Normalize(source[index]);
    }

    private static double? Normalize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        return value;
    }

    private sealed record StateRunContext(
        RunManifest Manifest,
        RunManifestMetadata ManifestMetadata,
        Window Window,
        Topology Topology,
        IReadOnlyDictionary<string, NodeData> NodeData,
        IReadOnlyList<ModeValidationWarning> InitialWarnings,
        IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> InitialNodeWarnings)
    {
        public double BinMinutes => Window.BinDuration.TotalMinutes;
    }
}

public sealed class StateQueryException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }

    public StateQueryException(int statusCode, string message, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
