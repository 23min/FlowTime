using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private static readonly double[] fallbackRetryKernel = RetryKernelPolicy.DefaultKernel;
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
        var context = await LoadContextAsync(runId, loadComputedValues: false, cancellationToken);

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

        var flowLatency = ComputeFlowLatency(context);
        var nodeSnapshots = new List<NodeSnapshot>(capacity: context.Topology.Nodes.Count);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            flowLatency.TryGetValue(topologyNode.Id, out var nodeFlowLatency);
            nodeSnapshots.Add(BuildNodeSnapshot(topologyNode, data, context, binIndex, GetNodeWarnings(validation, topologyNode.Id), nodeFlowLatency));
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

    public async Task<StateWindowResponse> GetStateWindowAsync(
        string runId,
        int startBin,
        int endBin,
        GraphQueryMode mode = GraphQueryMode.Operational,
        CancellationToken cancellationToken = default)
    {
        var includeComputed = mode == GraphQueryMode.Full;
        var context = await LoadContextAsync(runId, loadComputedValues: includeComputed, cancellationToken);

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

        var topologyNodes = includeComputed
            ? context.Topology.Nodes
            : context.Topology.Nodes.Where(node => !IsComputedKind(node.Kind)).ToList();

        var flowLatency = ComputeFlowLatency(context);
        var seriesList = new List<NodeSeries>(capacity: topologyNodes.Count);
        foreach (var topologyNode in topologyNodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            flowLatency.TryGetValue(topologyNode.Id, out var nodeFlowLatency);
            seriesList.Add(BuildNodeSeries(topologyNode, data, context, startBin, count, GetNodeWarnings(validation, topologyNode.Id), nodeFlowLatency));
        }

        if (includeComputed)
        {
            foreach (var kvp in context.ModelNodes)
            {
                var modelNode = kvp.Value;
                var nodeId = modelNode.Id ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                var kind = NormalizeKind(modelNode.Kind);
                if (!IsComputedKind(kind))
                {
                    continue;
                }

                if (!context.NodeData.TryGetValue(nodeId, out var data) || data.Values is null)
                {
                    continue;
                }

                var series = BuildComputedNodeSeries(nodeId, kind, data, context, startBin, count, GetNodeWarnings(validation, nodeId));
                if (series is not null)
                {
                    seriesList.Add(series);
                }
            }
        }

        var edgeSeries = BuildEdgeSeries(context, startBin, count);

        logger.LogInformation(
            stateWindowEvent,
            "Resolved state window for run {RunId} (mode={Mode}, windowMode={WindowMode}) from bin {StartBin} to {EndBin} ({RequestedBins} of {TotalBins})",
            runId,
            context.ManifestMetadata.Mode,
            mode,
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
            Edges = edgeSeries,
            Warnings = BuildWarnings(context, validation.Warnings)
        };
    }

    private async Task<StateRunContext> LoadContextAsync(string runId, bool loadComputedValues, CancellationToken cancellationToken)
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
            var seriesIndex = await adapter.GetIndexAsync();

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
            var bins = metadata.Window.Bins;
            var valueSeriesLookup = BuildSeriesLookup(manifest.Series);
            var modelNodeDefinitions = modelDefinition.Nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Id))
                .ToDictionary(n => n.Id!, StringComparer.OrdinalIgnoreCase);
            var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal);
            var preValidationWarnings = new List<ModeValidationWarning>();
            var preValidationNodeWarnings = new Dictionary<string, List<ModeValidationWarning>>(StringComparer.OrdinalIgnoreCase);
            var appendedGlobalMissingWarning = false;

            foreach (var node in metadata.Topology.Nodes)
            {
                try
                {
                    var data = loader.LoadNodeData(node, bins);
                    data = AugmentNodeDataFromManifest(
                        node,
                        data,
                        runDirectory,
                        bins,
                        valueSeriesLookup);
                    var kernelResult = RetryKernelPolicy.Apply(data.RetryKernel);
                    if (!ReferenceEquals(data.RetryKernel, kernelResult.Kernel))
                    {
                        data = data with { RetryKernel = kernelResult.Kernel };
                    }
                    if (kernelResult.HasMessages)
                    {
                        foreach (var message in kernelResult.Messages)
                        {
                            AppendNodeWarning(preValidationNodeWarnings, node.Id, "retry_kernel_policy", message);
                        }
                    }

                    EnsureSeriesPresence(node, data, bins, preValidationNodeWarnings);
                    ValidateAttemptConservation(node, data, preValidationNodeWarnings);

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

            if (loadComputedValues)
            {
                foreach (var nodeDef in modelNodeDefinitions.Values)
                {
                    var nodeId = nodeDef.Id!;
                    var kind = NormalizeKind(string.IsNullOrWhiteSpace(nodeDef.Kind) ? "const" : nodeDef.Kind);
                    if (!IsComputedKind(kind))
                    {
                        continue;
                    }

                    double[]? values = null;
                    if (nodeDef.Values is { Length: > 0 })
                    {
                        values = NormalizeInlineValues(nodeDef.Values, bins);
                    }
                    else if (valueSeriesLookup.TryGetValue(nodeId, out var seriesRef))
                    {
                        var seriesPath = Path.Combine(runDirectory, seriesRef.Path.Replace('/', Path.DirectorySeparatorChar));
                        try
                        {
                            values = CsvReader.ReadTimeSeries(seriesPath, bins);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to load series {SeriesId} for computed node {NodeId} in run {RunId}", seriesRef.Id, nodeId, runId);
                            AppendNodeWarning(preValidationNodeWarnings, nodeId, "value_series_missing", $"Series '{seriesRef.Id}' could not be loaded for computed node '{nodeId}'.");
                        }
                    }
                    else
                    {
                        AppendNodeWarning(preValidationNodeWarnings, nodeId, "value_series_missing", $"No output series was found for computed node '{nodeId}'.");
                    }

                    if (values is null)
                    {
                        continue;
                    }

                    if (nodeData.TryGetValue(nodeId, out var existing))
                    {
                        nodeData[nodeId] = existing with { Values = values };
                    }
                    else
                    {
                        nodeData[nodeId] = CreateValuesOnlyNodeData(nodeId, bins, values);
                    }
                }
            }

            var readonlyNodeWarnings = preValidationNodeWarnings.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ModeValidationWarning>)kvp.Value,
                StringComparer.OrdinalIgnoreCase);

            return new StateRunContext(
                manifest,
                manifestMetadata,
                metadata.Window,
                metadata.Topology,
                nodeData,
                preValidationWarnings,
                readonlyNodeWarnings,
                new ReadOnlyDictionary<string, NodeDefinition>(modelNodeDefinitions),
                seriesIndex);
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

    private static NodeSnapshot BuildNodeSnapshot(Node node, NodeData data, StateRunContext context, int binIndex, IReadOnlyList<ModeValidationWarning> nodeWarnings, double?[]? flowLatencyForNode = null)
    {
        var kind = NormalizeKind(node.Kind);
        var arrivals = GetValue(data.Arrivals, binIndex);
        var served = GetValue(data.Served, binIndex);
        var errors = GetValue(data.Errors, binIndex);
        var externalDemand = GetOptionalValue(data.ExternalDemand, binIndex);
        var queue = GetOptionalValue(data.QueueDepth, binIndex);
        var capacity = GetOptionalValue(data.Capacity, binIndex);
        var isServiceKind = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase);
        var attempts = isServiceKind ? ComputeAttemptValue(data, binIndex, allowDerived: true) : null;
        var failures = isServiceKind ? ComputeFailureValue(data, binIndex) : null;
        var exhaustedFailures = isServiceKind ? GetOptionalValue(data.ExhaustedFailures, binIndex) : null;
        var retryEcho = isServiceKind ? ComputeRetryEchoValue(data, binIndex, allowDerived: true) : null;
        var retryBudgetRemaining = isServiceKind ? GetOptionalValue(data.RetryBudgetRemaining, binIndex) : null;
        var maxAttempts = node.Semantics?.MaxAttempts;

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
        var retryTax = ComputeRetryTaxValue(attempts, served);

        double? flowLatencyMs = null;
        if (flowLatencyForNode != null && binIndex >= 0 && binIndex < flowLatencyForNode.Length)
        {
            var raw = flowLatencyForNode[binIndex];
            flowLatencyMs = raw.HasValue && double.IsFinite(raw.Value) ? raw.Value : null;
        }

        double? serviceTimeMs = null;
        if (string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase))
        {
            serviceTimeMs = ComputeServiceTimeValue(data, binIndex);
        }

        var color = string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase)
            ? ColoringRules.PickQueueColor(latencyMinutes, node.Semantics?.SlaMinutes)
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
                Attempts = attempts,
                Failures = failures,
                ExhaustedFailures = exhaustedFailures,
                RetryEcho = retryEcho,
                RetryBudgetRemaining = retryBudgetRemaining,
                Queue = queue,
                Capacity = capacity,
                ExternalDemand = externalDemand,
                MaxAttempts = maxAttempts
            },
            Derived = new NodeDerivedMetrics
            {
                Utilization = utilization,
                LatencyMinutes = latencyMinutes,
                ServiceTimeMs = serviceTimeMs,
                FlowLatencyMs = flowLatencyMs,
                ThroughputRatio = throughputRatio,
                RetryTax = retryTax,
                Color = color
            },
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, nodeWarnings),
            Aliases = node.Semantics?.Aliases
        };
    }

    private static NodeData CreateEmptyNodeData(Node node, int bins)
    {
        double[] CreateSeries() => new double[bins];
        var kernelResult = RetryKernelPolicy.Apply(node.Semantics.RetryKernel?.ToArray());

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = CreateSeries(),
            Served = CreateSeries(),
            Errors = CreateSeries(),
            Attempts = node.Semantics.Attempts != null ? CreateSeries() : null,
            Failures = node.Semantics.Failures != null ? CreateSeries() : null,
            ExhaustedFailures = node.Semantics.ExhaustedFailures != null ? CreateSeries() : null,
            RetryEcho = node.Semantics.RetryEcho != null ? CreateSeries() : null,
            RetryKernel = kernelResult.Kernel,
            ExternalDemand = node.Semantics.ExternalDemand != null ? CreateSeries() : null,
            QueueDepth = node.Semantics.QueueDepth != null ? CreateSeries() : null,
            Capacity = node.Semantics.Capacity != null ? CreateSeries() : null,
            ProcessingTimeMsSum = node.Semantics.ProcessingTimeMsSum != null ? CreateSeries() : null,
            ServedCount = node.Semantics.ServedCount != null ? CreateSeries() : null,
            RetryBudgetRemaining = node.Semantics.RetryBudgetRemaining != null ? CreateSeries() : null
        };
    }

    private static NodeSeries BuildNodeSeries(Node node, NodeData data, StateRunContext context, int startBin, int count, IReadOnlyList<ModeValidationWarning> nodeWarnings, double?[]? flowLatencyForNode = null)
    {
        var kind = NormalizeKind(node.Kind);
        var arrivalsSlice = ExtractSlice(data.Arrivals, startBin, count);
        var servedSlice = ExtractSlice(data.Served, startBin, count);
        var errorsSlice = ExtractSlice(data.Errors, startBin, count);

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = arrivalsSlice,
            ["served"] = servedSlice,
            ["errors"] = errorsSlice
        };

        var isServiceKind = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase);

        var attemptsSeries = isServiceKind
            ? ComputeAttemptSeries(data, startBin, count, allowDerived: true)
            : null;
        if (attemptsSeries != null)
        {
            series["attempts"] = attemptsSeries;
        }

        if (isServiceKind)
        {
            var failuresSlice = data.Failures != null ? ExtractSlice(data.Failures, startBin, count) : errorsSlice;
            series["failures"] = failuresSlice;

            if (data.ExhaustedFailures != null)
            {
                series["exhaustedFailures"] = ExtractSlice(data.ExhaustedFailures, startBin, count);
            }

            if (data.RetryBudgetRemaining != null)
            {
                series["retryBudgetRemaining"] = ExtractSlice(data.RetryBudgetRemaining, startBin, count);
            }
        }

        var retryEchoSeries = isServiceKind
            ? ComputeRetryEchoSeries(data, startBin, count, allowDerived: true)
            : null;
        if (retryEchoSeries != null)
        {
            series["retryEcho"] = retryEchoSeries;
        }

        var retryTaxSeries = ComputeRetryTaxSeries(attemptsSeries, servedSlice);
        if (retryTaxSeries != null)
        {
            series["retryTax"] = retryTaxSeries;
        }

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

        if (string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase))
        {
            var serviceTimeSeries = ComputeServiceTimeSeries(data, startBin, count);
            if (serviceTimeSeries != null)
            {
                series["serviceTimeMs"] = serviceTimeSeries;
            }
        }

        if (flowLatencyForNode != null)
        {
            var flowLatencySlice = ExtractSlice(flowLatencyForNode, startBin, count);
            series["flowLatencyMs"] = flowLatencySlice;
        }

        if (data.Values is not null)
        {
            var valuesSlice = ExtractSlice(data.Values, startBin, count);
            series["values"] = valuesSlice;
            series[$"series:{node.Id}"] = valuesSlice;
        }

        return new NodeSeries
        {
            Id = node.Id,
            Kind = kind,
            Series = series,
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, nodeWarnings),
            Aliases = node.Semantics?.Aliases
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
        TryAdd(node.Semantics.Attempts);
        TryAdd(node.Semantics.Failures);
        TryAdd(node.Semantics.ExhaustedFailures);
        TryAdd(node.Semantics.RetryEcho);
        TryAdd(node.Semantics.RetryBudgetRemaining);
        TryAdd(node.Semantics.ExternalDemand);
        TryAdd(node.Semantics.QueueDepth);
        TryAdd(node.Semantics.Capacity);
        TryAdd(node.Semantics.ProcessingTimeMsSum);
        TryAdd(node.Semantics.ServedCount);

        if (nodeWarnings.Count > 0)
        {
            return new NodeTelemetryInfo
            {
                Sources = Array.Empty<string>(),
                Warnings = ConvertNodeWarnings(nodeWarnings)
            };
        }

        if (sources.Count == 0 && manifestMetadata.TelemetrySources.Count > 0)
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

    private static double?[] ExtractSlice(double?[] source, int start, int count)
    {
        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            if (index >= 0 && index < source.Length)
            {
                var sample = source[index];
                result[i] = sample.HasValue ? Normalize(sample.Value) : null;
            }
            else
            {
                result[i] = null;
            }
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

    private static double? ComputeServiceTimeValue(NodeData data, int index)
    {
        if (data.ProcessingTimeMsSum is null || data.ServedCount is null)
        {
            return null;
        }

        if (index < 0 ||
            index >= data.ProcessingTimeMsSum.Length ||
            index >= data.ServedCount.Length)
        {
            return null;
        }

        var sum = data.ProcessingTimeMsSum[index];
        var count = data.ServedCount[index];

        if (!double.IsFinite(sum) || !double.IsFinite(count))
        {
            return null;
        }

        if (sum == 0 && count == 0)
        {
            return 0d;
        }

        var denominator = count <= 0 ? 1d : count;
        var value = sum / denominator;
        return double.IsFinite(value) ? value : null;
    }

    private static double?[]? ComputeServiceTimeSeries(NodeData data, int start, int count)
    {
        if (data.ProcessingTimeMsSum is null || data.ServedCount is null)
        {
            return null;
        }

        var sumLength = data.ProcessingTimeMsSum.Length;
        var countLength = data.ServedCount.Length;
        var result = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            if (index < 0 || index >= sumLength || index >= countLength)
            {
                result[i] = null;
                continue;
            }

            result[i] = ComputeServiceTimeValue(data, index);
        }

        return result;
    }

    private static Dictionary<string, double?[]> ComputeFlowLatency(StateRunContext context)
    {
        var bins = context.Window.Bins;
        var result = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);

        var predecessors = new Dictionary<string, List<(string Pred, double Weight)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in context.Topology.Edges)
        {
            var from = edge.Source?.Split(':')[0];
            var to = edge.Target?.Split(':')[0];
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                continue;
            }

            var weight = edge.Multiplier ?? edge.Weight;
            if (weight <= 0 || double.IsNaN(weight) || double.IsInfinity(weight))
            {
                weight = 1d;
            }

            if (!predecessors.TryGetValue(to, out var list))
            {
                list = new List<(string Pred, double Weight)>();
                predecessors[to] = list;
            }

            list.Add((from, weight));
        }

        foreach (var node in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(node.Id, out var data))
            {
                continue;
            }

            var baseSeries = new double?[bins];
            var kind = NormalizeKind(node.Kind);
            var isQueue = string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase);
            var isService = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase);

            for (var i = 0; i < bins; i++)
            {
                double? baseValue = null;
                if (isQueue)
                {
                    var queue = GetOptionalValue(data.QueueDepth, i);
                    var served = GetOptionalValue(data.Served, i);
                    if (queue.HasValue && served.HasValue)
                    {
                        var latMin = LatencyComputer.Calculate(queue.Value, served.Value, context.Window.BinDuration.TotalMinutes);
                        if (latMin.HasValue && double.IsFinite(latMin.Value))
                        {
                            baseValue = latMin.Value * 60000d;
                        }
                    }
                }
                else if (isService)
                {
                    baseValue = ComputeServiceTimeValue(data, i);
                }

                baseSeries[i] = baseValue;
            }

            double?[]? upstream = null;
            if (predecessors.TryGetValue(node.Id, out var preds) && preds.Count > 0)
            {
                upstream = new double?[bins];
                for (var i = 0; i < bins; i++)
                {
                    double? bestLatency = null;
                    double bestServed = double.NegativeInfinity;

                    foreach (var (predId, weight) in preds)
                    {
                        if (!result.TryGetValue(predId, out var predSeries))
                        {
                            continue;
                        }

                        if (!context.NodeData.TryGetValue(predId, out var predData))
                        {
                            continue;
                        }

                        var candidateLatency = predSeries[i];
                        if (!candidateLatency.HasValue || !double.IsFinite(candidateLatency.Value))
                        {
                            continue;
                        }

                        var servedSeries = predData.Served;
                        var servedVal = servedSeries != null && i < servedSeries.Length ? servedSeries[i] * weight : 0d;

                        if (!double.IsFinite(servedVal))
                        {
                            continue;
                        }

                        if (servedVal > bestServed)
                        {
                            bestServed = servedVal;
                            bestLatency = candidateLatency.Value;
                        }
                    }

                    upstream[i] = bestLatency;
                }
            }

            var combined = new double?[bins];
            for (var i = 0; i < bins; i++)
            {
                var baseVal = baseSeries[i];
                var upVal = upstream?[i];

                if (baseVal.HasValue && double.IsFinite(baseVal.Value))
                {
                    combined[i] = upVal.HasValue ? baseVal.Value + upVal.Value : baseVal.Value;
                }
                else
                {
                    combined[i] = upVal.HasValue ? upVal.Value : null;
                }
            }

            result[node.Id] = combined;
        }

        return result;
    }

    private static IReadOnlyList<EdgeSeries> BuildEdgeSeries(StateRunContext context, int startBin, int count)
    {
        if (context.Topology.Edges is null || context.Topology.Edges.Count == 0)
        {
            return Array.Empty<EdgeSeries>();
        }

        var totalBins = context.Window.Bins;
        var result = new List<EdgeSeries>();

        foreach (var edge in context.Topology.Edges)
        {
            var sourceNodeId = ExtractNodeReference(edge.Source);
            var targetNodeId = ExtractNodeReference(edge.Target);
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(targetNodeId))
            {
                continue;
            }

            var field = NormalizeField(edge.Field);
            if (!IsRetryDependencyField(field))
            {
                continue;
            }

            if (!context.NodeData.TryGetValue(sourceNodeId, out var sourceData))
            {
                continue;
            }

            var attemptsSeries = ComputeAttemptSeries(sourceData, 0, totalBins, allowDerived: true);
            var failuresSeries = ComputeFailureSeries(sourceData, 0, totalBins);
            if (attemptsSeries is null && failuresSeries is null && sourceData.ExhaustedFailures is null)
            {
                continue;
            }

            var lag = Math.Max(0, edge.Lag ?? 0);
            var multiplier = NormalizeMultiplier(edge.Multiplier ?? edge.Weight);
            var attemptsLoad = new double?[count];
            var failuresLoad = new double?[count];
            var retryRate = new double?[count];
            double?[]? exhaustedLoad = sourceData.ExhaustedFailures != null ? new double?[count] : null;

            for (var i = 0; i < count; i++)
            {
                var sourceIndex = startBin + i - lag;
                if (sourceIndex < 0 || sourceIndex >= totalBins)
                {
                    attemptsLoad[i] = null;
                    failuresLoad[i] = null;
                    retryRate[i] = null;
                    if (exhaustedLoad is not null)
                    {
                        exhaustedLoad[i] = null;
                    }
                    continue;
                }

                var attempt = Sample(attemptsSeries, sourceIndex);
                var failure = Sample(failuresSeries, sourceIndex);
                var exhausted = GetOptionalValue(sourceData.ExhaustedFailures, sourceIndex);

                attemptsLoad[i] = attempt.HasValue ? Normalize(attempt.Value * multiplier) : null;
                failuresLoad[i] = failure.HasValue ? Normalize(failure.Value * multiplier) : null;
                retryRate[i] = ComputeRetryRate(attempt, failure);
                if (exhaustedLoad is not null)
                {
                    exhaustedLoad[i] = exhausted.HasValue ? Normalize(exhausted.Value * multiplier) : null;
                }
            }

            var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["attemptsLoad"] = attemptsLoad,
                ["failuresLoad"] = failuresLoad,
                ["retryRate"] = retryRate
            };

            if (exhaustedLoad is not null)
            {
                series["exhaustedFailuresLoad"] = exhaustedLoad;
            }

            var edgeId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}:{(string.IsNullOrWhiteSpace(edge.Field) ? "attempts" : edge.Field)}"
                : edge.Id!;

            result.Add(new EdgeSeries
            {
                Id = edgeId,
                From = sourceNodeId,
                To = targetNodeId,
                EdgeType = NormalizeEdgeType(edge.EdgeType),
                Field = field,
                Multiplier = multiplier,
                Lag = lag,
                Series = series
            });
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

    private static double? ComputeAttemptValue(NodeData data, int index, bool allowDerived)
    {
        var attempts = GetOptionalValue(data.Attempts, index);
        if (attempts.HasValue || !allowDerived)
        {
            return attempts;
        }

        var served = GetOptionalValue(data.Served, index);
        var failures = ComputeFailureValue(data, index);

        if (!served.HasValue || !failures.HasValue)
        {
            return null;
        }

        return Normalize(served.Value + failures.Value);
    }

    private static double?[]? ComputeAttemptSeries(NodeData data, int start, int count, bool allowDerived)
    {
        if (data.Attempts != null)
        {
            return ExtractSlice(data.Attempts, start, count);
        }

        if (!allowDerived || data.Served == null)
        {
            return null;
        }

        var failuresSource = data.Failures ?? data.Errors;
        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            if (index < 0 || index >= data.Served.Length)
            {
                result[i] = null;
                continue;
            }

            var served = data.Served[index];
            var failure = failuresSource[index];
            if (double.IsNaN(served) || double.IsNaN(failure) || double.IsInfinity(served) || double.IsInfinity(failure))
            {
                result[i] = null;
                continue;
            }

            result[i] = Normalize(served + failure);
        }

        return result;
    }

    private static double?[]? ComputeFailureSeries(NodeData data, int start, int count)
    {
        if (data.Failures != null)
        {
            return ExtractSlice(data.Failures, start, count);
        }

        if (data.Errors != null)
        {
            return ExtractSlice(data.Errors, start, count);
        }

        return null;
    }

    private static double? ComputeRetryEchoValue(NodeData data, int index, bool allowDerived)
    {
        var precomputed = GetOptionalValue(data.RetryEcho, index);
        if (precomputed.HasValue || !allowDerived)
        {
            return precomputed;
        }

        var failures = data.Failures ?? data.Errors;
        if (failures == null || index < 0 || index >= failures.Length)
        {
            return null;
        }

        var kernel = (data.RetryKernel != null && data.RetryKernel.Length > 0) ? data.RetryKernel : fallbackRetryKernel;

        double sum = 0;
        var hasContribution = false;
        for (var k = 0; k < kernel.Length; k++)
        {
            var sourceIndex = index - k;
            if (sourceIndex < 0)
            {
                break;
            }

            if (sourceIndex >= failures.Length)
            {
                continue;
            }

            var failure = failures[sourceIndex];
            if (double.IsNaN(failure) || double.IsInfinity(failure))
            {
                continue;
            }

            sum += failure * kernel[k];
            hasContribution = true;
        }

        if (!hasContribution)
        {
            return null;
        }

        return Normalize(sum);
    }

    private static double?[]? ComputeRetryEchoSeries(NodeData data, int start, int count, bool allowDerived)
    {
        if (data.RetryEcho != null)
        {
            return ExtractSlice(data.RetryEcho, start, count);
        }

        if (!allowDerived || (data.Errors == null && data.Failures == null))
        {
            return null;
        }

        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            result[i] = ComputeRetryEchoValue(data, index, allowDerived);
        }

        return result;
    }

    private static double? ComputeFailureValue(NodeData data, int index)
    {
        var failure = GetOptionalValue(data.Failures, index);
        if (failure.HasValue)
        {
            return failure;
        }

        return GetValue(data.Errors, index);
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

    private static double? ComputeRetryTaxValue(double? attempts, double? served)
    {
        if (!attempts.HasValue || !served.HasValue)
        {
            return null;
        }

        if (attempts.Value <= 0)
        {
            return null;
        }

        var retries = attempts.Value - served.Value;
        if (retries <= 0)
        {
            return 0d;
        }

        var ratio = retries / attempts.Value;
        return Normalize(ratio);
    }

    private static double?[]? ComputeRetryTaxSeries(double?[]? attempts, double?[]? served)
    {
        if (attempts is null || served is null || attempts.Length != served.Length)
        {
            return null;
        }

        var result = new double?[attempts.Length];
        for (var i = 0; i < attempts.Length; i++)
        {
            var attempt = attempts[i];
            var success = served[i];
            if (!attempt.HasValue || !success.HasValue || attempt.Value <= 0)
            {
                result[i] = null;
                continue;
            }

            var retries = attempt.Value - success.Value;
            if (retries <= 0)
            {
                result[i] = 0d;
                continue;
            }

            result[i] = Normalize(retries / attempt.Value);
        }

        return result;
    }

    private static Dictionary<string, SeriesReference> BuildSeriesLookup(IEnumerable<SeriesReference> seriesReferences)
    {
        var lookup = new Dictionary<string, SeriesReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in seriesReferences)
        {
            if (string.IsNullOrWhiteSpace(reference.Id))
            {
                continue;
            }

            var key = reference.Id;
            var atIndex = key.IndexOf('@');
            if (atIndex >= 0)
            {
                key = key[..atIndex];
            }

            if (!lookup.ContainsKey(key))
            {
                lookup[key] = reference;
            }
        }

        return lookup;
    }

    private static NodeSeries? BuildComputedNodeSeries(
        string nodeId,
        string kind,
        NodeData data,
        StateRunContext context,
        int start,
        int count,
        IReadOnlyList<ModeValidationWarning> nodeWarnings)
    {
        if (data.Values is null)
        {
            return null;
        }

        var valuesSlice = ExtractSlice(data.Values, start, count);

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = valuesSlice,
            [$"series:{nodeId}"] = valuesSlice
        };

        return new NodeSeries
        {
            Id = nodeId,
            Kind = kind,
            Series = series,
            Telemetry = new NodeTelemetryInfo
            {
                Sources = Array.Empty<string>(),
                Warnings = ConvertNodeWarnings(nodeWarnings)
            }
        };
    }

    private static NodeData CreateValuesOnlyNodeData(string nodeId, int bins, double[] values)
    {
        return new NodeData
        {
            NodeId = nodeId,
            Arrivals = CreateNaNSeries(bins),
            Served = CreateNaNSeries(bins),
            Errors = CreateNaNSeries(bins),
            Attempts = null,
            Failures = null,
            RetryEcho = null,
            RetryKernel = null,
            ExternalDemand = null,
            QueueDepth = null,
            Capacity = null,
            ProcessingTimeMsSum = null,
            ServedCount = null,
            Values = values
        };
    }

    private static double[] CreateNaNSeries(int bins)
    {
        var result = new double[bins];
        for (var i = 0; i < bins; i++)
        {
            result[i] = double.NaN;
        }
        return result;
    }

    private static double[] NormalizeInlineValues(double[] source, int bins)
    {
        var result = new double[bins];
        var length = Math.Min(source.Length, bins);
        Array.Copy(source, result, length);

        if (length < bins)
        {
            for (var i = length; i < bins; i++)
            {
                result[i] = double.NaN;
            }
        }

        return result;
    }

    private static void EnsureSeriesPresence(
        Node node,
        NodeData data,
        int bins,
        IDictionary<string, List<ModeValidationWarning>> warnings)
    {
        void CheckSeries(string? identifier, double[]? series, string code, string description)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return;
            }

            if (series is null || series.Length == 0)
            {
                AppendNodeWarning(warnings, node.Id, code, description);
                return;
            }

            var hasFinite = false;
            for (var i = 0; i < Math.Min(series.Length, bins); i++)
            {
                var sample = series[i];
                if (!double.IsNaN(sample) && !double.IsInfinity(sample))
                {
                    hasFinite = true;
                    break;
                }
            }

            if (!hasFinite)
            {
                AppendNodeWarning(warnings, node.Id, code, description);
            }
        }

        var semantics = node.Semantics;
        CheckSeries(semantics.Attempts, data.Attempts, "attempts_series_missing", "Attempts series was expected but could not be resolved.");
        CheckSeries(semantics.Failures, data.Failures, "failures_series_missing", "Failures series was expected but could not be resolved.");
        CheckSeries(semantics.RetryEcho, data.RetryEcho, "retry_echo_series_missing", "Retry echo series was expected but could not be resolved.");
        CheckSeries(semantics.ExhaustedFailures, data.ExhaustedFailures, "exhausted_failures_series_missing", "Exhausted failures series was expected but could not be resolved.");
    }

    private static void ValidateAttemptConservation(
        Node node,
        NodeData data,
        IDictionary<string, List<ModeValidationWarning>> warnings)
    {
        if (data.Attempts is null || data.Served is null)
        {
            return;
        }

        var failures = data.Failures ?? data.Errors;
        if (failures is null)
        {
            return;
        }

        var bins = Math.Min(data.Attempts.Length, Math.Min(data.Served.Length, failures.Length));
        const double tolerance = 1e-4;

        for (var i = 0; i < bins; i++)
        {
            var attempt = data.Attempts[i];
            var served = data.Served[i];
            var failure = failures[i];

            if (double.IsNaN(attempt) || double.IsNaN(served) || double.IsNaN(failure) ||
                double.IsInfinity(attempt) || double.IsInfinity(served) || double.IsInfinity(failure))
            {
                continue;
            }

            var expected = served + failure;
            var delta = Math.Abs(attempt - expected);
            if (delta > tolerance)
            {
                AppendNodeWarning(
                    warnings,
                    node.Id,
                    "attempts_conservation_mismatch",
                    $"Attempts do not equal served + failures for node '{node.Id}' at bin {i} (attempts={attempt:0.######}, served={served:0.######}, failures={failure:0.######}).");
                break;
            }
        }
    }

    private static void AppendNodeWarning(
        IDictionary<string, List<ModeValidationWarning>> warnings,
        string nodeId,
        string code,
        string message)
    {
        if (!warnings.TryGetValue(nodeId, out var list))
        {
            list = new List<ModeValidationWarning>();
            warnings[nodeId] = list;
        }

        list.Add(new ModeValidationWarning
        {
            Code = code,
            Message = message,
            NodeId = nodeId
        });
    }

    private static bool IsComputedKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        var normalized = NormalizeKind(kind);
        return normalized is "const" or "expression" or "expr" or "pmf";
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

    private static NodeData AugmentNodeDataFromManifest(
        Node node,
        NodeData data,
        string runDirectory,
        int bins,
        IReadOnlyDictionary<string, SeriesReference> seriesLookup)
    {
        double[]? Resolve(string? id, double[]? current)
        {
            if (current != null)
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (!seriesLookup.TryGetValue(id, out var reference) || string.IsNullOrWhiteSpace(reference.Path))
            {
                return null;
            }

            var relative = reference.Path.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(runDirectory, relative);
            if (!File.Exists(path))
            {
                return null;
            }

            return CsvReader.ReadTimeSeries(path, bins);
        }

        var semantics = node.Semantics;

        var arrivals = Resolve(semantics.Arrivals, data.Arrivals);
        var served = Resolve(semantics.Served, data.Served);
        var errors = Resolve(semantics.Errors, data.Errors);
        var attempts = Resolve(semantics.Attempts, data.Attempts);
        var failures = Resolve(semantics.Failures, data.Failures);
        var retryEcho = Resolve(semantics.RetryEcho, data.RetryEcho);
        var queue = Resolve(semantics.QueueDepth, data.QueueDepth);
        var capacity = Resolve(semantics.Capacity, data.Capacity);
        var external = Resolve(semantics.ExternalDemand, data.ExternalDemand);
        var processingTime = Resolve(semantics.ProcessingTimeMsSum, data.ProcessingTimeMsSum);
        var servedCount = Resolve(semantics.ServedCount, data.ServedCount);

        return data with
        {
            Arrivals = arrivals ?? data.Arrivals,
            Served = served ?? data.Served,
            Errors = errors ?? data.Errors,
            Attempts = attempts,
            Failures = failures ?? data.Failures,
            RetryEcho = retryEcho,
            QueueDepth = queue,
            Capacity = capacity,
            ExternalDemand = external,
            ProcessingTimeMsSum = processingTime,
            ServedCount = servedCount
        };
    }

    private static StateMetadata BuildMetadata(StateRunContext context)
    {
        var metadata = context.ManifestMetadata;
        var telemetryResolved = metadata.TelemetrySources.Count > 0 &&
            !context.InitialWarnings.Any(w => string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase));
        return new StateMetadata
        {
            RunId = context.Manifest.RunId,
            TemplateId = metadata.TemplateId,
            TemplateTitle = metadata.TemplateTitle,
            TemplateVersion = metadata.TemplateVersion,
            Mode = metadata.Mode,
            ProvenanceHash = metadata.ProvenanceHash,
            TelemetrySourcesResolved = telemetryResolved,
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

        if (context.Manifest.Warnings is { Length: > 0 })
        {
            foreach (var warning in context.Manifest.Warnings)
            {
                warnings.Add(new StateWarning
                {
                    Code = warning.Code,
                    Message = warning.Message,
                    NodeId = warning.NodeId,
                    Severity = string.IsNullOrWhiteSpace(warning.Severity) ? "warning" : warning.Severity
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

    private static string NormalizeField(string? field)
    {
        return string.IsNullOrWhiteSpace(field) ? string.Empty : field.Trim().ToLowerInvariant();
    }

    private static string NormalizeEdgeType(string? edgeType)
    {
        if (string.IsNullOrWhiteSpace(edgeType))
        {
            return "dependency";
        }

        return edgeType.Trim().ToLowerInvariant();
    }

    private static bool IsRetryDependencyField(string field)
    {
        return string.Equals(field, "attempts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "failures", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "exhaustedfailures", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractNodeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return string.Empty;
        }

        var separator = reference.IndexOf(':');
        return separator < 0 ? reference : reference[..separator];
    }

    private static double NormalizeMultiplier(double? raw)
    {
        if (!raw.HasValue || double.IsNaN(raw.Value) || double.IsInfinity(raw.Value))
        {
            return 1d;
        }

        return raw.Value;
    }

    private static double? Sample(double?[]? series, int index)
    {
        if (series is null || index < 0 || index >= series.Length)
        {
            return null;
        }

        var value = series[index];
        return value.HasValue ? Normalize(value.Value) : null;
    }

    private static double? ComputeRetryRate(double? attempts, double? failures)
    {
        if (!attempts.HasValue || attempts.Value <= 0)
        {
            return null;
        }

        var numerator = failures ?? 0d;
        var rate = numerator / attempts.Value;
        return double.IsFinite(rate) ? Normalize(rate) : null;
    }

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "service";
        }

        return kind.Trim().ToLowerInvariant();
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

        const double epsilon = 1e-9;
        if (Math.Abs(value) < epsilon)
        {
            return 0d;
        }

        var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < epsilon)
        {
            rounded = 0d;
        }

        return rounded;
    }

    private sealed record StateRunContext(
        RunManifest Manifest,
        RunManifestMetadata ManifestMetadata,
        Window Window,
        Topology Topology,
        IReadOnlyDictionary<string, NodeData> NodeData,
        IReadOnlyList<ModeValidationWarning> InitialWarnings,
        IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> InitialNodeWarnings,
        IReadOnlyDictionary<string, NodeDefinition> ModelNodes,
        SeriesIndex SeriesIndex)
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
