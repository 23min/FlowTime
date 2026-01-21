using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.Dispatching;
using FlowTime.Core.DataSources;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FlowTime.API.Services;

public sealed record EdgeQueryFilter(
    IReadOnlyCollection<string>? EdgeIds,
    IReadOnlyCollection<string>? EdgeMetrics,
    IReadOnlyCollection<string>? ClassIds);

public sealed class StateQueryService
{
    private const int maxWindowBins = 500;
    private static readonly EventId stateSnapshotEvent = new(3001, "StateSnapshotObservability");
    private static readonly EventId stateWindowEvent = new(3002, "StateWindowObservability");
    private const int backlogGrowthStreakBins = 3;
    private const int backlogOverloadStreakBins = 3;
    private const int backlogAgeRiskStreakBins = 3;
    private static readonly double[] fallbackRetryKernel = RetryKernelPolicy.DefaultKernel;
    private static readonly QueueLatencyStatusDescriptor pausedGateClosedStatus = new()
    {
        Code = "paused_gate_closed",
        Message = "Queue latency unavailable: dispatch gate closed"
    };
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
        var classCoverageStates = new List<ClassCoverage>(context.Topology.Nodes.Count);
        var coverageDiagnostics = new List<ClassCoverageDiagnostic>(context.Topology.Nodes.Count);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            flowLatency.TryGetValue(topologyNode.Id, out var nodeFlowLatency);
            var classAggregation = ClassMetricsAggregator.Aggregate(data, binIndex);
            LogClassCoverageDiagnostics(context.Manifest.RunId, topologyNode, data, classAggregation);
            if (classAggregation.Coverage != ClassCoverage.Full)
            {
                coverageDiagnostics.Add(new ClassCoverageDiagnostic(topologyNode.Id, classAggregation.Coverage, classAggregation.Warnings));
            }
            classCoverageStates.Add(classAggregation.Coverage);
            nodeSnapshots.Add(BuildNodeSnapshot(topologyNode, data, context, binIndex, GetNodeWarnings(validation, topologyNode.Id), classAggregation, nodeFlowLatency));
        }
        if (coverageDiagnostics.Count > 0)
        {
            LogClassCoverageSummary(context.Manifest.RunId, "snapshot", binIndex, coverageDiagnostics);
        }

        logger.LogInformation(
            stateSnapshotEvent,
            "Resolved state snapshot for run {RunId} (mode={Mode}) at bin {BinIndex} of {TotalBins}",
            runId,
            context.ManifestMetadata.Mode,
            binIndex,
            context.Window.Bins);

        var edgeSeries = BuildEdgeSeries(context, binIndex, 1);

        return new StateSnapshotResponse
        {
            Metadata = BuildMetadata(context, ResolveClassCoverage(classCoverageStates)),
            Bin = new BinDetail
            {
                Index = binIndex,
                StartUtc = ToOffset(binStart),
                EndUtc = ToOffset(binEnd),
                DurationMinutes = context.Window.BinDuration.TotalMinutes
            },
            Nodes = nodeSnapshots,
            Edges = edgeSeries,
            EdgeWarnings = BuildEdgeWarnings(context),
            Warnings = BuildWarnings(context, validation.Warnings)
        };
    }

    public async Task<StateWindowResponse> GetStateWindowAsync(
        string runId,
        int startBin,
        int endBin,
        GraphQueryMode mode = GraphQueryMode.Operational,
        EdgeQueryFilter? edgeFilter = null,
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
        var classCoverageStates = new List<ClassCoverage>(topologyNodes.Count);
        var coverageDiagnostics = new List<ClassCoverageDiagnostic>(topologyNodes.Count);
        foreach (var topologyNode in topologyNodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            flowLatency.TryGetValue(topologyNode.Id, out var nodeFlowLatency);
            var classAggregation = ClassMetricsAggregator.Aggregate(data, startBin);
            LogClassCoverageDiagnostics(context.Manifest.RunId, topologyNode, data, classAggregation);
            if (classAggregation.Coverage != ClassCoverage.Full)
            {
                coverageDiagnostics.Add(new ClassCoverageDiagnostic(topologyNode.Id, classAggregation.Coverage, classAggregation.Warnings));
            }
            classCoverageStates.Add(classAggregation.Coverage);
            var mergedWarnings = MergeWarnings(GetNodeWarnings(validation, topologyNode.Id), classAggregation.Warnings, topologyNode.Id);
            seriesList.Add(BuildNodeSeries(topologyNode, data, context, startBin, count, mergedWarnings, nodeFlowLatency));
        }
        if (coverageDiagnostics.Count > 0)
        {
            LogClassCoverageSummary(context.Manifest.RunId, "window", startBin, coverageDiagnostics);
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

        var edgeSeries = ApplyEdgeFilters(BuildEdgeSeries(context, startBin, count), edgeFilter);
        var edgeWarnings = ApplyEdgeWarningFilter(BuildEdgeWarnings(context), edgeFilter);
        var backlogWarnings = BuildBacklogWarnings(context, topologyNodes, startBin, count);
        var combinedWarnings = MergeWarnings(validation.Warnings, backlogWarnings);

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
            Metadata = BuildMetadata(context, ResolveClassCoverage(classCoverageStates)),
            Window = new WindowSlice
            {
                StartBin = startBin,
                EndBin = endBin,
                BinCount = count
            },
            TimestampsUtc = timestamps,
            Nodes = seriesList,
            Edges = edgeSeries,
            EdgeWarnings = edgeWarnings,
            Warnings = BuildWarnings(context, combinedWarnings)
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

            if (manifestMetadata.Mode.Equals("telemetry", StringComparison.OrdinalIgnoreCase))
            {
                InspectTelemetrySources(
                    metadata.Topology.Nodes,
                    manifestMetadata,
                    modelDirectory,
                    preValidationWarnings,
                    preValidationNodeWarnings,
                    ref appendedGlobalMissingWarning);
            }

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
                    var byClass = ResolveClassData(
                        node,
                        data,
                        seriesIndex,
                        runDirectory,
                        bins,
                        valueSeriesLookup,
                        logger);
                    if (byClass != null)
                    {
                        data = data with { ByClass = byClass };
                    }
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

                        if (!HasNodeWarning(preValidationNodeWarnings, node.Id, "telemetry_sources_unresolved"))
                        {
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
                        }

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

            if (manifest.Warnings is { Length: > 0 })
            {
                var warningSummary = manifest.Warnings
                    .Select(w =>
                        string.IsNullOrWhiteSpace(w.NodeId)
                            ? w.Code
                            : $"{w.Code}@{w.NodeId}")
                    .ToArray();

                logger.LogWarning(
                    "Run {RunId} manifest contains {WarningCount} warning(s): {Warnings}",
                    runId,
                    warningSummary.Length,
                    string.Join(", ", warningSummary));
            }

            return new StateRunContext(
                manifest,
                manifestMetadata,
                metadata.Window,
                metadata.Topology,
                nodeData,
                preValidationWarnings,
                readonlyNodeWarnings,
                new ReadOnlyDictionary<string, NodeDefinition>(modelNodeDefinitions),
                seriesIndex,
                runDirectory);
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

    private static NodeSnapshot BuildNodeSnapshot(
        Node node,
        NodeData data,
        StateRunContext context,
        int binIndex,
        IReadOnlyList<ModeValidationWarning> nodeWarnings,
        ClassAggregationResult classAggregation,
        double?[]? flowLatencyForNode = null)
    {
        var kind = NormalizeKind(node.Kind);
        var logicalType = DetermineLogicalType(node, kind, context, out var serviceWithBufferDefinition);
        var dispatchSchedule = ResolveDispatchSchedule(node, context, serviceWithBufferDefinition);
        var arrivals = GetValue(data.Arrivals, binIndex);
        var served = GetValue(data.Served, binIndex);
        var errors = GetOptionalValue(data.Errors, binIndex);
        var externalDemand = GetOptionalValue(data.ExternalDemand, binIndex);
        var queue = GetOptionalValue(data.QueueDepth, binIndex);
        var capacity = GetOptionalValue(data.Capacity, binIndex);
        var parallelism = GetParallelismValue(node.Semantics, data, binIndex);
        var effectiveCapacity = GetEffectiveCapacity(node.Semantics, data, binIndex, capacity);
        var isServiceWithBuffer = string.Equals(logicalType, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase);
        var isServiceKind = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase) || isServiceWithBuffer;
        var attempts = isServiceKind ? ComputeAttemptValue(data, binIndex, allowDerived: true) : null;
        var failures = isServiceKind ? ComputeFailureValue(data, binIndex) : null;
        var exhaustedFailures = isServiceKind ? GetOptionalValue(data.ExhaustedFailures, binIndex) : null;
        var retryEcho = isServiceKind ? ComputeRetryEchoValue(data, binIndex, allowDerived: true) : null;
        var retryBudgetRemaining = isServiceKind ? GetOptionalValue(data.RetryBudgetRemaining, binIndex) : null;
        var maxAttempts = node.Semantics?.MaxAttempts;

        var rawUtilization = served.HasValue
            ? UtilizationComputer.Calculate(served.Value, effectiveCapacity)
            : null;
        var utilization = rawUtilization.HasValue ? Normalize(rawUtilization.Value) : null;

        double? latencyMinutes = null;
        if (IsQueueLikeKind(kind) && queue.HasValue && served.HasValue)
        {
            var rawLatency = LatencyComputer.Calculate(queue.Value, served.Value, context.Window.BinDuration.TotalMinutes);
            latencyMinutes = rawLatency.HasValue ? Normalize(rawLatency.Value) : null;
        }

        var queueLatencyStatus = IsQueueLikeKind(kind)
            ? DetermineQueueLatencyStatus(queue, served, dispatchSchedule, binIndex)
            : null;

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
        if (isServiceKind)
        {
            serviceTimeMs = ComputeServiceTimeValue(data, binIndex);
        }

        var color = isServiceWithBuffer
            ? ColoringRules.PickServiceColor(utilization)
            : IsDlqKind(kind)
                ? "dlq"
                : IsQueueLikeKind(kind)
                    ? ColoringRules.PickQueueColor(latencyMinutes, node.Semantics?.SlaMinutes)
                    : ColoringRules.PickServiceColor(utilization);

        var mergedWarnings = MergeWarnings(nodeWarnings, classAggregation.Warnings, node.Id);
        var slaMetrics = BuildSlaMetrics(node, logicalType, data, context, binIndex, dispatchSchedule);
        var queueOrigin = data.QueueDepth is null ? null : ResolveQueueSeriesOrigin(node, context);
        var seriesMetadata = BuildDerivedSeriesMetadata(
            hasLatency: IsQueueLikeKind(kind) && data.QueueDepth is not null && data.Served is not null,
            hasServiceTime: isServiceKind && data.ProcessingTimeMsSum is not null && data.ServedCount is not null,
            hasFlowLatency: flowLatencyForNode is not null,
            hasUtilization: utilization.HasValue,
            hasThroughputRatio: throughputRatio.HasValue,
            queueOrigin: queueOrigin);

        return new NodeSnapshot
        {
            Id = node.Id,
            Kind = kind,
            LogicalType = logicalType,
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
                Parallelism = parallelism,
                ExternalDemand = externalDemand,
                MaxAttempts = maxAttempts,
                QueueLatencyStatus = queueLatencyStatus
            },
            SeriesMetadata = seriesMetadata,
            ByClass = ConvertClassMetrics(classAggregation.ByClass),
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
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, mergedWarnings),
            Aliases = node.Semantics?.Aliases,
            DispatchSchedule = dispatchSchedule,
            Sla = slaMetrics
        };
    }

    private const string slaStatusOk = "ok";
    private const string slaStatusUnavailable = "unavailable";
    private const string slaStatusNoEvents = "noEvents";

    private static IReadOnlyList<SlaSeriesDescriptor>? BuildSlaSeries(
        Node node,
        string? logicalType,
        NodeData data,
        StateRunContext context,
        int startBin,
        int count,
        DispatchScheduleDescriptor? dispatchSchedule)
    {
        var completion = ComputeCompletionSlaSeries(data.Arrivals, data.Served, dispatchSchedule, startBin, count);
        var backlogAge = BuildBacklogAgeSlaSeries(node, logicalType, data, context, startBin, count);
        var scheduleAdherence = ComputeScheduleAdherenceSeries(data.Served, dispatchSchedule, startBin, count);

        var items = new List<SlaSeriesDescriptor>(capacity: 3);
        if (completion is not null)
        {
            items.Add(completion);
        }
        if (backlogAge is not null)
        {
            items.Add(backlogAge);
        }
        if (scheduleAdherence is not null)
        {
            items.Add(scheduleAdherence);
        }

        return items.Count == 0 ? null : items;
    }

    private static IReadOnlyList<SlaMetricDescriptor>? BuildSlaMetrics(
        Node node,
        string? logicalType,
        NodeData data,
        StateRunContext context,
        int binIndex,
        DispatchScheduleDescriptor? dispatchSchedule)
    {
        var completionValue = ComputeCompletionSlaValue(data.Arrivals, data.Served, dispatchSchedule, binIndex);
        var completion = new SlaMetricDescriptor
        {
            Kind = "completion",
            Status = completionValue.HasValue ? slaStatusOk : slaStatusNoEvents,
            Threshold = null,
            Value = completionValue
        };

        var backlogAge = BuildBacklogAgeMetric(node, logicalType);

        var scheduleAdherence = BuildScheduleAdherenceMetric(data.Served, dispatchSchedule, binIndex);

        var items = new List<SlaMetricDescriptor>(capacity: 3)
        {
            completion
        };

        if (backlogAge is not null)
        {
            items.Add(backlogAge);
        }

        if (scheduleAdherence is not null)
        {
            items.Add(scheduleAdherence);
        }

        return items;
    }

    private static SlaSeriesDescriptor ComputeCompletionSlaSeries(
        double[] arrivals,
        double[] served,
        DispatchScheduleDescriptor? schedule,
        int startBin,
        int count)
    {
        var values = new double?[count];
        double? lastValue = null;
        var any = false;
        var hasSchedule = schedule is not null && schedule.PeriodBins > 0;

        for (var i = 0; i < count; i++)
        {
            var index = startBin + i;
            var ratio = ComputeThroughputRatio(arrivals[index], served[index]);
            if (hasSchedule)
            {
                if (DispatchScheduleProcessor.IsDispatchBin(index, schedule!.PeriodBins, schedule.PhaseOffset))
                {
                    if (ratio.HasValue)
                    {
                        lastValue = Normalize(ratio.Value);
                        any = true;
                    }
                }
            }
            else if (ratio.HasValue)
            {
                lastValue = Normalize(ratio.Value);
                any = true;
            }

            values[i] = lastValue;
        }

        return new SlaSeriesDescriptor
        {
            Kind = "completion",
            Status = any ? slaStatusOk : slaStatusNoEvents,
            Threshold = null,
            Values = values
        };
    }

    private static double? ComputeCompletionSlaValue(
        double[] arrivals,
        double[] served,
        DispatchScheduleDescriptor? schedule,
        int binIndex)
    {
        if (binIndex < 0 || binIndex >= arrivals.Length || binIndex >= served.Length)
        {
            return null;
        }

        var hasSchedule = schedule is not null && schedule.PeriodBins > 0;
        if (!hasSchedule)
        {
            var ratio = ComputeThroughputRatio(arrivals[binIndex], served[binIndex]);
            return ratio.HasValue ? Normalize(ratio.Value) : null;
        }

        double? lastValue = null;
        for (var i = 0; i <= binIndex; i++)
        {
            if (!DispatchScheduleProcessor.IsDispatchBin(i, schedule!.PeriodBins, schedule.PhaseOffset))
            {
                continue;
            }

            var ratio = ComputeThroughputRatio(arrivals[i], served[i]);
            if (ratio.HasValue)
            {
                lastValue = Normalize(ratio.Value);
            }
        }

        return lastValue;
    }

    private static SlaSeriesDescriptor? BuildBacklogAgeSlaSeries(
        Node node,
        string? logicalType,
        NodeData data,
        StateRunContext context,
        int startBin,
        int count)
    {
        var isQueueLike = IsQueueLikeKind(node.Kind) || IsQueueLikeKind(logicalType);
        if (!isQueueLike)
        {
            return null;
        }

        var values = new double?[count];
        return new SlaSeriesDescriptor
        {
            Kind = "backlogAge",
            Status = slaStatusUnavailable,
            Threshold = node.Semantics?.SlaMinutes,
            Values = values
        };
    }

    private static SlaMetricDescriptor? BuildBacklogAgeMetric(Node node, string? logicalType)
    {
        var isQueueLike = IsQueueLikeKind(node.Kind) || IsQueueLikeKind(logicalType);
        if (!isQueueLike)
        {
            return null;
        }

        return new SlaMetricDescriptor
        {
            Kind = "backlogAge",
            Status = slaStatusUnavailable,
            Threshold = node.Semantics?.SlaMinutes,
            Value = null
        };
    }

    private static SlaSeriesDescriptor? ComputeScheduleAdherenceSeries(
        double[] served,
        DispatchScheduleDescriptor? schedule,
        int startBin,
        int count)
    {
        if (schedule is null || schedule.PeriodBins <= 0)
        {
            return null;
        }

        var values = new double?[count];
        var any = false;

        for (var i = 0; i < count; i++)
        {
            var index = startBin + i;
            if (!DispatchScheduleProcessor.IsDispatchBin(index, schedule.PeriodBins, schedule.PhaseOffset))
            {
                values[i] = null;
                continue;
            }

            var servedValue = served[index];
            values[i] = servedValue > 0d ? 1d : 0d;
            any = true;
        }

        return new SlaSeriesDescriptor
        {
            Kind = "scheduleAdherence",
            Status = any ? slaStatusOk : slaStatusNoEvents,
            Threshold = 1d,
            Values = values
        };
    }

    private static SlaMetricDescriptor? BuildScheduleAdherenceMetric(
        double[] served,
        DispatchScheduleDescriptor? schedule,
        int binIndex)
    {
        if (schedule is null || schedule.PeriodBins <= 0)
        {
            return null;
        }

        if (!DispatchScheduleProcessor.IsDispatchBin(binIndex, schedule.PeriodBins, schedule.PhaseOffset))
        {
            return new SlaMetricDescriptor
            {
                Kind = "scheduleAdherence",
                Status = slaStatusNoEvents,
                Threshold = 1d,
                Value = null
            };
        }

        var servedValue = binIndex >= 0 && binIndex < served.Length ? served[binIndex] : 0d;
        return new SlaMetricDescriptor
        {
            Kind = "scheduleAdherence",
            Status = slaStatusOk,
            Threshold = 1d,
            Value = servedValue > 0d ? 1d : 0d
        };
    }

    private static IReadOnlyDictionary<string, ClassMetrics>? ConvertClassMetrics(IReadOnlyDictionary<string, ClassMetricsSnapshot> byClass)
    {
        if (byClass is null || byClass.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, ClassMetrics>(byClass.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in byClass)
        {
            result[kvp.Key] = new ClassMetrics
            {
                Arrivals = kvp.Value.Arrivals,
                Served = kvp.Value.Served,
                Errors = kvp.Value.Errors,
                Queue = kvp.Value.Queue,
                Capacity = kvp.Value.Capacity,
                ProcessingTimeMsSum = kvp.Value.ProcessingTimeMsSum,
                ServedCount = kvp.Value.ServedCount
            };
        }

        return result;
    }

    private static IReadOnlyList<ModeValidationWarning> MergeWarnings(
        IReadOnlyList<ModeValidationWarning> existing,
        IReadOnlyList<ModeValidationWarning> additional,
        string nodeId)
    {
        if (additional is null || additional.Count == 0)
        {
            return existing;
        }

        var merged = new List<ModeValidationWarning>(existing);
        foreach (var warning in additional)
        {
            merged.Add(new ModeValidationWarning
            {
                Code = warning.Code,
                Message = warning.Message,
                NodeId = nodeId
            });
        }

        return merged;
    }

    private static IReadOnlyList<ModeValidationWarning> MergeWarnings(
        IReadOnlyList<ModeValidationWarning> existing,
        IReadOnlyList<ModeValidationWarning> additional)
    {
        if (additional is null || additional.Count == 0)
        {
            return existing;
        }

        var merged = new List<ModeValidationWarning>(existing.Count + additional.Count);
        merged.AddRange(existing);
        merged.AddRange(additional);
        return merged;
    }

    private static IReadOnlyList<ModeValidationWarning> BuildBacklogWarnings(
        StateRunContext context,
        IEnumerable<Node> nodes,
        int startBin,
        int count)
    {
        var warnings = new List<ModeValidationWarning>();
        var endBin = startBin + count - 1;
        var binMinutes = context.Window.BinDuration.TotalMinutes;

        foreach (var node in nodes)
        {
            if (!IsQueueLikeKind(node.Kind))
            {
                continue;
            }

            if (!context.NodeData.TryGetValue(node.Id, out var data))
            {
                continue;
            }

            var growth = FindQueueGrowthStreak(data.QueueDepth, startBin, endBin);
            if (growth.HasValue && growth.Value.Length >= backlogGrowthStreakBins)
            {
                warnings.Add(new ModeValidationWarning
                {
                    Code = "backlog_growth_streak",
                    Message = $"Queue depth increased for {growth.Value.Length} consecutive bins (bins {growth.Value.Start}–{growth.Value.End}).",
                    NodeId = node.Id,
                    StartBin = growth.Value.Start,
                    EndBin = growth.Value.End,
                    Signal = "queueDepth"
                });
            }

            var overload = FindOverloadStreak(node.Semantics, data, startBin, endBin);
            if (overload.HasValue && overload.Value.Length >= backlogOverloadStreakBins)
            {
                warnings.Add(new ModeValidationWarning
                {
                    Code = "backlog_overload_ratio",
                    Message = $"Arrivals exceeded effective capacity for {overload.Value.Length} consecutive bins (bins {overload.Value.Start}–{overload.Value.End}).",
                    NodeId = node.Id,
                    StartBin = overload.Value.Start,
                    EndBin = overload.Value.End,
                    Signal = "overloadRatio"
                });
            }

            var ageRisk = FindAgeRiskStreak(data.QueueDepth, data.Served, binMinutes, node.Semantics?.SlaMinutes, startBin, endBin);
            if (ageRisk.HasValue && ageRisk.Value.Length >= backlogAgeRiskStreakBins)
            {
                warnings.Add(new ModeValidationWarning
                {
                    Code = "backlog_age_risk",
                    Message = $"Queue latency exceeded SLA for {ageRisk.Value.Length} consecutive bins (bins {ageRisk.Value.Start}–{ageRisk.Value.End}).",
                    NodeId = node.Id,
                    StartBin = ageRisk.Value.Start,
                    EndBin = ageRisk.Value.End,
                    Signal = "latencyMinutes"
                });
            }
        }

        return warnings;
    }

    private static (int Start, int End, int Length)? FindQueueGrowthStreak(
        double[]? queueSeries,
        int startBin,
        int endBin)
    {
        if (queueSeries is null || endBin - startBin < 1)
        {
            return null;
        }

        int? bestStart = null;
        int? bestEnd = null;
        var bestLength = 0;
        int? currentStart = null;
        var currentLength = 0;

        for (var i = startBin + 1; i <= endBin; i++)
        {
            var previous = GetOptionalValue(queueSeries, i - 1);
            var current = GetOptionalValue(queueSeries, i);

            if (previous.HasValue && current.HasValue && current.Value > previous.Value)
            {
                if (currentLength == 0)
                {
                    currentStart = i - 1;
                }

                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestEnd = i;
                }
            }
            else
            {
                currentLength = 0;
                currentStart = null;
            }
        }

        return bestLength > 0 && bestStart.HasValue && bestEnd.HasValue
            ? (bestStart.Value, bestEnd.Value, bestLength)
            : null;
    }

    private static (int Start, int End, int Length)? FindOverloadStreak(
        NodeSemantics? semantics,
        NodeData data,
        int startBin,
        int endBin)
    {
        if (data.Arrivals is null || data.Capacity is null)
        {
            return null;
        }

        return FindStreak(startBin, endBin, bin =>
        {
            var arrivals = GetOptionalValue(data.Arrivals, bin);
            var baseCapacity = GetOptionalValue(data.Capacity, bin);
            var capacity = semantics is null
                ? baseCapacity
                : GetEffectiveCapacity(semantics, data, bin, baseCapacity);
            if (!arrivals.HasValue || !capacity.HasValue || capacity.Value <= 0d)
            {
                return false;
            }

            var ratio = arrivals.Value / capacity.Value;
            return ratio > 1d;
        });
    }

    private static (int Start, int End, int Length)? FindAgeRiskStreak(
        double[]? queueSeries,
        double[]? servedSeries,
        double binMinutes,
        double? slaMinutes,
        int startBin,
        int endBin)
    {
        if (!slaMinutes.HasValue || slaMinutes.Value <= 0d || queueSeries is null || servedSeries is null || binMinutes <= 0d)
        {
            return null;
        }

        var threshold = slaMinutes.Value;
        return FindStreak(startBin, endBin, bin =>
        {
            var queue = GetOptionalValue(queueSeries, bin);
            var served = GetOptionalValue(servedSeries, bin);
            if (!queue.HasValue || !served.HasValue)
            {
                return false;
            }

            var latency = LatencyComputer.Calculate(queue.Value, served.Value, binMinutes);
            return latency.HasValue && latency.Value > threshold;
        });
    }

    private static (int Start, int End, int Length)? FindStreak(
        int startBin,
        int endBin,
        Func<int, bool> predicate)
    {
        int? bestStart = null;
        int? bestEnd = null;
        var bestLength = 0;
        int? currentStart = null;
        var currentLength = 0;

        for (var i = startBin; i <= endBin; i++)
        {
            if (predicate(i))
            {
                if (currentLength == 0)
                {
                    currentStart = i;
                }

                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestEnd = i;
                }
            }
            else
            {
                currentLength = 0;
                currentStart = null;
            }
        }

        return bestLength > 0 && bestStart.HasValue && bestEnd.HasValue
            ? (bestStart.Value, bestEnd.Value, bestLength)
            : null;
    }

    private static string? ResolveClassCoverage(IReadOnlyList<ClassCoverage> coverages)
    {
        if (coverages is null || coverages.Count == 0)
        {
            return null;
        }

        if (coverages.Any(c => c == ClassCoverage.Partial))
        {
            return "partial";
        }

        if (coverages.Any(c => c == ClassCoverage.Full))
        {
            return "full";
        }

        return "missing";
    }

    private static NodeData CreateEmptyNodeData(Node node, int bins)
    {
        double[] CreateSeries() => new double[bins];
        var kernelResult = RetryKernelPolicy.Apply(node.Semantics.RetryKernel?.ToArray());
        var parallelism = BuildParallelismSeries(node.Semantics.Parallelism, bins);
        var hasErrors = !string.IsNullOrWhiteSpace(node.Semantics.Errors);

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = CreateSeries(),
            Served = CreateSeries(),
            Errors = hasErrors ? CreateSeries() : null,
            Attempts = node.Semantics.Attempts != null ? CreateSeries() : null,
            Failures = node.Semantics.Failures != null ? CreateSeries() : null,
            ExhaustedFailures = node.Semantics.ExhaustedFailures != null ? CreateSeries() : null,
            RetryEcho = node.Semantics.RetryEcho != null ? CreateSeries() : null,
            RetryKernel = kernelResult.Kernel,
            ExternalDemand = node.Semantics.ExternalDemand != null ? CreateSeries() : null,
            QueueDepth = node.Semantics.QueueDepth != null ? CreateSeries() : null,
            Capacity = node.Semantics.Capacity != null ? CreateSeries() : null,
            Parallelism = parallelism,
            ProcessingTimeMsSum = node.Semantics.ProcessingTimeMsSum != null ? CreateSeries() : null,
            ServedCount = node.Semantics.ServedCount != null ? CreateSeries() : null,
            RetryBudgetRemaining = node.Semantics.RetryBudgetRemaining != null ? CreateSeries() : null
        };
    }

    private void LogClassCoverageDiagnostics(
        string runId,
        Node node,
        NodeData data,
        ClassAggregationResult aggregation)
    {
        if (aggregation.Coverage == ClassCoverage.Full)
        {
            return;
        }

        var classList = data.ByClass is null || data.ByClass.Count == 0
            ? "none"
            : string.Join(", ", data.ByClass.Keys);

        if (aggregation.Coverage == ClassCoverage.Missing)
        {
            logger.LogWarning(
                "Class coverage missing for node {NodeId} in run {RunId}. Semantics arrivals={Arrivals}, served={Served}, errors={Errors}. Available class series: {Classes}",
                node.Id,
                runId,
                node.Semantics?.Arrivals,
                node.Semantics?.Served,
                node.Semantics?.Errors,
                classList);
            return;
        }

        var warningCodes = aggregation.Warnings.Select(w => w.Code).ToArray();
        var warningMessages = aggregation.Warnings.Select(w => w.Message).ToArray();
        logger.LogWarning(
            "Class coverage partial for node {NodeId} in run {RunId}. WarningCodes={WarningCodes}. Messages={WarningMessages}. Available class series: {Classes}",
            node.Id,
            runId,
            warningCodes,
            warningMessages,
            classList);
    }

    private void LogClassCoverageSummary(
        string runId,
        string scope,
        int binIndex,
        IReadOnlyList<ClassCoverageDiagnostic> diagnostics)
    {
        var summary = diagnostics
            .Select(d =>
            {
                var codes = d.Warnings.Count == 0
                    ? "none"
                    : string.Join(",", d.Warnings.Select(w => w.Code));
                return $"{d.NodeId}:{d.Coverage}:{codes}";
            })
            .ToArray();

        logger.LogWarning(
            "Class coverage summary for run {RunId} ({Scope} bin={BinIndex}): {Summary}",
            runId,
            scope,
            binIndex,
            string.Join("; ", summary));
    }

    private sealed record ClassCoverageDiagnostic(string NodeId, ClassCoverage Coverage, IReadOnlyList<ModeValidationWarning> Warnings);

    private static NodeSeries BuildNodeSeries(Node node, NodeData data, StateRunContext context, int startBin, int count, IReadOnlyList<ModeValidationWarning> nodeWarnings, double?[]? flowLatencyForNode = null)
    {
        var kind = NormalizeKind(node.Kind);
        var logicalType = DetermineLogicalType(node, kind, context, out var serviceWithBufferDefinition);
        var dispatchSchedule = ResolveDispatchSchedule(node, context, serviceWithBufferDefinition);
        var arrivalsSlice = ExtractSlice(data.Arrivals, startBin, count);
        var servedSlice = ExtractSlice(data.Served, startBin, count);
        var errorsSlice = data.Errors != null ? ExtractSlice(data.Errors, startBin, count) : null;

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = arrivalsSlice,
            ["served"] = servedSlice
        };
        if (errorsSlice != null)
        {
            series["errors"] = errorsSlice;
        }

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
            if (failuresSlice != null)
            {
                series["failures"] = failuresSlice;
            }

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

        if (data.ProcessingTimeMsSum != null)
        {
            series["processingTimeMsSum"] = ExtractSlice(data.ProcessingTimeMsSum, startBin, count);
        }

        if (data.ServedCount != null)
        {
            series["servedCount"] = ExtractSlice(data.ServedCount, startBin, count);
        }

        double?[]? utilizationSeries = null;
        if (data.Capacity != null)
        {
            series["capacity"] = ExtractSlice(data.Capacity, startBin, count);
            utilizationSeries = ComputeUtilizationSeries(data, node.Semantics, startBin, count);
            if (utilizationSeries != null)
            {
                series["utilization"] = utilizationSeries;
            }
        }

        if (data.Parallelism != null)
        {
            series["parallelism"] = ExtractSlice(data.Parallelism, startBin, count);
        }

        QueueLatencyStatusDescriptor?[]? queueLatencyStatuses = null;
        double?[]? latencySeries = null;

        if (IsQueueLikeKind(kind) && data.QueueDepth != null)
        {
            latencySeries = ComputeLatencySeries(data, context.Window, startBin, count);
            if (latencySeries != null)
            {
                series["latencyMinutes"] = latencySeries;
            }

            queueLatencyStatuses = ComputeQueueLatencyStatusSeries(
                data.QueueDepth,
                data.Served,
                dispatchSchedule,
                startBin,
                count);
        }

        var throughputSeries = ComputeThroughputSeries(data, startBin, count);
        if (throughputSeries != null)
        {
            series["throughputRatio"] = throughputSeries;
        }

        double?[]? serviceTimeSeries = null;
        if (string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(logicalType, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
        {
            serviceTimeSeries = ComputeServiceTimeSeries(data, startBin, count);
            if (serviceTimeSeries != null)
            {
                series["serviceTimeMs"] = serviceTimeSeries;
            }
        }

        double?[]? flowLatencySlice = null;
        if (flowLatencyForNode != null)
        {
            flowLatencySlice = ExtractSlice(flowLatencyForNode, startBin, count);
            series["flowLatencyMs"] = flowLatencySlice;
        }

        if (data.Values is not null)
        {
            var valuesSlice = ExtractSlice(data.Values, startBin, count);
            series["values"] = valuesSlice;
            series[$"series:{node.Id}"] = valuesSlice;
        }

        var slaSeries = BuildSlaSeries(node, logicalType, data, context, startBin, count, dispatchSchedule);
        var queueOrigin = data.QueueDepth is null ? null : ResolveQueueSeriesOrigin(node, context);
        var seriesMetadata = BuildDerivedSeriesMetadata(
            hasLatency: latencySeries is not null,
            hasServiceTime: serviceTimeSeries is not null,
            hasFlowLatency: flowLatencySlice is not null,
            hasUtilization: utilizationSeries is not null,
            hasThroughputRatio: throughputSeries is not null,
            queueOrigin: queueOrigin);

        return new NodeSeries
        {
            Id = node.Id,
            Kind = kind,
            LogicalType = logicalType,
            Series = series,
            SeriesMetadata = seriesMetadata,
            ByClass = BuildClassSeries(data, startBin, count),
            Telemetry = BuildTelemetryInfo(node, context.ManifestMetadata, nodeWarnings),
            Aliases = node.Semantics?.Aliases,
            DispatchSchedule = dispatchSchedule,
            QueueLatencyStatus = queueLatencyStatuses,
            Sla = slaSeries
        };
    }

    private static double[]? BuildParallelismSeries(object? parallelism, int bins)
    {
        var scalar = ParseParallelismScalar(parallelism);
        return scalar.HasValue ? CreateConstantSeries(scalar.Value, bins) : null;
    }

    private static IDictionary<string, IDictionary<string, double?[]>>? BuildClassSeries(NodeData data, int startBin, int count)
    {
        if (data.ByClass is null || data.ByClass.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, IDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in data.ByClass)
        {
            var classId = string.IsNullOrWhiteSpace(kvp.Key) ? "*" : kvp.Key.Trim();
            var classData = kvp.Value;
            if (classData is null)
            {
                continue;
            }

            var classSeries = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            AddSeriesIfAvailable(classSeries, "arrivals", classData.Arrivals);
            AddSeriesIfAvailable(classSeries, "served", classData.Served);
            AddSeriesIfAvailable(classSeries, "errors", classData.Errors);
            AddSeriesIfAvailable(classSeries, "queue", classData.QueueDepth);
            AddSeriesIfAvailable(classSeries, "capacity", classData.Capacity);
            AddSeriesIfAvailable(classSeries, "processingTimeMsSum", classData.ProcessingTimeMsSum);
            AddSeriesIfAvailable(classSeries, "servedCount", classData.ServedCount);

            if (classSeries.Count > 0)
            {
                result[classId] = classSeries;
            }
        }

        return result.Count == 0 ? null : result;

        void AddSeriesIfAvailable(IDictionary<string, double?[]> target, string key, double[]? series)
        {
            if (series is null)
            {
                return;
            }

            var slice = ExtractSlice(series, startBin, count);
            var hasValue = false;
            for (var i = 0; i < slice.Length; i++)
            {
                if (slice[i].HasValue)
                {
                    hasValue = true;
                    break;
                }
            }

            if (hasValue)
            {
                target[key] = slice;
            }
        }
    }

    private static IReadOnlyDictionary<string, SeriesSemanticsMetadata>? BuildDerivedSeriesMetadata(
        bool hasLatency,
        bool hasServiceTime,
        bool hasFlowLatency,
        bool hasUtilization,
        bool hasThroughputRatio,
        string? queueOrigin)
    {
        Dictionary<string, SeriesSemanticsMetadata>? metadata = null;

        if (hasLatency)
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["latencyMinutes"] = new SeriesSemanticsMetadata
            {
                Aggregation = "avg",
                Origin = "derived"
            };
        }

        if (hasServiceTime)
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["serviceTimeMs"] = new SeriesSemanticsMetadata
            {
                Aggregation = "avg",
                Origin = "derived"
            };
        }

        if (hasFlowLatency)
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["flowLatencyMs"] = new SeriesSemanticsMetadata
            {
                Aggregation = "avg",
                Origin = "derived"
            };
        }

        if (hasUtilization)
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["utilization"] = new SeriesSemanticsMetadata
            {
                Aggregation = "unknown",
                Origin = "derived"
            };
        }

        if (hasThroughputRatio)
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["throughputRatio"] = new SeriesSemanticsMetadata
            {
                Aggregation = "unknown",
                Origin = "derived"
            };
        }

        if (!string.IsNullOrWhiteSpace(queueOrigin))
        {
            metadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            metadata["queue"] = new SeriesSemanticsMetadata
            {
                Origin = queueOrigin
            };
        }

        return metadata;
    }

    private static SeriesSemanticsMetadata? ResolveEdgeSeriesMetadata(string metric, string origin)
    {
        if (string.IsNullOrWhiteSpace(metric))
        {
            return null;
        }

        var normalized = metric.Trim();
        var aggregation = string.Equals(normalized, "retryRate", StringComparison.OrdinalIgnoreCase)
            ? "unknown"
            : "sum";

        return normalized.ToLowerInvariant() switch
        {
            "flowvolume" => new SeriesSemanticsMetadata { Aggregation = aggregation, Origin = origin },
            "attemptsvolume" => new SeriesSemanticsMetadata { Aggregation = aggregation, Origin = origin },
            "failuresvolume" => new SeriesSemanticsMetadata { Aggregation = aggregation, Origin = origin },
            "retryvolume" => new SeriesSemanticsMetadata { Aggregation = aggregation, Origin = origin },
            "retryrate" => new SeriesSemanticsMetadata { Aggregation = aggregation, Origin = origin },
            _ => null
        };
    }

    private static void TryAddEdgeSeriesMetadata(
        Dictionary<string, SeriesSemanticsMetadata> metadata,
        string metric,
        string origin)
    {
        var entry = ResolveEdgeSeriesMetadata(metric, origin);
        if (entry is null)
        {
            return;
        }

        metadata[metric] = entry;
    }

    private static string? ResolveQueueSeriesOrigin(Node node, StateRunContext context)
    {
        if (node.Semantics is null || string.IsNullOrWhiteSpace(node.Semantics.QueueDepth))
        {
            return null;
        }

        var raw = node.Semantics.QueueDepth.Trim();
        if (raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return "explicit";
        }

        var seriesId = ExtractNodeId(raw);
        if (string.Equals(seriesId, "self", StringComparison.OrdinalIgnoreCase))
        {
            return "derived";
        }

        if (context.ModelNodes.TryGetValue(seriesId, out var definition))
        {
            if (definition.Metadata is not null &&
                definition.Metadata.TryGetValue("series.origin", out var origin) &&
                !string.IsNullOrWhiteSpace(origin))
            {
                return origin.Trim().ToLowerInvariant();
            }

            return "explicit";
        }

        return "derived";
    }

    private static string ExtractNodeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf(':');
        return separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
    }

    private void InspectTelemetrySources(
        IEnumerable<Node> nodes,
        RunManifestMetadata manifestMetadata,
        string modelDirectory,
        ICollection<ModeValidationWarning> warnings,
        IDictionary<string, List<ModeValidationWarning>> nodeWarnings,
        ref bool appendedGlobalMissingWarning)
    {
        if (manifestMetadata.NodeSources.Count == 0 && manifestMetadata.TelemetrySources.Count == 0)
        {
            return;
        }

        foreach (var node in nodes)
        {
            var missingSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in EnumerateTelemetrySources(node, manifestMetadata))
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                string resolvedPath;
                try
                {
                    resolvedPath = UriResolver.ResolveFilePath(source, modelDirectory);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Telemetry source could not be resolved for node {NodeId}", node.Id);
                    missingSources.Add(source);
                    continue;
                }

                if (!File.Exists(resolvedPath))
                {
                    logger.LogWarning("Telemetry source missing for node {NodeId}: {Source}", node.Id, source);
                    missingSources.Add(source);
                }
            }

            if (missingSources.Count == 0)
            {
                continue;
            }

            foreach (var source in missingSources)
            {
                AppendNodeWarning(
                    nodeWarnings,
                    node.Id,
                    "telemetry_sources_unresolved",
                    $"Telemetry source '{source}' could not be resolved for node '{node.Id}'.");
            }

            if (!appendedGlobalMissingWarning)
            {
                warnings.Add(new ModeValidationWarning
                {
                    Code = "telemetry_sources_missing",
                    Message = "One or more telemetry sources could not be resolved for this run."
                });
                appendedGlobalMissingWarning = true;
            }
        }
    }

    private static IEnumerable<string?> EnumerateTelemetrySources(Node node, RunManifestMetadata manifestMetadata)
    {
        IEnumerable<string?> series = new[]
        {
            node.Semantics.Arrivals,
            node.Semantics.Served,
            node.Semantics.Errors,
            node.Semantics.Attempts,
            node.Semantics.Failures,
            node.Semantics.ExhaustedFailures,
            node.Semantics.RetryEcho,
            node.Semantics.RetryBudgetRemaining,
            node.Semantics.ExternalDemand,
            node.Semantics.QueueDepth,
            node.Semantics.Capacity,
            node.Semantics.ProcessingTimeMsSum,
            node.Semantics.ServedCount
        };

        foreach (var value in series)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var identifier = value.Trim();
            if (manifestMetadata.NodeSources.TryGetValue(identifier, out var source) && !string.IsNullOrWhiteSpace(source))
            {
                yield return source;
                continue;
            }

            if (identifier.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                yield return identifier;
            }
        }
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

    private static double?[]? ComputeUtilizationSeries(NodeData data, NodeSemantics semantics, int start, int count)
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
            var effectiveCapacity = GetEffectiveCapacity(semantics, data, start + i, capacity);
            var raw = UtilizationComputer.Calculate(served, effectiveCapacity);
            result[i] = raw.HasValue ? Normalize(raw.Value) : null;
        }

        return result;
    }

    private static double? GetEffectiveCapacity(NodeSemantics semantics, NodeData data, int index, double? capacity = null)
    {
        var baseCapacity = capacity ?? GetOptionalValue(data.Capacity, index);
        if (!baseCapacity.HasValue)
        {
            return null;
        }

        var parallelism = GetParallelismValue(semantics, data, index);
        if (!parallelism.HasValue)
        {
            return baseCapacity.Value;
        }

        if (!double.IsFinite(parallelism.Value) || parallelism.Value <= 0d)
        {
            return baseCapacity.Value;
        }

        return baseCapacity.Value * parallelism.Value;
    }

    private static double? GetParallelismValue(NodeSemantics semantics, NodeData data, int index)
    {
        if (data.Parallelism != null && index >= 0 && index < data.Parallelism.Length)
        {
            var value = data.Parallelism[index];
            return double.IsFinite(value) ? value : null;
        }

        return ParseParallelismScalar(semantics.Parallelism);
    }

    private static double? ParseParallelismScalar(object? value)
    {
        if (value is null)
        {
            return null;
        }

        double parsed;
        switch (value)
        {
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric):
                parsed = numeric;
                break;
            case IConvertible:
                parsed = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            default:
                return null;
        }

        if (!double.IsFinite(parsed) || parsed <= 0d)
        {
            return null;
        }

        return parsed;
    }

    private static double[] CreateConstantSeries(double value, int bins)
    {
        var series = new double[bins];
        for (var i = 0; i < bins; i++)
        {
            series[i] = value;
        }

        return series;
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

    private static QueueLatencyStatusDescriptor?[]? ComputeQueueLatencyStatusSeries(
        double[]? queueSeries,
        double[]? servedSeries,
        DispatchScheduleDescriptor? schedule,
        int startBin,
        int count)
    {
        if (schedule is null || schedule.PeriodBins <= 0 || queueSeries is null)
        {
            return null;
        }

        var result = new QueueLatencyStatusDescriptor?[count];
        var hasStatus = false;

        for (var offset = 0; offset < count; offset++)
        {
            var absoluteBin = startBin + offset;
            var queue = GetOptionalValue(queueSeries, absoluteBin);
            var served = GetOptionalValue(servedSeries, absoluteBin);
            var status = DetermineQueueLatencyStatus(queue, served, schedule, absoluteBin);
            result[offset] = status;
            if (status is not null)
            {
                hasStatus = true;
            }
        }

        return hasStatus ? result : null;
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

        if (count <= 0)
        {
            return null;
        }

        var value = sum / count;
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
        var incomingEdges = BuildIncomingFlowEdges(context);

        foreach (var node in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(node.Id, out var data))
            {
                continue;
            }

            var baseSeries = new double?[bins];
            var kind = NormalizeKind(node.Kind);
            var isQueue = IsQueueLikeKind(kind);
            var isServiceWithBuffer = string.Equals(kind, "servicewithbuffer", StringComparison.OrdinalIgnoreCase);
            var isService = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase) || isServiceWithBuffer;
            var isSink = string.Equals(kind, "sink", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(node.NodeRole, "sink", StringComparison.OrdinalIgnoreCase);
            var hasArrivalsSemantics = !string.IsNullOrWhiteSpace(node.Semantics?.Arrivals);
            var hasServedSemantics = !isSink && !string.IsNullOrWhiteSpace(node.Semantics?.Served);

            for (var i = 0; i < bins; i++)
            {
                double? baseValue = null;
                double? queueLatencyMs = null;
                if (isQueue)
                {
                    var queue = GetOptionalValue(data.QueueDepth, i);
                    var served = GetOptionalValue(data.Served, i);
                    if (queue.HasValue && served.HasValue)
                    {
                        var latMin = LatencyComputer.Calculate(queue.Value, served.Value, context.Window.BinDuration.TotalMinutes);
                        if (latMin.HasValue && double.IsFinite(latMin.Value))
                        {
                            queueLatencyMs = latMin.Value * 60000d;
                        }
                    }
                }
                double? serviceTimeMs = null;
                if (isService)
                {
                    serviceTimeMs = ComputeServiceTimeValue(data, i);
                }

                if (isServiceWithBuffer)
                {
                    if (queueLatencyMs.HasValue && serviceTimeMs.HasValue)
                    {
                        baseValue = queueLatencyMs.Value + serviceTimeMs.Value;
                    }
                    else
                    {
                        baseValue = queueLatencyMs ?? serviceTimeMs;
                    }
                }
                else if (isQueue)
                {
                    baseValue = queueLatencyMs;
                }
                else if (isService)
                {
                    baseValue = serviceTimeMs;
                }

                baseSeries[i] = baseValue;
            }

            double?[]? upstream = null;
            if (incomingEdges.TryGetValue(node.Id, out var preds) && preds.Count > 0)
            {
                upstream = new double?[bins];
                for (var i = 0; i < bins; i++)
                {
                    double totalFlow = 0d;
                    double weightedLatency = 0d;

                    foreach (var (predId, flow) in preds)
                    {
                        if (!result.TryGetValue(predId, out var predSeries))
                        {
                            continue;
                        }

                        if (i < 0 || i >= predSeries.Length)
                        {
                            continue;
                        }

                        var candidateLatency = predSeries[i];
                        if (!candidateLatency.HasValue || !double.IsFinite(candidateLatency.Value))
                        {
                            continue;
                        }

                        if (flow is null || i < 0 || i >= flow.Length)
                        {
                            continue;
                        }

                        var flowValue = flow[i];
                        if (!flowValue.HasValue || !double.IsFinite(flowValue.Value) || flowValue.Value <= 0d)
                        {
                            continue;
                        }

                        totalFlow += flowValue.Value;
                        weightedLatency += flowValue.Value * candidateLatency.Value;
                    }

                    if (totalFlow > 0d)
                    {
                        upstream[i] = Normalize(weightedLatency / totalFlow);
                    }
                }
            }

            var combined = new double?[bins];
            for (var i = 0; i < bins; i++)
            {
                var baseVal = baseSeries[i];
                var upVal = upstream?[i];
                if (isSink && hasArrivalsSemantics)
                {
                    var arrivals = GetOptionalValue(data.Arrivals, i);
                    if (!arrivals.HasValue || !double.IsFinite(arrivals.Value) || arrivals.Value <= 0)
                    {
                        combined[i] = null;
                        continue;
                    }
                }
                else if (hasServedSemantics)
                {
                    var served = GetOptionalValue(data.Served, i);
                    if (!served.HasValue || !double.IsFinite(served.Value) || served.Value <= 0)
                    {
                        combined[i] = null;
                        continue;
                    }
                }

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

    private static Dictionary<string, List<(string Pred, double?[] Flow)>> BuildIncomingFlowEdges(StateRunContext context)
    {
        var incoming = new Dictionary<string, List<(string Pred, double?[] Flow)>>(StringComparer.OrdinalIgnoreCase);
        if (context.Topology.Edges is null || context.Topology.Edges.Count == 0)
        {
            return incoming;
        }

        var edgeSeries = BuildEdgeFlowVolumeLookup(context);
        if (edgeSeries.Count == 0)
        {
            return incoming;
        }

        foreach (var edge in context.Topology.Edges)
        {
            var edgeType = string.IsNullOrWhiteSpace(edge.EdgeType)
                ? "throughput"
                : NormalizeEdgeType(edge.EdgeType);
            if (!string.Equals(edgeType, "throughput", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var edgeId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}"
                : edge.Id!;

            if (!edgeSeries.TryGetValue(edgeId, out var flow))
            {
                continue;
            }

            var sourceId = ExtractNodeReference(edge.Source);
            var targetId = ExtractNodeReference(edge.Target);
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            if (!incoming.TryGetValue(targetId, out var list))
            {
                list = new List<(string Pred, double?[] Flow)>();
                incoming[targetId] = list;
            }

            list.Add((sourceId, flow));
        }

        return incoming;
    }

    private static Dictionary<string, double?[]> BuildEdgeFlowVolumeLookup(StateRunContext context)
    {
        var lookup = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
        if (context.Topology.Edges is null || context.Topology.Edges.Count == 0)
        {
            return lookup;
        }

        var edgeDefinitions = BuildEdgeDefinitionLookup(context.Topology.Edges);
        if (edgeDefinitions.Count == 0)
        {
            return lookup;
        }

        foreach (var series in context.SeriesIndex.Series)
        {
            if (!IsEdgeSeriesMetadata(series))
            {
                continue;
            }

            if (!TryParseEdgeSeriesId(series.Id, out var edgeToken, out var metric))
            {
                continue;
            }

            if (!string.Equals(metric, "flowVolume", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsDefaultSeriesClass(series.Class))
            {
                continue;
            }

            if (!edgeDefinitions.TryGetValue(edgeToken, out var definition))
            {
                continue;
            }

            if (lookup.ContainsKey(definition.RawId))
            {
                continue;
            }

            var seriesPath = Path.Combine(context.RunDirectory, series.Path);
            if (!File.Exists(seriesPath))
            {
                continue;
            }

            var values = CsvReader.ReadTimeSeries(seriesPath, context.Window.Bins);
            var normalized = new double?[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                normalized[i] = Normalize(values[i]);
            }

            lookup[definition.RawId] = normalized;
        }

        return lookup;
    }

    private static bool IsDefaultSeriesClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<EdgeSeries> BuildEdgeSeries(StateRunContext context, int startBin, int count)
    {
        if (context.Topology.Edges is null || context.Topology.Edges.Count == 0)
        {
            return Array.Empty<EdgeSeries>();
        }

        var artifactEdges = BuildArtifactEdgeSeries(context, startBin, count);
        var retryEdges = BuildRetryEdgeSeries(context, startBin, count);

        if (artifactEdges.Count == 0)
        {
            return retryEdges;
        }

        if (retryEdges.Count == 0)
        {
            return artifactEdges;
        }

        var merged = artifactEdges.ToDictionary(edge => edge.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var retryEdge in retryEdges)
        {
            if (merged.TryGetValue(retryEdge.Id, out var existing))
            {
                merged[retryEdge.Id] = MergeEdgeSeries(existing, retryEdge);
                continue;
            }

            merged[retryEdge.Id] = retryEdge;
        }

        var edges = merged.Values.ToList();
        AddRetryVolumeToThroughputEdges(context, edges, startBin, count);
        return edges;
    }

    private static IReadOnlyList<EdgeSeries> ApplyEdgeFilters(
        IReadOnlyList<EdgeSeries> edges,
        EdgeQueryFilter? filter)
    {
        if (filter is null || edges.Count == 0)
        {
            return edges;
        }

        var edgeIds = filter.EdgeIds is { Count: > 0 }
            ? new HashSet<string>(filter.EdgeIds, StringComparer.OrdinalIgnoreCase)
            : null;
        var edgeMetrics = filter.EdgeMetrics is { Count: > 0 }
            ? new HashSet<string>(filter.EdgeMetrics, StringComparer.OrdinalIgnoreCase)
            : null;
        var classIds = filter.ClassIds is { Count: > 0 }
            ? new HashSet<string>(filter.ClassIds, StringComparer.OrdinalIgnoreCase)
            : null;

        var filtered = new List<EdgeSeries>(edges.Count);
        foreach (var edge in edges)
        {
            if (edgeIds is not null && !edgeIds.Contains(edge.Id))
            {
                continue;
            }

            var series = edge.Series;
            if (edgeMetrics is not null)
            {
                series = series
                    .Where(kvp => edgeMetrics.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            IReadOnlyDictionary<string, SeriesSemanticsMetadata>? seriesMetadata = edge.SeriesMetadata;
            if (edgeMetrics is not null && seriesMetadata is not null)
            {
                seriesMetadata = seriesMetadata
                    .Where(kvp => edgeMetrics.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            IDictionary<string, IDictionary<string, double?[]>>? byClass = edge.ByClass;
            if (classIds is not null && byClass is not null)
            {
                byClass = byClass
                    .Where(kvp => classIds.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            if (edgeMetrics is not null && byClass is not null)
            {
                var filteredByClass = new Dictionary<string, IDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (classId, metrics) in byClass)
                {
                    var filteredMetrics = metrics
                        .Where(kvp => edgeMetrics.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                    if (filteredMetrics.Count > 0)
                    {
                        filteredByClass[classId] = filteredMetrics;
                    }
                }

                byClass = filteredByClass.Count == 0 ? null : filteredByClass;
            }

            if (byClass is not null && byClass.Count == 0)
            {
                byClass = null;
            }

            if (series.Count == 0 && byClass is null)
            {
                continue;
            }

            filtered.Add(new EdgeSeries
            {
                Id = edge.Id,
                From = edge.From,
                To = edge.To,
                EdgeType = edge.EdgeType,
                Field = edge.Field,
                Multiplier = edge.Multiplier,
                Lag = edge.Lag,
                Series = series,
                SeriesMetadata = seriesMetadata,
                ByClass = byClass
            });
        }

        return filtered;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StateWarning>> ApplyEdgeWarningFilter(
        IReadOnlyDictionary<string, IReadOnlyList<StateWarning>> edgeWarnings,
        EdgeQueryFilter? filter)
    {
        if (filter?.EdgeIds is not { Count: > 0 } || edgeWarnings.Count == 0)
        {
            return edgeWarnings;
        }

        var edgeIds = new HashSet<string>(filter.EdgeIds, StringComparer.OrdinalIgnoreCase);
        return edgeWarnings
            .Where(kvp => edgeIds.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<EdgeSeries> BuildArtifactEdgeSeries(StateRunContext context, int startBin, int count)
    {
        var edgeSeriesEntries = context.SeriesIndex.Series
            .Where(series => IsEdgeSeriesMetadata(series))
            .ToList();

        if (edgeSeriesEntries.Count == 0)
        {
            return Array.Empty<EdgeSeries>();
        }

        var edgeDefinitions = BuildEdgeDefinitionLookup(context.Topology.Edges);
        if (edgeDefinitions.Count == 0)
        {
            return Array.Empty<EdgeSeries>();
        }

        var bins = context.Window.Bins;
        var builders = new Dictionary<string, EdgeSeriesBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in edgeSeriesEntries)
        {
            if (!TryParseEdgeSeriesId(series.Id, out var edgeToken, out var metric))
            {
                continue;
            }

            if (!edgeDefinitions.TryGetValue(edgeToken, out var definition))
            {
                continue;
            }

            var seriesPath = Path.Combine(context.RunDirectory, series.Path);
            if (!File.Exists(seriesPath))
            {
                continue;
            }

            var values = CsvReader.ReadTimeSeries(seriesPath, bins);
            var slice = ExtractSlice(values, startBin, count);

            if (!builders.TryGetValue(edgeToken, out var builder))
            {
                builder = new EdgeSeriesBuilder(definition);
                builders[edgeToken] = builder;
            }

            builder.AddSeries(series.Class, metric, slice, "explicit");
        }

        return builders.Values.Select(builder => builder.Build()).ToList();
    }

    private static void AddRetryVolumeToThroughputEdges(
        StateRunContext context,
        List<EdgeSeries> edges,
        int startBin,
        int count)
    {
        if (edges.Count == 0 || context.Topology.Edges is null || context.Topology.Edges.Count == 0)
        {
            return;
        }

        var edgeSeriesById = edges.ToDictionary(edge => edge.Id, StringComparer.OrdinalIgnoreCase);
        var throughputIncoming = new Dictionary<string, List<EdgeSeries>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in context.Topology.Edges)
        {
            var edgeType = string.IsNullOrWhiteSpace(edge.EdgeType)
                ? "throughput"
                : edge.EdgeType.Trim().ToLowerInvariant();
            if (!string.Equals(edgeType, "throughput", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var edgeId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}"
                : edge.Id!;
            if (!edgeSeriesById.TryGetValue(edgeId, out var series))
            {
                continue;
            }

            var targetId = ExtractNodeReference(edge.Target);
            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            if (!throughputIncoming.TryGetValue(targetId, out var list))
            {
                list = new List<EdgeSeries>();
                throughputIncoming[targetId] = list;
            }

            if (list.Contains(series))
            {
                continue;
            }

            list.Add(series);
        }

        foreach (var (targetId, incomingEdges) in throughputIncoming)
        {
            if (!context.NodeData.TryGetValue(targetId, out var nodeData))
            {
                continue;
            }

            var retrySeries = ComputeRetryEchoSeries(nodeData, startBin, count, allowDerived: true);
            if (retrySeries is null)
            {
                continue;
            }

            if (incomingEdges.Count == 0)
            {
                continue;
            }

            var edgeInfos = new List<(EdgeSeries Edge, double?[]? Flow, double?[] Retry)>();
            foreach (var edge in incomingEdges)
            {
                if (edge.Series.ContainsKey("retryVolume"))
                {
                    continue;
                }

                var retryValues = new double?[count];
                edge.Series["retryVolume"] = retryValues;
                var seriesMetadata = edge.SeriesMetadata as Dictionary<string, SeriesSemanticsMetadata>;
                if (seriesMetadata is null)
                {
                    seriesMetadata = edge.SeriesMetadata is null
                        ? new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, SeriesSemanticsMetadata>(edge.SeriesMetadata, StringComparer.OrdinalIgnoreCase);
                    edge.SeriesMetadata = seriesMetadata;
                }
                TryAddEdgeSeriesMetadata(seriesMetadata, "retryVolume", "derived");

                edgeInfos.Add((
                    edge,
                    edge.Series.TryGetValue("flowVolume", out var values) ? values : null,
                    retryValues));
            }

            if (edgeInfos.Count == 0)
            {
                continue;
            }
            var splitCount = edgeInfos.Count;

            for (var i = 0; i < count; i++)
            {
                var retryValue = retrySeries[i];
                if (!retryValue.HasValue || !double.IsFinite(retryValue.Value) || retryValue.Value <= 0)
                {
                    continue;
                }

                var totalFlow = 0d;
                foreach (var info in edgeInfos)
                {
                    if (info.Flow is null)
                    {
                        continue;
                    }

                    var flowValue = info.Flow[i];
                    if (flowValue.HasValue && double.IsFinite(flowValue.Value) && flowValue.Value > 0)
                    {
                        totalFlow += flowValue.Value;
                    }
                }

                if (totalFlow <= 0)
                {
                    var perEdge = Normalize(retryValue.Value / splitCount);
                    foreach (var info in edgeInfos)
                    {
                        info.Retry[i] = perEdge;
                    }

                    continue;
                }

                foreach (var info in edgeInfos)
                {
                    if (info.Flow is null)
                    {
                        continue;
                    }

                    var flowValue = info.Flow[i];
                    if (!flowValue.HasValue || !double.IsFinite(flowValue.Value) || flowValue.Value <= 0)
                    {
                        continue;
                    }

                    info.Retry[i] = Normalize(retryValue.Value * (flowValue.Value / totalFlow));
                }
            }
        }
    }

    private static IReadOnlyList<EdgeSeries> BuildRetryEdgeSeries(StateRunContext context, int startBin, int count)
    {
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
            var failuresSeries = string.Equals(field, "exhaustedfailures", StringComparison.OrdinalIgnoreCase)
                ? ToNullableSeries(sourceData.ExhaustedFailures)
                : ComputeFailureSeries(sourceData, 0, totalBins);
            if (attemptsSeries is null && failuresSeries is null)
            {
                continue;
            }

            var lag = Math.Max(0, edge.Lag ?? 0);
            var multiplier = NormalizeMultiplier(edge.Multiplier ?? edge.Weight);
            var attemptsVolume = new double?[count];
            var failuresVolume = new double?[count];
            var retryRate = new double?[count];

            for (var i = 0; i < count; i++)
            {
                var sourceIndex = startBin + i - lag;
                if (sourceIndex < 0 || sourceIndex >= totalBins)
                {
                    attemptsVolume[i] = null;
                    failuresVolume[i] = null;
                    retryRate[i] = null;
                    continue;
                }

                var attempt = Sample(attemptsSeries, sourceIndex);
                var failure = Sample(failuresSeries, sourceIndex);

                attemptsVolume[i] = attempt.HasValue ? Normalize(attempt.Value * multiplier) : null;
                failuresVolume[i] = failure.HasValue ? Normalize(failure.Value * multiplier) : null;
                retryRate[i] = ComputeRetryRate(attempt, failure);
            }

            var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["attemptsVolume"] = attemptsVolume,
                ["failuresVolume"] = failuresVolume,
                ["retryRate"] = retryRate
            };
            var seriesMetadata = new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            TryAddEdgeSeriesMetadata(seriesMetadata, "attemptsVolume", "derived");
            TryAddEdgeSeriesMetadata(seriesMetadata, "failuresVolume", "derived");
            TryAddEdgeSeriesMetadata(seriesMetadata, "retryRate", "derived");

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
                Series = series,
                SeriesMetadata = seriesMetadata.Count == 0 ? null : seriesMetadata
            });
        }

        return result;
    }

    private static bool IsEdgeSeriesMetadata(SeriesMetadata series)
    {
        return string.Equals(series.Kind, "edge", StringComparison.OrdinalIgnoreCase)
            || series.Id.StartsWith("edge_", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, EdgeDefinitionInfo> BuildEdgeDefinitionLookup(IReadOnlyList<Edge> edges)
    {
        var lookup = new Dictionary<string, EdgeDefinitionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            var rawId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}"
                : edge.Id!;
            var normalized = NormalizeEdgeToken(rawId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!lookup.ContainsKey(normalized))
            {
                lookup[normalized] = new EdgeDefinitionInfo(rawId, edge);
            }
        }

        return lookup;
    }

    private static bool TryParseEdgeSeriesId(string seriesId, out string edgeToken, out string metric)
    {
        edgeToken = string.Empty;
        metric = string.Empty;

        if (string.IsNullOrWhiteSpace(seriesId))
        {
            return false;
        }

        var atIndex = seriesId.IndexOf('@');
        if (atIndex <= 0)
        {
            return false;
        }

        var core = seriesId[..atIndex];
        if (!core.StartsWith("edge_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lastUnderscore = core.LastIndexOf('_');
        if (lastUnderscore <= "edge_".Length)
        {
            return false;
        }

        edgeToken = core["edge_".Length..lastUnderscore];
        metric = core[(lastUnderscore + 1)..];

        return !string.IsNullOrWhiteSpace(edgeToken) && !string.IsNullOrWhiteSpace(metric);
    }

    private static string NormalizeEdgeToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static EdgeSeries MergeEdgeSeries(EdgeSeries target, EdgeSeries source)
    {
        var mergedSeries = new Dictionary<string, double?[]>(target.Series, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, series) in source.Series)
        {
            mergedSeries[key] = series;
        }

        Dictionary<string, IDictionary<string, double?[]>>? mergedByClass = null;
        if (target.ByClass is not null)
        {
            mergedByClass = new Dictionary<string, IDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, metrics) in target.ByClass)
            {
                mergedByClass[classId] = new Dictionary<string, double?[]>(metrics, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (source.ByClass is not null)
        {
            mergedByClass ??= new Dictionary<string, IDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, metrics) in source.ByClass)
            {
                if (!mergedByClass.TryGetValue(classId, out var targetMetrics))
                {
                    targetMetrics = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
                    mergedByClass[classId] = targetMetrics;
                }

                foreach (var (metricKey, metricSeries) in metrics)
                {
                    targetMetrics[metricKey] = metricSeries;
                }
            }
        }

        Dictionary<string, SeriesSemanticsMetadata>? mergedMetadata = null;
        if (target.SeriesMetadata is not null)
        {
            mergedMetadata = new Dictionary<string, SeriesSemanticsMetadata>(target.SeriesMetadata, StringComparer.OrdinalIgnoreCase);
        }

        if (source.SeriesMetadata is not null)
        {
            mergedMetadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var (metric, metadata) in source.SeriesMetadata)
            {
                mergedMetadata[metric] = metadata;
            }
        }

        return new EdgeSeries
        {
            Id = target.Id,
            From = string.IsNullOrWhiteSpace(target.From) ? source.From : target.From,
            To = string.IsNullOrWhiteSpace(target.To) ? source.To : target.To,
            EdgeType = target.EdgeType ?? source.EdgeType,
            Field = target.Field ?? source.Field,
            Multiplier = target.Multiplier ?? source.Multiplier,
            Lag = target.Lag ?? source.Lag,
            Series = mergedSeries,
            SeriesMetadata = mergedMetadata,
            ByClass = mergedByClass
        };
    }

    private sealed record EdgeDefinitionInfo(string RawId, Edge Edge);

    private sealed class EdgeSeriesBuilder
    {
        private readonly EdgeDefinitionInfo definition;

        public EdgeSeriesBuilder(EdgeDefinitionInfo definition)
        {
            this.definition = definition;
        }

        public IDictionary<string, double?[]> Series { get; } = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SeriesSemanticsMetadata>? SeriesMetadata { get; private set; }
        public IDictionary<string, IDictionary<string, double?[]>>? ByClass { get; private set; }

        public void AddSeries(string classId, string metric, double?[] values, string origin)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.Equals(classId, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                Series[metric] = values;
                TryAddSeriesMetadata(metric, origin);
                return;
            }

            AddClassSeries(classId, metric, values, origin);
        }

        public void AddClassSeries(string classId, string metric, double?[] values, string origin)
        {
            if (ByClass is null)
            {
                ByClass = new Dictionary<string, IDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase);
            }

            if (!ByClass.TryGetValue(classId, out var metrics))
            {
                metrics = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
                ByClass[classId] = metrics;
            }

            metrics[metric] = values;
            TryAddSeriesMetadata(metric, origin);
        }

        private void TryAddSeriesMetadata(string metric, string origin)
        {
            var metadata = ResolveEdgeSeriesMetadata(metric, origin);
            if (metadata is null)
            {
                return;
            }

            SeriesMetadata ??= new Dictionary<string, SeriesSemanticsMetadata>(StringComparer.OrdinalIgnoreCase);
            SeriesMetadata[metric] = metadata;
        }

        public EdgeSeries Build()
        {
            return new EdgeSeries
            {
                Id = definition.RawId,
                From = ExtractNodeReference(definition.Edge.Source),
                To = ExtractNodeReference(definition.Edge.Target),
                EdgeType = NormalizeEdgeType(definition.Edge.EdgeType),
                Field = NormalizeField(definition.Edge.Field),
                Multiplier = NormalizeMultiplier(definition.Edge.Multiplier ?? definition.Edge.Weight),
                Lag = definition.Edge.Lag,
                Series = Series,
                SeriesMetadata = SeriesMetadata,
                ByClass = ByClass
            };
        }
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
        if (failuresSource == null)
        {
            return null;
        }
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

        return GetOptionalValue(data.Errors, index);
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

        var ratio = served.Value / arrivals.Value;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return null;
        }

        if (ratio < 0d)
        {
            return 0d;
        }

        if (ratio > 1d)
        {
            return 1d;
        }

        return ratio;
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
        var dispatchSchedule = ResolveDispatchSchedule(nodeId, context);

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = valuesSlice,
            [$"series:{nodeId}"] = valuesSlice
        };

        return new NodeSeries
        {
            Id = nodeId,
            Kind = kind,
            LogicalType = NormalizeKind(kind),
            Series = series,
            Telemetry = new NodeTelemetryInfo
            {
                Sources = Array.Empty<string>(),
                Warnings = ConvertNodeWarnings(nodeWarnings)
            },
            DispatchSchedule = dispatchSchedule
        };
    }

    private static DispatchScheduleDescriptor? ResolveDispatchSchedule(
        Node node,
        StateRunContext context,
        NodeDefinition? fallbackDefinition = null)
    {
        if (context.ModelNodes.TryGetValue(node.Id, out var definition))
        {
            var descriptor = ConvertDispatchSchedule(definition.DispatchSchedule);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        if (fallbackDefinition is not null)
        {
            var descriptor = ConvertDispatchSchedule(fallbackDefinition.DispatchSchedule);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        if (node.DispatchSchedule is not null)
        {
            var descriptor = ConvertDispatchSchedule(node.DispatchSchedule);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static DispatchScheduleDescriptor? ResolveDispatchSchedule(string nodeId, StateRunContext context)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        if (!context.ModelNodes.TryGetValue(nodeId, out var definition))
        {
            return null;
        }

        return ConvertDispatchSchedule(definition.DispatchSchedule);
    }

    private static DispatchScheduleDescriptor? ConvertDispatchSchedule(DispatchScheduleDefinition? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        var period = schedule.PeriodBins;
        if (period <= 0)
        {
            return null;
        }

        var kind = string.IsNullOrWhiteSpace(schedule.Kind) ? "time-based" : schedule.Kind.Trim();
        var normalizedPhase = DispatchScheduleProcessor.NormalizePhase(schedule.PhaseOffset ?? 0, period);
        var capacitySeries = string.IsNullOrWhiteSpace(schedule.CapacitySeries)
            ? null
            : schedule.CapacitySeries.Trim();

        return new DispatchScheduleDescriptor
        {
            Kind = kind,
            PeriodBins = period,
            PhaseOffset = normalizedPhase,
            CapacitySeries = capacitySeries
        };
    }

    private static NodeData CreateValuesOnlyNodeData(string nodeId, int bins, double[] values)
    {
        return new NodeData
        {
            NodeId = nodeId,
            Arrivals = CreateNaNSeries(bins),
            Served = CreateNaNSeries(bins),
            Errors = null,
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

    private static bool HasNodeWarning(
        IDictionary<string, List<ModeValidationWarning>> warnings,
        string nodeId,
        string code)
    {
        return warnings.TryGetValue(nodeId, out var list) &&
            list.Any(w => string.Equals(w.Code, code, StringComparison.OrdinalIgnoreCase));
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
        var parallelism = ResolveParallelism(semantics.Parallelism, data.Parallelism);
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
            Parallelism = parallelism,
            ExternalDemand = external,
            ProcessingTimeMsSum = processingTime,
            ServedCount = servedCount
        };

        double[]? ResolveParallelism(object? value, double[]? current)
        {
            if (current != null)
            {
                return current;
            }

            if (value is null)
            {
                return null;
            }

            if (value is string seriesId)
            {
                var scalar = ParseParallelismScalar(seriesId);
                return scalar.HasValue ? CreateConstantSeries(scalar.Value, bins) : Resolve(seriesId, null);
            }

            var literal = ParseParallelismScalar(value);
            return literal.HasValue ? CreateConstantSeries(literal.Value, bins) : null;
        }
    }

    private static IReadOnlyDictionary<string, NodeClassData>? ResolveClassData(
        Node node,
        NodeData data,
        SeriesIndex seriesIndex,
        string runDirectory,
        int bins,
        IReadOnlyDictionary<string, SeriesReference> seriesLookup,
        ILogger logger)
    {
        if (seriesIndex.Series is null || seriesIndex.Series.Length == 0)
        {
            return null;
        }

        var byClass = new Dictionary<string, NodeClassData>(StringComparer.OrdinalIgnoreCase);

        void Apply(string? semanticId, Func<NodeClassData, double[], NodeClassData> merge)
        {
            var componentId = ResolveComponentId(semanticId, seriesLookup);
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return;
            }

            var metricKey = NormalizeSeriesKey(semanticId ?? string.Empty);
            var atIndexMetric = metricKey.IndexOf('@');
            if (atIndexMetric >= 0)
            {
                metricKey = metricKey[..atIndexMetric];
            }

            foreach (var meta in seriesIndex.Series)
            {
                if (!string.Equals(meta.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var metaKey = meta.Id;
                var metaAt = metaKey.IndexOf('@');
                if (metaAt >= 0)
                {
                    metaKey = metaKey[..metaAt];
                }

                if (!string.Equals(metaKey, metricKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = Path.Combine(runDirectory, meta.Path.Replace('/', Path.DirectorySeparatorChar));
                double[] series;
                try
                {
                    series = CsvReader.ReadTimeSeries(path, bins);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to load class series {SeriesId} for node {NodeId}", meta.Id, node.Id);
                    continue;
                }

                var classId = string.IsNullOrWhiteSpace(meta.Class) ? "*" : meta.Class.Trim();
                if (string.Equals(classId, "*", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(classId, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip totals; they are already represented in NodeData.Arrivals/Served/Errors arrays.
                    continue;
                }
                var current = byClass.TryGetValue(classId, out var existing) ? existing : new NodeClassData();
                byClass[classId] = merge(current, series);
            }
        }

        Apply(node.Semantics.Arrivals, (current, series) => current with { Arrivals = series });
        Apply(node.Semantics.Served, (current, series) => current with { Served = series });
        Apply(node.Semantics.Errors, (current, series) => current with { Errors = series });
        Apply(node.Semantics.QueueDepth, (current, series) => current with { QueueDepth = series });
        Apply(node.Semantics.Capacity, (current, series) => current with { Capacity = series });
        Apply(node.Semantics.ProcessingTimeMsSum, (current, series) => current with { ProcessingTimeMsSum = series });
        Apply(node.Semantics.ServedCount, (current, series) => current with { ServedCount = series });

        if (byClass.Count > 0)
        {
            logger.LogDebug("Loaded byClass metrics for node {NodeId}: classes={ClassCount}", node.Id, byClass.Count);
        }

        return byClass.Count == 0 ? null : byClass;
    }

    private static string? ResolveComponentId(string? semanticId, IReadOnlyDictionary<string, SeriesReference> seriesLookup)
    {
        if (string.IsNullOrWhiteSpace(semanticId))
        {
            return null;
        }

        var normalized = NormalizeSeriesKey(semanticId);
        var key = normalized;
        var atIndex = key.IndexOf('@');
        if (atIndex >= 0)
        {
            key = key[..atIndex];
        }

        if (seriesLookup.TryGetValue(key, out var reference))
        {
            var component = ExtractComponentId(reference.Id);
            if (!string.IsNullOrWhiteSpace(component))
            {
                return component;
            }
        }

        var fallback = ExtractComponentId(normalized);
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static string NormalizeSeriesKey(string semanticId)
    {
        var trimmed = semanticId.Trim();
        if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["file:".Length..].TrimStart('/', '\\');
        }

        trimmed = Path.GetFileName(trimmed);

        if (trimmed.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return trimmed;
    }

    private static string? ExtractComponentId(string? seriesId)
    {
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            return null;
        }

        var parts = seriesId.Split('@');
        return parts.Length >= 2 ? parts[1] : null;
    }

    private static StateMetadata BuildMetadata(StateRunContext context, string? classCoverage = null)
    {
        var metadata = context.ManifestMetadata;
        var telemetryResolved = metadata.TelemetrySources.Count > 0 &&
            !context.InitialWarnings.Any(w => string.Equals(w.Code, "telemetry_sources_missing", StringComparison.OrdinalIgnoreCase));
        var mergedCoverage = MergeClassCoverage(classCoverage, context.Manifest.ClassCoverage);
        var edgeQuality = ResolveEdgeQuality(context, mergedCoverage);
        return new StateMetadata
        {
            RunId = context.Manifest.RunId,
            TemplateId = metadata.TemplateId,
            TemplateTitle = metadata.TemplateTitle,
            TemplateNarrative = metadata.TemplateNarrative,
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
            },
            ClassCoverage = mergedCoverage,
            EdgeQuality = edgeQuality
        };
    }

    private static string? MergeClassCoverage(string? computed, string? manifest)
    {
        var computedRank = CoverageRank(computed);
        var manifestRank = CoverageRank(manifest);

        if (computedRank < 0)
        {
            return manifest;
        }

        if (manifestRank < 0)
        {
            return computed;
        }

        if (computedRank == 0 && manifestRank > 0)
        {
            return manifest;
        }

        return computedRank <= manifestRank ? computed : manifest;
    }

    private static int CoverageRank(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "missing" => 0,
            "partial" => 1,
            "full" => 2,
            _ => -1
        };
    }

    private static string ResolveEdgeQuality(StateRunContext context, string? classCoverage)
    {
        if (context.SeriesIndex.Series is null || context.SeriesIndex.Series.Length == 0)
        {
            return "missing";
        }

        var edgeSeriesEntries = context.SeriesIndex.Series
            .Where(IsEdgeSeriesMetadata)
            .ToList();

        if (edgeSeriesEntries.Count == 0)
        {
            return "missing";
        }

        var classIds = context.SeriesIndex.Classes?
            .Select(entry => entry.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();

        if (classIds.Length == 0)
        {
            return "exact";
        }

        var edgeTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeClassCoverage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var hasFlowVolume = false;

        foreach (var series in edgeSeriesEntries)
        {
            if (!TryParseEdgeSeriesId(series.Id, out var edgeToken, out var metric))
            {
                continue;
            }

            if (!string.Equals(metric, "flowVolume", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasFlowVolume = true;
            edgeTokens.Add(edgeToken);

            if (string.IsNullOrWhiteSpace(series.Class) ||
                string.Equals(series.Class, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!edgeClassCoverage.TryGetValue(edgeToken, out var classes))
            {
                classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                edgeClassCoverage[edgeToken] = classes;
            }

            classes.Add(series.Class.Trim());
        }

        if (!hasFlowVolume)
        {
            return "missing";
        }

        if (edgeClassCoverage.Count == 0)
        {
            var coverage = classCoverage?.Trim().ToLowerInvariant();
            if (coverage == "missing")
            {
                return "approx";
            }

            if (coverage == "partial")
            {
                return "partialClass";
            }

            return "approx";
        }

        var expectedClasses = new HashSet<string>(classIds, StringComparer.OrdinalIgnoreCase);
        var expectedEdgeClasses = BuildEdgeClassExpectations(context);
        foreach (var edgeToken in edgeTokens)
        {
            var expected = expectedClasses;
            if (expectedEdgeClasses.TryGetValue(edgeToken, out var edgeExpected) && edgeExpected.Count > 0)
            {
                expected = edgeExpected;
            }

            if (!edgeClassCoverage.TryGetValue(edgeToken, out var classes) ||
                classes.Count == 0 ||
                !expected.IsSubsetOf(classes))
            {
                return "partialClass";
            }
        }

        return "exact";
    }

    private static IReadOnlyDictionary<string, HashSet<string>> BuildEdgeClassExpectations(StateRunContext context)
    {
        if (context.Topology.Nodes is null || context.Topology.Edges is null || context.ModelNodes.Count == 0)
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var arrivalLookup = context.Topology.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Semantics?.Arrivals))
            .ToDictionary(node => node.Semantics!.Arrivals.Trim(), node => node.Id, StringComparer.OrdinalIgnoreCase);

        var edgeTokensByPair = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in context.Topology.Edges)
        {
            var sourceId = ExtractNodeReference(edge.Source);
            var targetId = ExtractNodeReference(edge.Target);
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            var rawId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}"
                : edge.Id!;
            var edgeToken = NormalizeEdgeToken(rawId);
            if (string.IsNullOrWhiteSpace(edgeToken))
            {
                continue;
            }

            edgeTokensByPair[$"{sourceId}|{targetId}"] = edgeToken;
        }

        var expectations = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in context.ModelNodes.Values)
        {
            if (!string.Equals(node.Kind, "router", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (node.Router?.Routes is null)
            {
                continue;
            }

            foreach (var route in node.Router.Routes)
            {
                if (string.IsNullOrWhiteSpace(route.Target) || route.Classes is null || route.Classes.Length == 0)
                {
                    continue;
                }

                if (!arrivalLookup.TryGetValue(route.Target.Trim(), out var targetNodeId))
                {
                    continue;
                }

                var key = $"{node.Id}|{targetNodeId}";
                if (!edgeTokensByPair.TryGetValue(key, out var edgeToken))
                {
                    continue;
                }

                if (!expectations.TryGetValue(edgeToken, out var expectedClasses))
                {
                    expectedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    expectations[edgeToken] = expectedClasses;
                }

                foreach (var classId in route.Classes)
                {
                    if (string.IsNullOrWhiteSpace(classId))
                    {
                        continue;
                    }

                    expectedClasses.Add(classId.Trim());
                }
            }
        }

        return expectations;
    }

    private static IReadOnlyList<StateWarning> BuildWarnings(StateRunContext context, IReadOnlyList<ModeValidationWarning> additionalWarnings)
    {
        var warnings = new List<StateWarning>();

        if (context.Manifest.Warnings is { Length: > 0 })
        {
            foreach (var warning in context.Manifest.Warnings)
            {
                warnings.Add(BuildStateWarning(warning));
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
                    NodeId = warning.NodeId,
                    StartBin = warning.StartBin,
                    EndBin = warning.EndBin,
                    Signal = warning.Signal
                });
            }
        }

        return warnings.Count == 0 ? Array.Empty<StateWarning>() : warnings;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StateWarning>> BuildEdgeWarnings(StateRunContext context)
    {
        if (context.Manifest.Warnings is not { Length: > 0 })
        {
            return new Dictionary<string, IReadOnlyList<StateWarning>>(StringComparer.OrdinalIgnoreCase);
        }

        var warningsByEdge = new Dictionary<string, List<StateWarning>>(StringComparer.OrdinalIgnoreCase);
        foreach (var warning in context.Manifest.Warnings)
        {
            if (warning.EdgeIds is not { Length: > 0 })
            {
                continue;
            }

            var stateWarning = BuildStateWarning(warning);
            foreach (var edgeId in warning.EdgeIds)
            {
                if (string.IsNullOrWhiteSpace(edgeId))
                {
                    continue;
                }

                if (!warningsByEdge.TryGetValue(edgeId, out var entries))
                {
                    entries = new List<StateWarning>();
                    warningsByEdge[edgeId] = entries;
                }

                entries.Add(stateWarning);
            }
        }

        if (warningsByEdge.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<StateWarning>>(StringComparer.OrdinalIgnoreCase);
        }

        return warningsByEdge.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<StateWarning>)entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static StateWarning BuildStateWarning(RunWarning warning)
    {
        return new StateWarning
        {
            Code = warning.Code,
            Message = warning.Message,
            NodeId = warning.NodeId,
            Severity = string.IsNullOrWhiteSpace(warning.Severity) ? "warning" : warning.Severity
        };
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

    private static readonly Regex seriesFileRegex = new(@"(?<name>[^/\\]+?)(?:\.csv)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string DetermineLogicalType(
        Node node,
        string normalizedKind,
        StateRunContext context,
        out NodeDefinition? serviceWithBufferDefinition)
    {
        serviceWithBufferDefinition = TryResolveServiceWithBufferDefinition(node.Semantics?.QueueDepth, context.ModelNodes);
        if (serviceWithBufferDefinition is not null)
        {
            return "serviceWithBuffer";
        }

        return normalizedKind;
    }

    private static NodeDefinition? TryResolveServiceWithBufferDefinition(
        string? reference,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var candidateId = TryResolveSeriesNodeId(reference);
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return null;
        }

        return nodeDefinitions.TryGetValue(candidateId, out var definition) &&
            string.Equals(definition.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase)
            ? definition
            : null;
    }

    private static string? TryResolveSeriesNodeId(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var value = reference.Trim();
        if (value.StartsWith("series:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["series:".Length..];
        }
        else if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var match = seriesFileRegex.Match(value);
            if (match.Success)
            {
                value = match.Groups["name"].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var at = value.IndexOf('@');
        if (at > 0)
        {
            value = value[..at];
        }

        return value.Trim();
    }

    private static bool IsQueueLikeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        var normalized = kind.Trim().ToLowerInvariant();
        return normalized is "queue" or "dlq" or "servicewithbuffer";
    }

    private static QueueLatencyStatusDescriptor? DetermineQueueLatencyStatus(
        double? queue,
        double? served,
        DispatchScheduleDescriptor? schedule,
        int absoluteBin)
    {
        if (schedule is null || schedule.PeriodBins <= 0)
        {
            return null;
        }

        if (!queue.HasValue || queue.Value <= 0)
        {
            return null;
        }

        var servedValue = served ?? 0d;
        if (servedValue > 0)
        {
            return null;
        }

        if (DispatchScheduleProcessor.IsDispatchBin(absoluteBin, schedule.PeriodBins, schedule.PhaseOffset))
        {
            return null;
        }

        return pausedGateClosedStatus;
    }

    private static bool IsDlqKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return kind.Trim().Equals("dlq", StringComparison.OrdinalIgnoreCase);
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

    private static double?[]? ToNullableSeries(double[]? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new double?[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            result[i] = Normalize(source[i]);
        }

        return result;
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
        SeriesIndex SeriesIndex,
        string RunDirectory)
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
