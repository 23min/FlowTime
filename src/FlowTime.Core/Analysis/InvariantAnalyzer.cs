using System.Globalization;
using System.Linq;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Compiler;
using FlowTime.Core.Dispatching;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Analysis;

/// <summary>
/// Analyzes evaluated model series for conservation and sanity issues.
/// </summary>
public static class InvariantAnalyzer
{
    private const double defaultTolerance = 1e-6;

    public static InvariantAnalysisResult Analyze(
        ModelDefinition model,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        double tolerance = defaultTolerance,
        IReadOnlyList<RunArtifactWriter.EdgeSeriesInput>? edgeSeries = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(evaluatedSeries);

        var warnings = new List<InvariantWarning>();
        var cache = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var nodeDefinitions = model.Nodes?
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id!, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
        AppendPostEvalInjectionWarnings(nodeDefinitions, evaluatedSeries, warnings);
        if (model.Topology?.Nodes is null || model.Topology.Nodes.Count == 0)
        {
            return new InvariantAnalysisResult(warnings);
        }
        var queueSeeds = BuildQueueInitials(model.Topology.Nodes);
        var incomingEdges = new Dictionary<string, List<TopologyEdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
        var outgoingEdges = new Dictionary<string, List<TopologyEdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
        var topologyNodeLookup = model.Topology.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id!, n => n, StringComparer.OrdinalIgnoreCase);
        var constraintLookup = model.Topology.Constraints
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint.Id))
            .ToDictionary(constraint => constraint.Id, constraint => constraint, StringComparer.OrdinalIgnoreCase);

        if (model.Topology.Edges is { Count: > 0 })
        {
            foreach (var edge in model.Topology.Edges)
            {
                var sourceId = ExtractNodeId(edge.Source);
                var targetId = ExtractNodeId(edge.Target);

                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    if (!outgoingEdges.TryGetValue(sourceId, out var edges))
                    {
                        edges = new List<TopologyEdgeDefinition>();
                        outgoingEdges[sourceId] = edges;
                    }
                    edges.Add(edge);
                }

                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    if (!incomingEdges.TryGetValue(targetId, out var edges))
                    {
                        edges = new List<TopologyEdgeDefinition>();
                        incomingEdges[targetId] = edges;
                    }
                    edges.Add(edge);
                }
            }
        }

        if (model.Topology.Edges is { Count: > 0 })
        {
            foreach (var edge in model.Topology.Edges)
            {
                var lag = edge.Lag ?? 0;
                if (lag <= 0)
                {
                    continue;
                }

                var sourceId = ExtractNodeId(edge.Source);
                var edgeId = string.IsNullOrWhiteSpace(edge.Id)
                    ? $"{edge.Source}->{edge.Target}"
                    : edge.Id!;
                warnings.Add(new InvariantWarning(
                    string.IsNullOrWhiteSpace(sourceId) ? "engine" : sourceId,
                    "edge_behavior_violation_lag",
                    $"Edge '{edgeId}' applies a lag of {lag} bin(s). Model transit as an explicit node instead of edge behavior.",
                    Array.Empty<int>(),
                    null,
                    "warning",
                    new[] { edgeId }));
            }
        }

        var edgeFlowSeries = BuildEdgeFlowSeriesLookup(edgeSeries);
        var edgeClassSeries = BuildEdgeClassSeriesLookup(edgeSeries);
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>>? classContributions = null;
        if (edgeClassSeries.Count > 0 &&
            TryCreateTimeGrid(model.Grid, out var classGrid))
        {
            var classAssignments = ClassAssignmentMapBuilder.Build(model);
            if (classAssignments.Count > 0)
            {
                classContributions = ClassContributionBuilder.Build(model, classGrid, evaluatedSeries, classAssignments, out _);
            }
        }

        foreach (var topoNode in model.Topology.Nodes)
        {
            if (string.IsNullOrWhiteSpace(topoNode.Id) || topoNode.Semantics is null)
            {
                continue;
            }

            var semantics = topoNode.Semantics;
            var nodeId = topoNode.Id;
            var nodeKind = (topoNode.Kind ?? string.Empty).Trim().ToLowerInvariant();
            var isServiceWithBuffer = nodeKind == "servicewithbuffer";
            var isServiceKind = nodeKind == "service" || nodeKind == "router";
            var isQueueKind = nodeKind == "queue";
            var isQueueLikeKind = nodeKind is "queue" or "dlq";
            var isDlqKind = nodeKind == "dlq";
            var isDependencyKind = nodeKind == "dependency";
            var isTerminalQueue = isQueueKind &&
                                  incomingEdges.TryGetValue(nodeId, out var terminalInbound) &&
                                  terminalInbound.Count > 0 &&
                                  terminalInbound.All(IsTerminalEdge) &&
                                  (!outgoingEdges.TryGetValue(nodeId, out var queueOutbound) || queueOutbound.Count == 0);

            if (!TryGetSeries(semantics.Arrivals, out var arrivals))
            {
                arrivals = null;
            }
            if (!TryGetSeries(semantics.Served, out var served))
            {
                served = null;
            }
            if (!TryGetSeries(semantics.Errors, out var errors))
            {
                errors = null;
            }
            if (!TryGetSeries(semantics.Attempts, out var attempts))
            {
                attempts = null;
            }
            if (!TryGetSeries(semantics.Failures, out var failures))
            {
                failures = null;
            }
            if (!TryGetSeries(semantics.ExhaustedFailures, out var exhaustedFailures))
            {
                exhaustedFailures = null;
            }
            if (!TryGetSeries(semantics.QueueDepth, out var queueDepth))
            {
                queueDepth = null;
            }
            if (!TryGetSeries(semantics.RetryEcho, out var retryEcho))
            {
                retryEcho = null;
            }
            if (!TryGetSeries(semantics.Capacity, out var capacity))
            {
                capacity = null;
            }
            if (!TryGetSeries(semantics.RetryBudgetRemaining, out var retryBudgetRemaining))
            {
                retryBudgetRemaining = null;
            }

            if (topoNode.Constraints is { Count: > 0 } && constraintLookup.Count > 0)
            {
                foreach (var constraintId in topoNode.Constraints)
                {
                    if (string.IsNullOrWhiteSpace(constraintId))
                    {
                        continue;
                    }

                    if (!constraintLookup.TryGetValue(constraintId, out var constraint))
                    {
                        continue;
                    }

                    if (!TryGetSeries(constraint.Semantics.Arrivals, out _))
                    {
                        warnings.Add(new InvariantWarning(
                            nodeId,
                            "constraint_missing_arrivals",
                            $"Constraint '{constraintId}' arrivals series was not available.",
                            Array.Empty<int>(),
                            null));
                    }

                    if (!TryGetSeries(constraint.Semantics.Served, out _))
                    {
                        warnings.Add(new InvariantWarning(
                            nodeId,
                            "constraint_missing_served",
                            $"Constraint '{constraintId}' served series was not available.",
                            Array.Empty<int>(),
                            null));
                    }
                }
            }

            var effectiveCapacity = capacity;
            if (capacity is not null)
            {
                ResolveParallelism(SemanticReferenceResolver.ParseParallelismReference(semantics.Parallelism), out var parallelismSeries, out var parallelismScalar);
                effectiveCapacity = ApplyParallelism(capacity, parallelismSeries, parallelismScalar);
            }

            // Non-negative checks
            CheckNonNegative(nodeId, "arrivals_negative", "Arrivals produced negative values", arrivals);
            CheckNonNegative(nodeId, "served_negative", "Served produced negative values", served);
            CheckNonNegative(nodeId, "errors_negative", "Errors produced negative values", errors);
            CheckNonNegative(nodeId, "attempts_negative", "Attempts produced negative values", attempts);
            CheckNonNegative(nodeId, "failures_negative", "Failures produced negative values", failures);
            CheckNonNegative(nodeId, "exhausted_failures_negative", "Exhausted failures produced negative values", exhaustedFailures);
            CheckNonNegative(nodeId, "queue_negative", "Queue depth produced negative values", queueDepth);
            CheckNonNegative(nodeId, "retry_echo_negative", "Retry echo produced negative values", retryEcho);
            CheckNonNegative(nodeId, "retry_budget_negative", "Retry budget remaining produced negative values", retryBudgetRemaining);

            // Served <= arrivals
            if (arrivals != null && served != null && !isServiceWithBuffer && !isDependencyKind)
            {
                CheckDiff(nodeId, "served_exceeds_arrivals",
                    "Served volume exceeded arrivals",
                    served, arrivals, greaterTolerance: true);
            }

            // Served <= effective capacity
            if (served != null && effectiveCapacity != null && (isServiceKind || isServiceWithBuffer))
            {
                CheckDiff(nodeId, "served_exceeds_capacity",
                    "Served volume exceeded effective capacity",
                    served, effectiveCapacity, greaterTolerance: true);
            }

            // Errors <= arrivals
            if (arrivals != null && errors != null)
            {
                CheckDiff(nodeId, "errors_exceed_arrivals",
                    "Errors exceeded arrivals",
                    errors, arrivals, greaterTolerance: true);
            }

            // Attempts >= arrivals
            if (arrivals != null && attempts != null)
            {
                CheckDiff(nodeId, "attempts_below_arrivals",
                    "Attempts were lower than arrivals",
                    attempts, arrivals, greaterTolerance: false);
            }

            // Failures <= attempts
            if (attempts != null && failures != null)
            {
                CheckDiff(nodeId, "failures_exceed_attempts",
                    "Failed retries exceeded total attempts",
                    failures, attempts, greaterTolerance: true);
            }

            if (exhaustedFailures != null && failures != null)
            {
                CheckDiff(nodeId, "exhausted_exceeds_failures",
                    "Exhausted retries exceeded total failures",
                    exhaustedFailures, failures, greaterTolerance: true);
            }
            else if (exhaustedFailures != null && errors != null)
            {
                CheckDiff(nodeId, "exhausted_exceeds_errors",
                    "Exhausted retries exceeded total errors",
                    exhaustedFailures, errors, greaterTolerance: true);
            }

            // Resolve dispatch schedule early — needed for queue validation (BUG-3 fix)
            var dispatchSchedule = ResolveDispatchSchedule(nodeId, semantics);

            // Queue depth validation
            if (queueDepth != null && arrivals != null && served != null)
            {
                var seed = (semantics.QueueDepth != null && queueSeeds.TryGetValue(semantics.QueueDepth, out var val))
                    ? val
                    : 0d;
                ValidateQueue(nodeId, queueDepth, arrivals, served, errors, seed, dispatchSchedule);
            }

            if (edgeFlowSeries.Count > 0)
            {
                if (served != null && outgoingEdges.TryGetValue(nodeId, out var outgoing))
                {
                    if (TrySumEdgeFlows(outgoing, served.Length, applyLag: false, out var outgoingSum))
                    {
                        CheckEdgeConservation(
                            nodeId,
                            "edge_flow_mismatch_outgoing",
                            "Served does not match sum of outgoing edge flows.",
                            served,
                            outgoingSum,
                            outgoing);
                    }
                }

                if (arrivals != null && incomingEdges.TryGetValue(nodeId, out var incoming))
                {
                    if (TrySumEdgeFlows(incoming, arrivals.Length, applyLag: true, out var incomingSum))
                    {
                        CheckEdgeConservation(
                            nodeId,
                            "edge_flow_mismatch_incoming",
                            "Arrivals do not match sum of incoming edge flows.",
                            arrivals,
                            incomingSum,
                            incoming);
                    }
                }
            }

            if (classContributions != null)
            {
                if (!string.IsNullOrWhiteSpace(semantics.Served) &&
                    classContributions.TryGetValue(new NodeId(semantics.Served), out var servedByClass) &&
                    outgoingEdges.TryGetValue(nodeId, out var outgoing))
                {
                    CheckEdgeClassFlows(
                        nodeId,
                        "Per-class served does not match outgoing edge flows.",
                        servedByClass,
                        outgoing,
                        applyLag: false);
                }

                if (!string.IsNullOrWhiteSpace(semantics.Arrivals) &&
                    classContributions.TryGetValue(new NodeId(semantics.Arrivals), out var arrivalsByClass) &&
                    incomingEdges.TryGetValue(nodeId, out var incoming))
                {
                    CheckEdgeClassFlows(
                        nodeId,
                        "Per-class arrivals do not match incoming edge flows.",
                        arrivalsByClass,
                        incoming,
                        applyLag: true);
                }
            }

            // Soft info: missing prerequisite metrics
            var expectsCapacity = isServiceKind || !string.IsNullOrWhiteSpace(semantics.Capacity);
            var expectsServed = isServiceKind || !string.IsNullOrWhiteSpace(semantics.Served);
            var expectsQueue = isQueueLikeKind || !string.IsNullOrWhiteSpace(semantics.QueueDepth);
            var hasMaxAttempts = semantics.MaxAttempts.HasValue;

            if (expectsCapacity && capacity == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_capacity_series",
                    "Capacity series was not available; utilization cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (hasMaxAttempts && exhaustedFailures == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_exhausted_failures_series",
                    "maxAttempts was configured but exhaustedFailures series was not produced.",
                    Array.Empty<int>(),
                    null,
                    "warning"));
            }

            if (hasMaxAttempts && retryBudgetRemaining == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_retry_budget_series",
                    "maxAttempts was configured but retryBudgetRemaining series was not produced.",
                    Array.Empty<int>(),
                    null,
                    "warning"));
            }
            else if (effectiveCapacity != null && effectiveCapacity.All(v => Math.Abs(v) <= tolerance))
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "capacity_all_zero",
                    "Effective capacity series is zero for all bins; utilization will be unavailable.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (isDependencyKind && arrivals == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_dependency_arrivals",
                    "Dependency arrivals series was not available; dependency load cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (isDependencyKind && served == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_dependency_served",
                    "Dependency served series was not available; dependency utilization cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }
            else if (expectsServed && served == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_served_series",
                    "Served/output series was not available; utilization cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (isDependencyKind)
            {
                var hasIncomingEffort = incomingEdges.TryGetValue(nodeId, out var dependencyIncoming) &&
                    dependencyIncoming.Any(edge => IsEffortEdge(edge));
                if (!hasIncomingEffort)
                {
                    warnings.Add(new InvariantWarning(
                        nodeId,
                        "dependency_missing_effort_edges",
                        "Dependency has no incoming effort edges; attempt pressure cannot be represented.",
                        Array.Empty<int>(),
                        null,
                        "info"));
                }

                if (!string.IsNullOrWhiteSpace(semantics.Errors) && hasIncomingEffort)
                {
                    var hasAttempts = dependencyIncoming!.Any(edge =>
                    {
                        var sourceId = ExtractNodeId(edge.Source);
                        if (string.IsNullOrWhiteSpace(sourceId))
                        {
                            return false;
                        }

                        if (!topologyNodeLookup.TryGetValue(sourceId, out var sourceNode) || sourceNode.Semantics is null)
                        {
                            return false;
                        }

                        return !string.IsNullOrWhiteSpace(sourceNode.Semantics.Attempts);
                    });

                    if (!hasAttempts)
                    {
                        warnings.Add(new InvariantWarning(
                            nodeId,
                            "dependency_retry_pressure_missing",
                            "Dependency errors are defined but upstream attempt series are missing; retry pressure will be invisible.",
                            Array.Empty<int>(),
                            null,
                            "info"));
                    }
                }
            }

            if (expectsQueue && queueDepth == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_queue_depth_series",
                    "Queue depth series was not available; queue overlays may be incomplete.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (dispatchSchedule is not null)
            {
                ValidateDispatchSchedule(nodeId, dispatchSchedule, arrivals, served);
            }

            // Derived-completeness infos
            var processingSeries = TryGetSeries(semantics.ProcessingTimeMsSum, out var proc) ? proc : null;
            var servedCountSeries = TryGetSeries(semantics.ServedCount, out var sc) ? sc : null;

            var expectsProcessing = isServiceKind || !string.IsNullOrWhiteSpace(semantics.ProcessingTimeMsSum);
            var expectsServedCount = isServiceKind || !string.IsNullOrWhiteSpace(semantics.ServedCount);
            if (expectsProcessing && processingSeries == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_processing_time_series",
                    "Processing time series was not available; service time cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (expectsServedCount && servedCountSeries == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_served_count_series",
                    "Served count series was not available; service time cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (!isDlqKind && !isTerminalQueue && !isServiceWithBuffer && queueDepth != null && served != null)
            {
                var badLatencyBins = new List<int>();
                var latencyBins = queueDepth.Length;
                if (arrivals != null)
                {
                    latencyBins = Math.Min(latencyBins, arrivals.Length);
                }
                for (var i = 0; i < latencyBins; i++)
                {
                    var q = queueDepth[i];
                    var s = served[i];
                    if (!double.IsFinite(q) || !double.IsFinite(s))
                    {
                        continue;
                    }

                    if (s <= tolerance && Math.Abs(q) > tolerance)
                    {
                        badLatencyBins.Add(i);
                    }
                }

                if (badLatencyBins.Count > 0)
                {
                    var warningCode = dispatchSchedule is not null
                        ? "queue_latency_gate_closed"
                        : "queue_latency_unreported";
                    var warningMessage = dispatchSchedule is not null
                        ? "Dispatch gate was closed while backlog was present; latency will display as Paused (gate closed)."
                        : "Queue latency could not be computed where served was zero and queue depth was positive.";

                    warnings.Add(new InvariantWarning(
                        nodeId,
                        warningCode,
                        warningMessage,
                        badLatencyBins.ToArray(),
                        null,
                        "info"));
                }
            }
            if (isDlqKind)
            {
                ValidateDlqConnectivity(nodeId);
            }
        }

        AppendRouterDiagnostics(model, evaluatedSeries, warnings);
        AppendServiceWithBufferClassCoverageWarnings(nodeDefinitions, model, evaluatedSeries, warnings);
        AppendTopologyClassCoverageWarnings(model, evaluatedSeries, warnings);

        return warnings.Count == 0
            ? new InvariantAnalysisResult(Array.Empty<InvariantWarning>())
            : new InvariantAnalysisResult(warnings);

        Dictionary<string, double[]> BuildEdgeFlowSeriesLookup(IReadOnlyList<RunArtifactWriter.EdgeSeriesInput>? inputs)
        {
            var lookup = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            if (inputs is null)
            {
                return lookup;
            }

            foreach (var series in inputs)
            {
                if (!string.Equals(series.Metric, "flowVolume", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(series.ClassId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(series.EdgeId) || series.Values is null)
                {
                    continue;
                }

                lookup[series.EdgeId.Trim()] = series.Values;
            }

            return lookup;
        }

        Dictionary<string, Dictionary<string, double[]>> BuildEdgeClassSeriesLookup(
            IReadOnlyList<RunArtifactWriter.EdgeSeriesInput>? inputs)
        {
            var lookup = new Dictionary<string, Dictionary<string, double[]>>(StringComparer.OrdinalIgnoreCase);
            if (inputs is null)
            {
                return lookup;
            }

            foreach (var series in inputs)
            {
                if (!string.Equals(series.Metric, "flowVolume", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(series.ClassId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(series.EdgeId) || series.Values is null)
                {
                    continue;
                }

                if (!lookup.TryGetValue(series.EdgeId.Trim(), out var perClass))
                {
                    perClass = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    lookup[series.EdgeId.Trim()] = perClass;
                }

                perClass[series.ClassId.Trim()] = series.Values;
            }

            return lookup;
        }

        bool TryGetSeries(string? id, out double[]? series)
        {
            series = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (cache.TryGetValue(id, out var cached))
            {
                series = cached;
                return true;
            }

            var nodeId = new NodeId(id.Trim());
            if (!evaluatedSeries.TryGetValue(nodeId, out var values))
            {
                return false;
            }

            cache[id] = values;
            series = values;
            return true;
        }

        void ResolveParallelism(CompiledParallelismReference? parallelism, out double[]? series, out double? scalar)
        {
            series = null;
            scalar = null;

            if (parallelism is null)
            {
                return;
            }

            if (parallelism.Constant.HasValue)
            {
                scalar = parallelism.Constant.Value;
                return;
            }

            if (parallelism.Series is not null && TryGetSeries(parallelism.Series.LookupKey, out var resolved))
            {
                series = resolved;
            }
        }

        double[] ApplyParallelism(double[] baseCapacity, double[]? parallelismSeries, double? parallelismScalar)
        {
            if (parallelismSeries is { Length: > 0 })
            {
                if (parallelismSeries.Length != baseCapacity.Length)
                {
                    return baseCapacity;
                }

                var scaled = new double[baseCapacity.Length];
                for (var i = 0; i < baseCapacity.Length; i++)
                {
                    var factor = parallelismSeries[i];
                    scaled[i] = double.IsFinite(factor) ? baseCapacity[i] * factor : baseCapacity[i];
                }

                return scaled;
            }

            if (parallelismScalar.HasValue && double.IsFinite(parallelismScalar.Value))
            {
                var scaled = new double[baseCapacity.Length];
                for (var i = 0; i < baseCapacity.Length; i++)
                {
                    scaled[i] = baseCapacity[i] * parallelismScalar.Value;
                }

                return scaled;
            }

            return baseCapacity;
        }

        void CheckNonNegative(string nodeId, string code, string message, double[]? series)
        {
            if (series == null)
            {
                return;
            }

            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            for (var i = 0; i < series.Length; i++)
            {
                if (series[i] < -tolerance)
                {
                    if (binCount < bins.Length)
                    {
                        bins[binCount++] = i;
                    }
                }
            }

            if (binCount > 0)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    code,
                    message,
                    bins[..binCount].ToArray(),
                    series[bins[0]]));
            }
        }

        void CheckDiff(string nodeId, string code, string message, double[] lhs, double[] rhs, bool greaterTolerance)
        {
            if (lhs.Length != rhs.Length)
            {
                return;
            }

            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            double worstRatio = 0;
            for (var i = 0; i < lhs.Length; i++)
            {
                var diff = lhs[i] - rhs[i];
                var violates = greaterTolerance ? diff > tolerance : diff < -tolerance;
                if (violates)
                {
                    if (binCount < bins.Length)
                    {
                        bins[binCount++] = i;
                    }

                    var baseValue = Math.Abs(rhs[i]) <= tolerance ? tolerance : Math.Abs(rhs[i]);
                    var ratio = Math.Abs(lhs[i] / baseValue);
                    if (ratio > worstRatio)
                    {
                        worstRatio = ratio;
                    }
                }
            }

            if (binCount > 0)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    code,
                    message,
                    bins[..binCount].ToArray(),
                    double.IsFinite(worstRatio) ? worstRatio : null));
            }
        }

        void ValidateQueue(string nodeId, double[] queueDepth, double[] inflow, double[] outflow, double[]? loss, double seed, DispatchScheduleDefinition? dispatchSchedule)
        {
            if (inflow.Length != outflow.Length || queueDepth.Length != inflow.Length)
            {
                return;
            }

            // Apply dispatch schedule to outflow copy (BUG-3 fix).
            // ServiceWithBufferNode zeros outflow on non-dispatch bins; the invariant
            // check must replicate this to avoid false positive queue_depth_mismatch.
            var effectiveOutflow = outflow;
            if (dispatchSchedule is not null && dispatchSchedule.PeriodBins > 0)
            {
                effectiveOutflow = (double[])outflow.Clone();
                DispatchScheduleProcessor.ApplySchedule(
                    dispatchSchedule.PeriodBins,
                    dispatchSchedule.PhaseOffset ?? 0,
                    effectiveOutflow,
                    capacityOverride: null);
            }

            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            double expected = seed;
            for (var i = 0; i < queueDepth.Length; i++)
            {
                expected = Math.Max(0, expected + inflow[i] - effectiveOutflow[i] - (loss?[i] ?? 0));
                var diff = Math.Abs(queueDepth[i] - expected);
                if (diff > tolerance)
                {
                    if (binCount < bins.Length)
                    {
                        bins[binCount++] = i;
                    }
                }
            }

            if (binCount > 0)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "queue_depth_mismatch",
                    "Queue depth series does not match derived inflow/outflow accumulation",
                    bins[..binCount].ToArray(),
                    null));
            }
        }

        bool TrySumEdgeFlows(IReadOnlyList<TopologyEdgeDefinition> edges, int expectedLength, bool applyLag, out double[] sum)
        {
            sum = Array.Empty<double>();
            if (edges.Count == 0)
            {
                return false;
            }

            var relevantEdges = edges.Where(IsFlowEdge).ToList();
            if (relevantEdges.Count == 0)
            {
                return false;
            }

            var total = new double[expectedLength];
            foreach (var edge in relevantEdges)
            {
                var edgeId = ResolveEdgeSeriesId(edge);
                if (string.IsNullOrWhiteSpace(edgeId) ||
                    !edgeFlowSeries.TryGetValue(edgeId, out var series) ||
                    series.Length != expectedLength)
                {
                    return false;
                }

                var lag = applyLag && edge.Lag is > 0 ? edge.Lag.Value : 0;
                for (var i = 0; i < expectedLength; i++)
                {
                    var index = i - lag;
                    if (index < 0 || index >= series.Length)
                    {
                        continue;
                    }

                    total[i] += series[index];
                }
            }

            sum = total;
            return true;
        }

        bool TrySumEdgeClassFlows(
            IReadOnlyList<TopologyEdgeDefinition> edges,
            IEnumerable<string> classIds,
            int expectedLength,
            bool applyLag,
            out Dictionary<string, double[]> totals,
            out HashSet<string> coveredClasses)
        {
            totals = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            coveredClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var classId in classIds)
            {
                if (string.IsNullOrWhiteSpace(classId))
                {
                    continue;
                }

                totals[classId] = new double[expectedLength];
            }

            if (totals.Count == 0)
            {
                return false;
            }

            var hasEdgeClassData = false;
            foreach (var edge in edges.Where(IsFlowEdge))
            {
                var edgeId = ResolveEdgeSeriesId(edge);
                if (string.IsNullOrWhiteSpace(edgeId) ||
                    !edgeClassSeries.TryGetValue(edgeId, out var perClass))
                {
                    continue;
                }

                hasEdgeClassData = true;
                foreach (var (classId, series) in perClass)
                {
                    if (!totals.TryGetValue(classId, out var total) || series.Length != expectedLength)
                    {
                        continue;
                    }

                    coveredClasses.Add(classId);
                    var lag = applyLag && edge.Lag is > 0 ? edge.Lag.Value : 0;
                    for (var i = 0; i < expectedLength; i++)
                    {
                        var index = i - lag;
                        if (index < 0 || index >= series.Length)
                        {
                            continue;
                        }

                        total[i] += series[index];
                    }
                }
            }

            return hasEdgeClassData;
        }

        void CheckEdgeClassFlows(
            string nodeId,
            string message,
            IReadOnlyDictionary<string, double[]> nodeByClass,
            IReadOnlyList<TopologyEdgeDefinition> edges,
            bool applyLag)
        {
            if (nodeByClass.Count == 0)
            {
                return;
            }

            var length = GetSeriesLength(nodeByClass);
            if (length == 0)
            {
                return;
            }

            if (TrySumEdgeClassFlows(edges, nodeByClass.Keys, length, applyLag, out var edgeByClass, out var covered))
            {
                CheckEdgeClassConservation(
                    nodeId,
                    "edge_class_mismatch",
                    message,
                    nodeByClass,
                    edgeByClass,
                    covered,
                    edges);
            }
        }

        void CheckEdgeConservation(
            string nodeId,
            string code,
            string message,
            double[] nodeSeries,
            double[] edgeSeriesSum,
            IReadOnlyList<TopologyEdgeDefinition> edges)
        {
            if (nodeSeries.Length != edgeSeriesSum.Length)
            {
                return;
            }

            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            var worstDiff = 0d;
            for (var i = 0; i < nodeSeries.Length; i++)
            {
                var diff = Math.Abs(nodeSeries[i] - edgeSeriesSum[i]);
                if (diff > tolerance)
                {
                    if (binCount < bins.Length)
                    {
                        bins[binCount++] = i;
                    }

                    if (diff > worstDiff)
                    {
                        worstDiff = diff;
                    }
                }
            }

            if (binCount > 0)
            {
                var edgeIds = ResolveEdgeIds(edges);
                warnings.Add(new InvariantWarning(
                    nodeId,
                    code,
                    BuildEdgeWarningMessage(message, edges),
                    bins[..binCount].ToArray(),
                    double.IsFinite(worstDiff) ? worstDiff : null,
                    EdgeIds: edgeIds.Count > 0 ? edgeIds : null));
            }
        }

        void CheckEdgeClassConservation(
            string nodeId,
            string code,
            string message,
            IReadOnlyDictionary<string, double[]> nodeByClass,
            IReadOnlyDictionary<string, double[]> edgeByClass,
            IReadOnlySet<string> coveredClasses,
            IReadOnlyList<TopologyEdgeDefinition> edges)
        {
            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            var worstDiff = 0d;
            var mismatchedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missingClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (classId, nodeSeries) in nodeByClass)
            {
                if (!edgeByClass.TryGetValue(classId, out var edgeSeries))
                {
                    missingClasses.Add(classId);
                    continue;
                }

                if (nodeSeries.Length != edgeSeries.Length)
                {
                    mismatchedClasses.Add(classId);
                    continue;
                }

                for (var i = 0; i < nodeSeries.Length; i++)
                {
                    var diff = Math.Abs(nodeSeries[i] - edgeSeries[i]);
                    if (diff > tolerance)
                    {
                        mismatchedClasses.Add(classId);
                        if (binCount < bins.Length)
                        {
                            bins[binCount++] = i;
                        }

                        if (diff > worstDiff)
                        {
                            worstDiff = diff;
                        }
                    }
                }
            }

            if (mismatchedClasses.Count == 0 && missingClasses.Count == 0)
            {
                return;
            }

            var notCoveredClasses = nodeByClass.Keys
                .Where(classId => !coveredClasses.Contains(classId))
                .ToArray();
            foreach (var classId in notCoveredClasses)
            {
                missingClasses.Add(classId);
            }

            var warningCode = missingClasses.Count > 0 ? "edge_class_partial_coverage" : code;
            var mismatchedList = string.Join(", ", mismatchedClasses.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            var missingList = string.Join(", ", missingClasses.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            var detail = message;
            if (!string.IsNullOrWhiteSpace(mismatchedList))
            {
                detail = $"{detail} Classes: {mismatchedList}.";
            }
            else if (!string.IsNullOrWhiteSpace(missingList))
            {
                detail = $"{detail} Missing classes: {missingList}.";
            }
            var edgeIds = ResolveEdgeIds(edges);
            warnings.Add(new InvariantWarning(
                nodeId,
                warningCode,
                BuildEdgeWarningMessage(detail, edges),
                binCount > 0 ? bins[..binCount].ToArray() : Array.Empty<int>(),
                double.IsFinite(worstDiff) ? worstDiff : null,
                "warning",
                edgeIds.Count > 0 ? edgeIds : null));
        }

        void ValidateDlqConnectivity(string nodeId)
        {
            if (incomingEdges.TryGetValue(nodeId, out var inbound) && inbound.Count > 0)
            {
                var invalid = inbound
                    .Where(edge => !IsTerminalEdge(edge))
                    .Select(GetEdgeLabel)
                    .ToArray();
                if (invalid.Length > 0)
                {
                    warnings.Add(new InvariantWarning(
                        nodeId,
                        "dlq_non_terminal_inbound",
                        $"DLQ nodes must only receive terminal edges ({string.Join(", ", invalid)}).",
                        Array.Empty<int>(),
                        null,
                        "warning"));
                }
            }

            if (outgoingEdges.TryGetValue(nodeId, out var outbound) && outbound.Count > 0)
            {
                var invalid = outbound
                    .Where(edge => !IsTerminalEdge(edge))
                    .Select(GetEdgeLabel)
                    .ToArray();
                if (invalid.Length > 0)
                {
                    warnings.Add(new InvariantWarning(
                        nodeId,
                        "dlq_non_terminal_outbound",
                        $"DLQ nodes must emit terminal edges ({string.Join(", ", invalid)}).",
                        Array.Empty<int>(),
                        null,
                        "warning"));
                }
            }
        }

        DispatchScheduleDefinition? ResolveDispatchSchedule(string topologyNodeId, TopologyNodeSemanticsDefinition semantics)
        {
            if (nodeDefinitions.TryGetValue(topologyNodeId, out var nodeDef) &&
                nodeDef.DispatchSchedule is not null)
            {
                return nodeDef.DispatchSchedule;
            }

            var queueDepthRef = ExtractNodeId(semantics.QueueDepth ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(queueDepthRef) &&
                nodeDefinitions.TryGetValue(queueDepthRef, out var depthDef) &&
                depthDef.DispatchSchedule is not null)
            {
                return depthDef.DispatchSchedule;
            }

            return null;
        }

        void ValidateDispatchSchedule(
            string topologyNodeId,
            DispatchScheduleDefinition schedule,
            double[]? arrivals,
            double[]? served)
        {
            if (!string.IsNullOrWhiteSpace(schedule.CapacitySeries) &&
                !TryGetSeries(schedule.CapacitySeries, out _))
            {
                warnings.Add(new InvariantWarning(
                    topologyNodeId,
                    "dispatch_capacity_missing",
                    $"Dispatch schedule references capacity series '{schedule.CapacitySeries}' but it was not produced.",
                    Array.Empty<int>(),
                    null,
                    "warning"));
            }

            if (served is null)
            {
                warnings.Add(new InvariantWarning(
                    topologyNodeId,
                    "dispatch_missing_served_series",
                    "Dispatch schedule configured but served/output series was not available.",
                    Array.Empty<int>(),
                    null,
                    "info"));
                return;
            }

            if (arrivals is null)
            {
                return;
            }

            var arrivalTotal = SumFinite(arrivals);
            if (arrivalTotal <= tolerance)
            {
                return;
            }

            var servedTotal = SumFinite(served);
            if (servedTotal <= tolerance)
            {
                warnings.Add(new InvariantWarning(
                    topologyNodeId,
                    "dispatch_never_releases",
                    "Dispatch schedule never released inventory across the simulated window; check period and phase alignment.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }
        }

        static bool IsTerminalEdge(TopologyEdgeDefinition edge) =>
            !string.IsNullOrWhiteSpace(edge.Type) &&
            edge.Type.Trim().Equals("terminal", StringComparison.OrdinalIgnoreCase);

        static bool IsEffortEdge(TopologyEdgeDefinition edge)
        {
            if (string.IsNullOrWhiteSpace(edge.Type))
            {
                return false;
            }

            var normalized = edge.Type.Trim();
            return normalized.Equals("effort", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("dependency", StringComparison.OrdinalIgnoreCase);
        }

        static string GetEdgeLabel(TopologyEdgeDefinition edge)
        {
            if (!string.IsNullOrWhiteSpace(edge.Id))
            {
                return edge.Id!;
            }

            var source = string.IsNullOrWhiteSpace(edge.Source) ? "?" : edge.Source;
            var target = string.IsNullOrWhiteSpace(edge.Target) ? "?" : edge.Target;
            return $"{source}->{target}";
        }

        static IReadOnlyList<string> ResolveEdgeIds(IReadOnlyList<TopologyEdgeDefinition> edges)
        {
            if (edges.Count == 0)
            {
                return Array.Empty<string>();
            }

            return edges
                .Select(GetEdgeLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static string BuildEdgeWarningMessage(string message, IReadOnlyList<TopologyEdgeDefinition> edges)
        {
            if (edges.Count == 0)
            {
                return message;
            }

            var distinct = edges
                .Select(GetEdgeLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (distinct.Length == 0)
            {
                return message;
            }

            var suffix = FormatEdgeList(distinct, 3);
            var trimmed = message.TrimEnd();
            if (trimmed.EndsWith(".", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^1];
            }

            return $"{trimmed}. Edges: {suffix}.";
        }

        static string FormatEdgeList(IReadOnlyList<string> labels, int max)
        {
            if (labels.Count <= max)
            {
                return string.Join(", ", labels);
            }

            var prefix = string.Join(", ", labels.Take(max));
            return $"{prefix} (+{labels.Count - max} more)";
        }

        static string ExtractNodeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var separatorIndex = trimmed.IndexOf(':');
            return separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
        }

        static string ExtractPort(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var separatorIndex = trimmed.IndexOf(':');
            return separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : string.Empty;
        }

        static bool IsFlowEdge(TopologyEdgeDefinition edge)
        {
            if (!string.IsNullOrWhiteSpace(edge.Measure) &&
                !edge.Measure.Equals("served", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var port = ExtractPort(edge.Source);
            if (!string.IsNullOrWhiteSpace(port) &&
                !port.Equals("out", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        static string ResolveEdgeSeriesId(TopologyEdgeDefinition edge)
        {
            if (!string.IsNullOrWhiteSpace(edge.Id))
            {
                return edge.Id!;
            }

            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
            {
                return string.Empty;
            }

            return $"{edge.Source}->{edge.Target}";
        }

        static int GetSeriesLength(IReadOnlyDictionary<string, double[]> seriesByClass)
        {
            foreach (var series in seriesByClass.Values)
            {
                return series.Length;
            }

            return 0;
        }

        static double SumFinite(double[] series)
        {
            double total = 0d;
            foreach (var value in series)
            {
                if (!double.IsFinite(value))
                {
                    continue;
                }

                total += Math.Max(0d, value);
            }

            return total;
        }
    }

    private static void AppendRouterDiagnostics(
        ModelDefinition model,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        List<InvariantWarning> warnings)
    {
        if (model.Nodes == null || model.Nodes.Count == 0)
        {
            return;
        }

        if (!model.Nodes.Any(n => string.Equals(n.Kind, "router", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!TryCreateTimeGrid(model.Grid, out var grid))
        {
            return;
        }

        var classAssignments = ClassAssignmentMapBuilder.Build(model);
        if (classAssignments.Count == 0)
        {
            return;
        }

        try
        {
            _ = ClassContributionBuilder.Build(model, grid, evaluatedSeries, classAssignments, out var routerDiagnostics);
            foreach (var diagnostic in routerDiagnostics)
            {
                warnings.Add(new InvariantWarning(
                    diagnostic.RouterId,
                    diagnostic.Code,
                    diagnostic.Message,
                    Array.Empty<int>(),
                    null,
                    "warning"));
            }
        }
        catch (Exception ex)
        {
            warnings.Add(new InvariantWarning(
                "router_diagnostics",
                "router_diagnostics_failed",
                $"Router diagnostics failed: {ex.Message}",
                Array.Empty<int>(),
                null,
                "warning"));
        }
    }

    private static void AppendServiceWithBufferClassCoverageWarnings(
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        ModelDefinition model,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        List<InvariantWarning> warnings)
    {
        if (nodeDefinitions.Count == 0 ||
            !nodeDefinitions.Values.Any(n => string.Equals(n.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var classAssignments = ClassAssignmentMapBuilder.Build(model);
        if (classAssignments.Count == 0)
        {
            return;
        }

        if (!TryCreateTimeGrid(model.Grid, out var grid))
        {
            return;
        }

        try
        {
            var contributions = ClassContributionBuilder.Build(model, grid, evaluatedSeries, classAssignments, out _);
            foreach (var warning in DetectServiceWithBufferClassCoverageGaps(nodeDefinitions, evaluatedSeries, contributions))
            {
                warnings.Add(warning);
            }
        }
        catch (Exception ex)
        {
            warnings.Add(new InvariantWarning(
                "serviceWithBuffer",
                "class_coverage_failed",
                $"ServiceWithBuffer class coverage diagnostics failed: {ex.Message}",
                Array.Empty<int>(),
                null,
                "warning"));
        }
    }

    private static void AppendTopologyClassCoverageWarnings(
        ModelDefinition model,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        List<InvariantWarning> warnings)
    {
        if (model.Topology?.Nodes is null || model.Topology.Nodes.Count == 0)
        {
            return;
        }

        var classAssignments = ClassAssignmentMapBuilder.Build(model);
        if (classAssignments.Count == 0)
        {
            return;
        }

        if (!TryCreateTimeGrid(model.Grid, out var grid))
        {
            return;
        }

        try
        {
            var contributions = ClassContributionBuilder.Build(model, grid, evaluatedSeries, classAssignments, out _);
            foreach (var warning in DetectTopologyServiceWithBufferClassCoverageGaps(model.Topology.Nodes, evaluatedSeries, contributions))
            {
                warnings.Add(warning);
            }
            foreach (var warning in DetectTopologyNodeClassCoverageGaps(model.Topology.Nodes, evaluatedSeries, contributions))
            {
                warnings.Add(warning);
            }
        }
        catch (Exception ex)
        {
            warnings.Add(new InvariantWarning(
                "topology",
                "topology_class_coverage_failed",
                $"Topology class coverage diagnostics failed: {ex.Message}",
                Array.Empty<int>(),
                null,
                "warning"));
        }
    }

    internal static IReadOnlyList<InvariantWarning> DetectServiceWithBufferClassCoverageGaps(
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> contributions)
    {
        var warnings = new List<InvariantWarning>();

        foreach (var node in nodeDefinitions.Values)
        {
            if (!string.Equals(node.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Inflow))
            {
                continue;
            }

            var inflowClasses = ResolveClasses(contributions, node.Inflow);
            if (inflowClasses.Count == 0)
            {
                continue;
            }

            CheckTarget(node.Id, "outflow", node.Outflow, inflowClasses, evaluatedSeries, contributions, warnings);
            CheckTarget(node.Id, "loss", node.Loss, inflowClasses, evaluatedSeries, contributions, warnings);
        }

        return warnings;
    }

    internal static IReadOnlyList<InvariantWarning> DetectTopologyServiceWithBufferClassCoverageGaps(
        IReadOnlyList<TopologyNodeDefinition> topologyNodes,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> contributions)
    {
        var warnings = new List<InvariantWarning>();

        foreach (var node in topologyNodes)
        {
            if (!string.Equals(node.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var inflowClasses = ResolveClasses(contributions, node.Semantics?.Arrivals);
            if (inflowClasses.Count == 0)
            {
                continue;
            }

            CheckTarget(node.Id, "served", node.Semantics?.Served, inflowClasses, evaluatedSeries, contributions, warnings);
            CheckTarget(node.Id, "errors", node.Semantics?.Errors, inflowClasses, evaluatedSeries, contributions, warnings);
        }

        return warnings;
    }

    internal static IReadOnlyList<InvariantWarning> DetectTopologyNodeClassCoverageGaps(
        IReadOnlyList<TopologyNodeDefinition> topologyNodes,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> contributions)
    {
        var warnings = new List<InvariantWarning>();

        foreach (var node in topologyNodes)
        {
            if (string.Equals(node.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var inflowClasses = ResolveClasses(contributions, node.Semantics?.Arrivals);
            if (inflowClasses.Count == 0)
            {
                continue;
            }

            CheckTarget(node.Id, "served", node.Semantics?.Served, inflowClasses, evaluatedSeries, contributions, warnings);
            CheckTarget(node.Id, "errors", node.Semantics?.Errors, inflowClasses, evaluatedSeries, contributions, warnings);
        }

        return warnings;
    }

    private static HashSet<string> ResolveClasses(
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> contributions,
        string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return contributions.TryGetValue(new NodeId(nodeId), out var series)
            ? new HashSet<string>(series.Keys, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void CheckTarget(
        string serviceNodeId,
        string label,
        string? targetId,
        HashSet<string> inflowClasses,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> contributions,
        List<InvariantWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        if (IsEffectivelyZero(targetId, evaluatedSeries))
        {
            return;
        }

        var targetClasses = ResolveClasses(contributions, targetId);

        void AddWarning(string code, string message)
        {
            warnings.Add(new InvariantWarning(serviceNodeId, code, message, Array.Empty<int>(), null, "warning"));
        }

        if (targetClasses.Count == 0)
        {
            AddWarning(
                $"class_series_missing_{label}",
                $"Class coverage missing for serviceWithBuffer node '{serviceNodeId}' {label} '{targetId}'. Inflow classes: {string.Join(", ", inflowClasses)}.");
            return;
        }

        var missing = inflowClasses.Except(targetClasses, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            AddWarning(
                $"class_series_partial_{label}",
                $"Class coverage partial for serviceWithBuffer node '{serviceNodeId}' {label} '{targetId}'. Missing classes: {string.Join(", ", missing)}.");
        }
    }

    private static bool IsEffectivelyZero(string targetId, IReadOnlyDictionary<NodeId, double[]> evaluatedSeries)
    {
        if (!evaluatedSeries.TryGetValue(new NodeId(targetId), out var series))
        {
            return false;
        }

        foreach (var value in series)
        {
            if (!double.IsFinite(value))
            {
                return false;
            }

            if (Math.Abs(value) > defaultTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCreateTimeGrid(GridDefinition? gridDefinition, out TimeGrid grid)
    {
        grid = default;
        if (gridDefinition is null)
        {
            return false;
        }

        try
        {
            var unit = TimeUnitExtensions.Parse(gridDefinition.BinUnit ?? "minutes");
            grid = new TimeGrid(gridDefinition.Bins, gridDefinition.BinSize, unit);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, double> BuildQueueInitials(IEnumerable<TopologyNodeDefinition> nodes)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var queueId = node.Semantics?.QueueDepth;
            if (!string.IsNullOrWhiteSpace(queueId))
            {
                map[queueId] = node.InitialCondition?.QueueDepth ?? 0d;
            }
        }

        return map;
    }

    private static void AppendPostEvalInjectionWarnings(
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        List<InvariantWarning> warnings)
    {
        if (nodeDefinitions.Count == 0 || evaluatedSeries.Count == 0)
        {
            return;
        }

        var unknown = evaluatedSeries.Keys
            .Select(key => key.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value) && !nodeDefinitions.ContainsKey(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknown.Count == 0)
        {
            return;
        }

        warnings.Add(new InvariantWarning(
            "engine",
            "post_eval_injection",
            $"Evaluated series contains nodes not present in the model DAG: {string.Join(", ", unknown)}.",
            Array.Empty<int>(),
            null,
            "warning"));
    }
}

public sealed record InvariantWarning(
    string NodeId,
    string Code,
    string Message,
    IReadOnlyList<int> Bins,
    double? Value,
    string Severity = "warning",
    IReadOnlyList<string>? EdgeIds = null);

public sealed record InvariantAnalysisResult(IReadOnlyList<InvariantWarning> Warnings);
