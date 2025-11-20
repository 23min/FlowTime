using System.Linq;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Analysis;

/// <summary>
/// Analyzes evaluated model series for conservation and sanity issues.
/// </summary>
public static class InvariantAnalyzer
{
    private const double DefaultTolerance = 1e-6;

    public static InvariantAnalysisResult Analyze(
        ModelDefinition model,
        IReadOnlyDictionary<NodeId, double[]> evaluatedSeries,
        double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(evaluatedSeries);

        if (model.Topology?.Nodes is null || model.Topology.Nodes.Count == 0)
        {
            return new InvariantAnalysisResult(Array.Empty<InvariantWarning>());
        }

        var warnings = new List<InvariantWarning>();
        var cache = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var queueSeeds = BuildQueueInitials(model.Topology.Nodes);
        var incomingEdges = new Dictionary<string, List<TopologyEdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
        var outgoingEdges = new Dictionary<string, List<TopologyEdgeDefinition>>(StringComparer.OrdinalIgnoreCase);

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

        foreach (var topoNode in model.Topology.Nodes)
        {
            if (string.IsNullOrWhiteSpace(topoNode.Id) || topoNode.Semantics is null)
            {
                continue;
            }

            var semantics = topoNode.Semantics;
            var nodeId = topoNode.Id;
            var nodeKind = (topoNode.Kind ?? string.Empty).Trim().ToLowerInvariant();
            var isServiceKind = nodeKind == "service" || nodeKind == "router";
            var isQueueKind = nodeKind == "queue";
            var isQueueLikeKind = nodeKind is "queue" or "dlq";
            var isDlqKind = nodeKind == "dlq";
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
            if (arrivals != null && served != null)
            {
                CheckDiff(nodeId, "served_exceeds_arrivals",
                    "Served volume exceeded arrivals",
                    served, arrivals, greaterTolerance: true);
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

            // Queue depth validation
            if (queueDepth != null && arrivals != null && served != null)
            {
                var seed = (semantics.QueueDepth != null && queueSeeds.TryGetValue(semantics.QueueDepth, out var val))
                    ? val
                    : 0d;
                ValidateQueue(nodeId, queueDepth, arrivals, served, errors, seed);
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
            else if (capacity != null && capacity.All(v => Math.Abs(v) <= tolerance))
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "capacity_all_zero",
                    "Capacity series is zero for all bins; utilization will be unavailable.",
                    Array.Empty<int>(),
                    null,
                    "info"));
            }

            if (expectsServed && served == null)
            {
                warnings.Add(new InvariantWarning(
                    nodeId,
                    "missing_served_series",
                    "Served/output series was not available; utilization cannot be computed.",
                    Array.Empty<int>(),
                    null,
                    "info"));
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

            if (!isDlqKind && !isTerminalQueue && queueDepth != null && served != null)
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
                    warnings.Add(new InvariantWarning(
                        nodeId,
                        "latency_uncomputable_bins",
                        "Queue latency could not be computed where served was zero and queue depth was positive.",
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

        return warnings.Count == 0
            ? new InvariantAnalysisResult(Array.Empty<InvariantWarning>())
            : new InvariantAnalysisResult(warnings);

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

        void ValidateQueue(string nodeId, double[] queueDepth, double[] inflow, double[] outflow, double[]? loss, double seed)
        {
            if (inflow.Length != outflow.Length || queueDepth.Length != inflow.Length)
            {
                return;
            }

            Span<int> bins = stackalloc int[5];
            var binCount = 0;
            double expected = seed;
            for (var i = 0; i < queueDepth.Length; i++)
            {
                expected = Math.Max(0, expected + inflow[i] - outflow[i] - (loss?[i] ?? 0));
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
                    "Queue depth does not match inflow/outflow accumulation",
                    bins[..binCount].ToArray(),
                    null));
            }
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

        static bool IsTerminalEdge(TopologyEdgeDefinition edge) =>
            !string.IsNullOrWhiteSpace(edge.Type) &&
            edge.Type.Trim().Equals("terminal", StringComparison.OrdinalIgnoreCase);

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
}

public sealed record InvariantWarning(
    string NodeId,
    string Code,
    string Message,
    IReadOnlyList<int> Bins,
    double? Value,
    string Severity = "warning");

public sealed record InvariantAnalysisResult(IReadOnlyList<InvariantWarning> Warnings);
