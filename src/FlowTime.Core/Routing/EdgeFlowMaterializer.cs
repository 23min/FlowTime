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
            .Where(candidate => ShouldEmitFlowEdge(candidate.Edge))
            .GroupBy(candidate => new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId), EdgeKeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.First(), EdgeKeyComparer.Instance);

        var result = new List<RunArtifactWriter.EdgeSeriesInput>();
        var handledRouterEdges = new HashSet<EdgeKey>(EdgeKeyComparer.Instance);

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

                AddEdgeSeriesMetric(result, candidate.Edge, "flowTotal", routeTotal, routeClasses);
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

                AddEdgeSeriesMetric(result, candidate.Edge, "flowTotal", routeTotal, routeClasses);
                handledRouterEdges.Add(new EdgeKey(candidate.SourceNodeId, candidate.TargetNodeId));
            }
        }

        foreach (var group in edgeCandidates
            .Where(candidate => !routerIds.Contains(candidate.SourceNodeId))
            .Where(candidate => ShouldEmitFlowEdge(candidate.Edge))
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

            var attemptsSeriesId = ResolveAttemptsSeriesId(node.Semantics);
            double[]? attemptsSeries = null;
            var hasAttemptsSeries = !string.IsNullOrWhiteSpace(attemptsSeriesId) &&
                totals.TryGetValue(new NodeId(attemptsSeriesId!), out attemptsSeries);
            Dictionary<string, double[]>? classSeriesForAttempts = null;
            if (hasAttemptsSeries && classSeries != null && classSeries.TryGetValue(new NodeId(attemptsSeriesId!), out var attemptsByClass))
            {
                classSeriesForAttempts = attemptsByClass.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
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

                AddEdgeSeriesMetric(result, candidate.Edge, "flowTotal", series, routeClasses);
                if (hasAttemptsSeries)
                {
                    var attemptsSeriesScaled = ScaleSeries(attemptsSeries!, fraction);
                    Dictionary<string, double[]>? attemptsClasses = null;
                    if (classSeriesForAttempts != null)
                    {
                        attemptsClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (classId, classValues) in classSeriesForAttempts)
                        {
                            attemptsClasses[classId] = ScaleSeries(classValues, fraction);
                        }
                    }

                    AddEdgeSeriesMetric(result, candidate.Edge, "attemptsLoad", attemptsSeriesScaled, attemptsClasses);
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

    private static bool ShouldEmitFlowEdge(TopologyEdgeDefinition edge)
    {
        if (!IsServedMeasure(edge.Measure))
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

    private static bool IsServedMeasure(string? measure)
    {
        return string.IsNullOrWhiteSpace(measure) || measure.Equals("served", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveServedSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Served) ? null : semantics.Served;
    }

    private static string? ResolveAttemptsSeriesId(TopologyNodeSemanticsDefinition semantics)
    {
        return string.IsNullOrWhiteSpace(semantics.Attempts) ? null : semantics.Attempts;
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
