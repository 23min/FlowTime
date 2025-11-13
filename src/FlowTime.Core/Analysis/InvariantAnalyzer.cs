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

        foreach (var topoNode in model.Topology.Nodes)
        {
            if (string.IsNullOrWhiteSpace(topoNode.Id) || topoNode.Semantics is null)
            {
                continue;
            }

            var semantics = topoNode.Semantics;
            var nodeId = topoNode.Id;

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
            if (!TryGetSeries(semantics.QueueDepth, out var queueDepth))
            {
                queueDepth = null;
            }
            if (!TryGetSeries(semantics.RetryEcho, out var retryEcho))
            {
                retryEcho = null;
            }

            // Non-negative checks
            CheckNonNegative(nodeId, "arrivals_negative", "Arrivals produced negative values", arrivals);
            CheckNonNegative(nodeId, "served_negative", "Served produced negative values", served);
            CheckNonNegative(nodeId, "errors_negative", "Errors produced negative values", errors);
            CheckNonNegative(nodeId, "attempts_negative", "Attempts produced negative values", attempts);
            CheckNonNegative(nodeId, "failures_negative", "Failures produced negative values", failures);
            CheckNonNegative(nodeId, "queue_negative", "Queue depth produced negative values", queueDepth);
            CheckNonNegative(nodeId, "retry_echo_negative", "Retry echo produced negative values", retryEcho);

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

            // Queue depth validation
            if (queueDepth != null && arrivals != null && served != null)
            {
                var seed = (semantics.QueueDepth != null && queueSeeds.TryGetValue(semantics.QueueDepth, out var val))
                    ? val
                    : 0d;
                ValidateQueue(nodeId, queueDepth, arrivals, served, errors, seed);
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
    double? Value);

public sealed record InvariantAnalysisResult(IReadOnlyList<InvariantWarning> Warnings);
