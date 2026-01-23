using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Routing;

public static class EdgeFlowMaterializer
{
    public static IReadOnlyList<RunArtifactWriter.EdgeSeriesInput> BuildEdgeFlowSeries(
        ModelDefinition model,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals)
    {
        if (model.Topology?.Edges is null || model.Topology.Edges.Count == 0)
        {
            return Array.Empty<RunArtifactWriter.EdgeSeriesInput>();
        }

        var topologyNodes = model.Topology.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

        var arrivalLookup = model.Topology.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Semantics?.Arrivals))
            .ToDictionary(n => n.Semantics!.Arrivals, n => n.Id, StringComparer.OrdinalIgnoreCase);

        var edgeCandidates = model.Topology.Edges
            .Select(edge => new EdgeCandidate(edge, ExtractNodeId(edge.Source), ExtractNodeId(edge.Target)))
            .Where(edge => !string.IsNullOrWhiteSpace(edge.SourceNodeId) && !string.IsNullOrWhiteSpace(edge.TargetNodeId))
            .ToList();

        var routerSpecs = RouterSpecificationBuilder.Build(model);
        var routerIds = new HashSet<string>(routerSpecs.Keys.Select(r => r.Value), StringComparer.OrdinalIgnoreCase);

        var classAssignments = ClassAssignmentMapBuilder.Build(model);
        IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>>? classSeries = null;
        if (classAssignments.Count > 0)
        {
            classSeries = ClassContributionBuilder.Build(model, grid, totals, classAssignments, out _)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        var edgeLookup = edgeCandidates
            .Where(candidate => ShouldEmitThroughputEdge(candidate.Edge))
            .GroupBy(candidate => new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId), EdgeKeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.First(), EdgeKeyComparer.Instance);

        var result = new List<RunArtifactWriter.EdgeSeriesInput>();
        var handledRouterEdges = new HashSet<EdgeKey>(EdgeKeyComparer.Instance);
        var flowVolumes = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        var throughputEdgesByTarget = new Dictionary<string, List<EdgeCandidate>>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in routerSpecs.Values)
        {
            var sourceSeriesId = spec.SourceId;
            if (!totals.TryGetValue(sourceSeriesId, out var sourceTotals))
            {
                continue;
            }

            var length = sourceTotals.Length;
            var remainingTotals = (double[])sourceTotals.Clone();
            Dictionary<string, double[]>? remainingClasses = null;
            if (classSeries != null && classSeries.TryGetValue(sourceSeriesId, out var sourceClasses))
            {
                remainingClasses = sourceClasses.ToDictionary(entry => entry.Key, entry => (double[])entry.Value.Clone(), StringComparer.OrdinalIgnoreCase);
            }

            foreach (var route in spec.Routes.Where(route => route.Classes.Count > 0))
            {
                if (!TryResolveEdge(route, spec.RouterId.Value, arrivalLookup, edgeLookup, out var candidate))
                {
                    continue;
                }

                var routeTotal = new double[length];
                Dictionary<string, double[]>? routeClasses = null;
                if (remainingClasses != null)
                {
                    routeClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var classId in route.Classes)
                    {
                        if (remainingClasses.TryGetValue(classId, out var series))
                        {
                            AddSeries(routeTotal, series);
                            SubtractSeries(remainingTotals, series);
                            remainingClasses.Remove(classId);
                            routeClasses[classId] = (double[])series.Clone();
                        }
                    }
                }

                var edgeId = GetEdgeId(candidate.Edge);
                flowVolumes[edgeId] = routeTotal;
                RegisterThroughputEdge(throughputEdgesByTarget, candidate);
                AddEdgeSeriesMetric(result, candidate.Edge, "flowVolume", routeTotal, routeClasses);
                handledRouterEdges.Add(new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId));
            }

            var weightRoutes = spec.Routes.Where(route => route.Classes.Count == 0).ToList();
            if (weightRoutes.Count == 0)
            {
                continue;
            }

            var totalWeight = weightRoutes.Sum(route => route.Weight);
            if (totalWeight <= 0)
            {
                totalWeight = weightRoutes.Count;
            }

            foreach (var route in weightRoutes)
            {
                if (!TryResolveEdge(route, spec.RouterId.Value, arrivalLookup, edgeLookup, out var candidate))
                {
                    continue;
                }

                var fraction = totalWeight <= 0 ? 0d : route.Weight / totalWeight;
                var routeTotal = ScaleSeries(remainingTotals, fraction);
                Dictionary<string, double[]>? routeClasses = null;

                if (remainingClasses != null)
                {
                    routeClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (classId, series) in remainingClasses)
                    {
                        routeClasses[classId] = ScaleSeries(series, fraction);
                    }
                }

                var edgeId = GetEdgeId(candidate.Edge);
                flowVolumes[edgeId] = routeTotal;
                RegisterThroughputEdge(throughputEdgesByTarget, candidate);
                AddEdgeSeriesMetric(result, candidate.Edge, "flowVolume", routeTotal, routeClasses);
                handledRouterEdges.Add(new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId));
            }
        }

        foreach (var group in edgeCandidates
            .Where(candidate => !routerIds.Contains(candidate.SourceNodeId))
            .Where(candidate => ShouldEmitThroughputEdge(candidate.Edge))
            .GroupBy(candidate => candidate.SourceNodeId, StringComparer.OrdinalIgnoreCase))
        {
            if (!topologyNodes.TryGetValue(group.Key, out var node))
            {
                continue;
            }

            var servedSeriesId = ResolveServedSeriesId(node.Semantics);
            if (string.IsNullOrWhiteSpace(servedSeriesId))
            {
                continue;
            }

            if (!totals.TryGetValue(new NodeId(servedSeriesId), out var baseSeries))
            {
                continue;
            }

            Dictionary<string, double[]>? classSeriesForServed = null;
            if (classSeries != null && classSeries.TryGetValue(new NodeId(servedSeriesId), out var byClass))
            {
                classSeriesForServed = byClass.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            }

            var edges = group.ToList();
            var totalWeight = edges.Sum(edge => NormalizeWeight(edge.Edge.Weight));
            if (totalWeight <= 0)
            {
                totalWeight = edges.Count;
            }

            foreach (var candidate in edges)
            {
                var key = new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId);
                if (handledRouterEdges.Contains(key))
                {
                    continue;
                }

                var weight = NormalizeWeight(candidate.Edge.Weight);
                var fraction = totalWeight <= 0 ? 0d : weight / totalWeight;
                var series = ScaleSeries(baseSeries, fraction);

                Dictionary<string, double[]>? routeClasses = null;
                if (classSeriesForServed != null)
                {
                    routeClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (classId, classValues) in classSeriesForServed)
                    {
                        routeClasses[classId] = ScaleSeries(classValues, fraction);
                    }
                }

                var edgeId = GetEdgeId(candidate.Edge);
                flowVolumes[edgeId] = series;
                RegisterThroughputEdge(throughputEdgesByTarget, candidate);
                AddEdgeSeriesMetric(result, candidate.Edge, "flowVolume", series, routeClasses);
            }
        }

        foreach (var group in edgeCandidates
            .Where(candidate => !routerIds.Contains(candidate.SourceNodeId))
            .Where(candidate => IsEffortEdge(candidate.Edge))
            .GroupBy(candidate => candidate.SourceNodeId, StringComparer.OrdinalIgnoreCase))
        {
            if (!topologyNodes.TryGetValue(group.Key, out var node))
            {
                continue;
            }

            var servedSeriesId = ResolveServedSeriesId(node.Semantics);
            if (string.IsNullOrWhiteSpace(servedSeriesId))
            {
                continue;
            }

            if (!totals.TryGetValue(new NodeId(servedSeriesId), out var baseSeries))
            {
                continue;
            }

            Dictionary<string, double[]>? classSeriesForServed = null;
            if (classSeries != null && classSeries.TryGetValue(new NodeId(servedSeriesId), out var byClass))
            {
                classSeriesForServed = byClass.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            }

            var edges = group.ToList();
            var totalWeight = edges.Sum(edge => NormalizeWeight(edge.Edge.Weight));
            if (totalWeight <= 0)
            {
                totalWeight = edges.Count;
            }

            foreach (var candidate in edges)
            {
                var edgeId = GetEdgeId(candidate.Edge);
                if (flowVolumes.ContainsKey(edgeId))
                {
                    continue;
                }

                var weight = NormalizeWeight(candidate.Edge.Weight);
                var fraction = totalWeight <= 0 ? 0d : weight / totalWeight;
                var series = ScaleSeries(baseSeries, fraction);

                Dictionary<string, double[]>? routeClasses = null;
                if (classSeriesForServed != null)
                {
                    routeClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (classId, classValues) in classSeriesForServed)
                    {
                        routeClasses[classId] = ScaleSeries(classValues, fraction);
                    }
                }

                flowVolumes[edgeId] = series;
                AddEdgeSeriesMetric(result, candidate.Edge, "flowVolume", series, routeClasses);
            }
        }

        foreach (var candidate in edgeCandidates.Where(candidate => IsEffortEdge(candidate.Edge)))
        {
            var port = ExtractPort(candidate.Edge.Source);
            if (!string.IsNullOrWhiteSpace(port) && !port.Equals("out", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var measure = NormalizeEdgeMeasure(candidate.Edge);
            if (!IsEffortMeasure(measure))
            {
                continue;
            }

            if (!topologyNodes.TryGetValue(candidate.SourceNodeId, out var node))
            {
                continue;
            }

            var attemptsSeriesId = ResolveAttemptsSeriesId(node.Semantics);
            if (string.IsNullOrWhiteSpace(attemptsSeriesId) ||
                !totals.TryGetValue(new NodeId(attemptsSeriesId), out var attemptsSeries))
            {
                continue;
            }

            var multiplier = NormalizeMultiplier(candidate.Edge.Multiplier ?? candidate.Edge.Weight);
            var attemptsScaled = ScaleSeries(attemptsSeries, multiplier);
            Dictionary<string, double[]>? attemptsClasses = null;
            if (classSeries != null && classSeries.TryGetValue(new NodeId(attemptsSeriesId), out var attemptsByClass))
            {
                attemptsClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var (classId, classValues) in attemptsByClass)
                {
                    attemptsClasses[classId] = ScaleSeries(classValues, multiplier);
                }
            }

            AddEdgeSeriesMetric(result, candidate.Edge, "attemptsVolume", attemptsScaled, attemptsClasses);
        }

        foreach (var candidate in edgeCandidates.Where(candidate => IsTerminalEdge(candidate.Edge)))
        {
            var measure = NormalizeEdgeMeasure(candidate.Edge);
            if (!IsFailureMeasure(measure))
            {
                continue;
            }

            if (!topologyNodes.TryGetValue(candidate.SourceNodeId, out var node))
            {
                continue;
            }

            var seriesId = ResolveTerminalSeriesId(node.Semantics, measure);
            if (string.IsNullOrWhiteSpace(seriesId) ||
                !totals.TryGetValue(new NodeId(seriesId), out var failureSeries))
            {
                continue;
            }

            var multiplier = NormalizeMultiplier(candidate.Edge.Multiplier ?? candidate.Edge.Weight);
            var failuresScaled = ScaleSeries(failureSeries, multiplier);
            Dictionary<string, double[]>? failuresByClass = null;
            if (classSeries != null && classSeries.TryGetValue(new NodeId(seriesId), out var classFailureSeries))
            {
                failuresByClass = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var (classId, classValues) in classFailureSeries)
                {
                    failuresByClass[classId] = ScaleSeries(classValues, multiplier);
                }
            }

            AddEdgeSeriesMetric(result, candidate.Edge, "failuresVolume", failuresScaled, failuresByClass);
        }

        foreach (var (targetNodeId, incomingEdges) in throughputEdgesByTarget)
        {
            if (!topologyNodes.TryGetValue(targetNodeId, out var node))
            {
                continue;
            }

            var retryEchoSeriesId = ResolveRetryEchoSeriesId(node.Semantics);
            if (string.IsNullOrWhiteSpace(retryEchoSeriesId) ||
                !totals.TryGetValue(new NodeId(retryEchoSeriesId), out var retrySeries))
            {
                continue;
            }

            if (incomingEdges.Count == 0)
            {
                continue;
            }

            var edgeInfos = incomingEdges
                .Select(edge =>
                {
                    var edgeId = GetEdgeId(edge.Edge);
                    flowVolumes.TryGetValue(edgeId, out var flowSeries);
                    return (edge.Edge, EdgeId: edgeId, Flow: flowSeries);
                })
                .ToList();

            var perEdgeSeries = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in edgeInfos)
            {
                if (!perEdgeSeries.ContainsKey(info.EdgeId))
                {
                    perEdgeSeries[info.EdgeId] = new double[retrySeries.Length];
                }
            }

            for (var i = 0; i < retrySeries.Length; i++)
            {
                var retryValue = retrySeries[i];
                if (!double.IsFinite(retryValue) || retryValue <= 0)
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
                    if (double.IsFinite(flowValue) && flowValue > 0)
                    {
                        totalFlow += flowValue;
                    }
                }

                if (totalFlow <= 0)
                {
                    var perEdge = retryValue / edgeInfos.Count;
                    foreach (var info in edgeInfos)
                    {
                        perEdgeSeries[info.EdgeId][i] = perEdge;
                    }

                    continue;
                }

                foreach (var info in edgeInfos)
                {
                    var flowValue = info.Flow is null ? 0d : info.Flow[i];
                    if (!double.IsFinite(flowValue) || flowValue <= 0)
                    {
                        continue;
                    }

                    perEdgeSeries[info.EdgeId][i] = retryValue * (flowValue / totalFlow);
                }
            }

            foreach (var info in edgeInfos)
            {
                if (perEdgeSeries[info.EdgeId].Any(value => value > 0))
                {
                    AddEdgeSeriesMetric(result, info.Edge, "retryVolume", perEdgeSeries[info.EdgeId], null);
                }
            }
        }

        return result;
    }

    private static void AddEdgeSeriesMetric(
        List<RunArtifactWriter.EdgeSeriesInput> result,
        TopologyEdgeDefinition edge,
        string metric,
        double[] values,
        Dictionary<string, double[]>? byClass)
    {
        var edgeId = string.IsNullOrWhiteSpace(edge.Id)
            ? $"{edge.Source}->{edge.Target}"
            : edge.Id!;

        result.Add(new RunArtifactWriter.EdgeSeriesInput
        {
            EdgeId = edgeId,
            Metric = metric,
            Values = values
        });

        if (byClass is null)
        {
            return;
        }

        foreach (var (classId, series) in byClass)
        {
            result.Add(new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = edgeId,
                Metric = metric,
                Values = series,
                ClassId = classId
            });
        }
    }

    private static bool TryResolveEdge(
        RouterRouteSpec route,
        string routerId,
        IReadOnlyDictionary<string, string> arrivalLookup,
        IReadOnlyDictionary<EdgeKey, EdgeCandidate> edgeLookup,
        out EdgeCandidate candidate)
    {
        candidate = default!;
        if (!arrivalLookup.TryGetValue(route.TargetId.Value, out var targetNodeId))
        {
            return false;
        }

        var key = new EdgeKey(routerId, targetNodeId);
        if (!edgeLookup.TryGetValue(key, out var resolved))
        {
            return false;
        }

        candidate = resolved;
        return true;
    }

    private static bool ShouldEmitThroughputEdge(TopologyEdgeDefinition edge)
    {
        if (!IsThroughputEdge(edge) || !IsServedMeasure(edge))
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

    private static bool IsServedMeasure(TopologyEdgeDefinition edge)
    {
        var measure = NormalizeEdgeMeasure(edge);
        return string.Equals(measure, "served", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailureMeasure(string? measure)
    {
        if (string.IsNullOrWhiteSpace(measure))
        {
            return false;
        }

        return string.Equals(measure, "errors", StringComparison.OrdinalIgnoreCase)
            || string.Equals(measure, "failures", StringComparison.OrdinalIgnoreCase)
            || string.Equals(measure, "exhaustedfailures", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEffortMeasure(string? measure)
    {
        if (string.IsNullOrWhiteSpace(measure))
        {
            return false;
        }

        return string.Equals(measure, "attempts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(measure, "load", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThroughputEdge(TopologyEdgeDefinition edge)
    {
        var type = NormalizeEdgeType(edge);
        return type == "throughput" || type == "topology";
    }

    private static bool IsEffortEdge(TopologyEdgeDefinition edge)
    {
        var type = NormalizeEdgeType(edge);
        return type == "effort" || type == "dependency";
    }

    private static bool IsTerminalEdge(TopologyEdgeDefinition edge)
    {
        var type = NormalizeEdgeType(edge);
        return type == "terminal";
    }

    private static string NormalizeEdgeType(TopologyEdgeDefinition edge)
    {
        if (string.IsNullOrWhiteSpace(edge.Type))
        {
            return "throughput";
        }

        return edge.Type.Trim().ToLowerInvariant();
    }

    private static string NormalizeEdgeMeasure(TopologyEdgeDefinition edge)
    {
        if (!string.IsNullOrWhiteSpace(edge.Measure))
        {
            return edge.Measure.Trim().ToLowerInvariant();
        }

        var port = ExtractPort(edge.Source);
        if (!string.IsNullOrWhiteSpace(port) && !port.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            return port.Trim().ToLowerInvariant();
        }

        return "served";
    }

    private static string? ResolveServedSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Served) ? null : semantics.Served;
    }

    private static string? ResolveAttemptsSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Attempts) ? null : semantics.Attempts;
    }

    private static string? ResolveErrorsSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Errors) ? null : semantics.Errors;
    }

    private static string? ResolveFailuresSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Failures) ? null : semantics.Failures;
    }

    private static string? ResolveExhaustedFailuresSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.ExhaustedFailures) ? null : semantics.ExhaustedFailures;
    }

    private static string? ResolveRetryEchoSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.RetryEcho) ? null : semantics.RetryEcho;
    }

    private static string? ResolveTerminalSeriesId(TopologyNodeSemanticsDefinition semantics, string? measure)
    {
        if (string.IsNullOrWhiteSpace(measure))
        {
            return null;
        }

        if (string.Equals(measure, "errors", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveErrorsSeriesId(semantics);
        }

        if (string.Equals(measure, "exhaustedfailures", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExhaustedFailuresSeriesId(semantics);
        }

        if (string.Equals(measure, "failures", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveFailuresSeriesId(semantics);
        }

        return null;
    }

    private static string GetEdgeId(TopologyEdgeDefinition edge)
    {
        return string.IsNullOrWhiteSpace(edge.Id)
            ? $"{edge.Source}->{edge.Target}"
            : edge.Id!;
    }

    private static void RegisterThroughputEdge(
        Dictionary<string, List<EdgeCandidate>> edgesByTarget,
        EdgeCandidate candidate)
    {
        if (!edgesByTarget.TryGetValue(candidate.TargetNodeId, out var list))
        {
            list = new List<EdgeCandidate>();
            edgesByTarget[candidate.TargetNodeId] = list;
        }

        var edgeId = GetEdgeId(candidate.Edge);
        if (list.Any(entry => string.Equals(GetEdgeId(entry.Edge), edgeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        list.Add(candidate);
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

    private static string ExtractPort(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf(':');
        return separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : string.Empty;
    }

    private static double NormalizeWeight(double weight) => weight <= 0 ? 1d : weight;

    private static double NormalizeMultiplier(double? raw)
    {
        if (!raw.HasValue || double.IsNaN(raw.Value) || double.IsInfinity(raw.Value))
        {
            return 1d;
        }

        return raw.Value;
    }

    private static double[] ScaleSeries(double[] source, double fraction)
    {
        var series = new double[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            series[i] = source[i] * fraction;
        }

        return series;
    }

    private static void AddSeries(double[] destination, double[] source)
    {
        var limit = Math.Min(destination.Length, source.Length);
        for (var i = 0; i < limit; i++)
        {
            destination[i] += source[i];
        }
    }

    private static void SubtractSeries(double[] destination, double[] source)
    {
        var limit = Math.Min(destination.Length, source.Length);
        for (var i = 0; i < limit; i++)
        {
            destination[i] -= source[i];
        }
    }

    private sealed record EdgeCandidate(TopologyEdgeDefinition Edge, string SourceNodeId, string TargetNodeId);

    private sealed record EdgeKey(string Source, string Target);

    private sealed class EdgeKeyComparer : IEqualityComparer<EdgeKey>
    {
        public static readonly EdgeKeyComparer Instance = new();

        public bool Equals(EdgeKey? x, EdgeKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Source, y.Source, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Target, y.Target, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(EdgeKey obj)
        {
            return HashCode.Combine(
                obj.Source?.ToLowerInvariant(),
                obj.Target?.ToLowerInvariant());
        }
    }
}
