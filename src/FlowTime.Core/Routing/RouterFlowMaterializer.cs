using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Execution;

namespace FlowTime.Core.Routing;

public static class RouterFlowMaterializer
{
    public static IReadOnlyDictionary<NodeId, double[]> ComputeOverrides(
        ModelDefinition model,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, string>? classAssignments = null)
    {
        var specs = RouterSpecificationBuilder.Build(model);
        if (specs.Count == 0)
        {
            return new Dictionary<NodeId, double[]>(new NodeIdComparer());
        }

        var assignments = classAssignments ?? ClassAssignmentMapBuilder.Build(model);
        Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>? classSeries = null;
        if (assignments.Count > 0)
        {
            classSeries = ClassContributionBuilder.Build(model, grid, totals, assignments, out _)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, new NodeIdComparer());
        }

        var overrides = new Dictionary<NodeId, double[]>(new NodeIdComparer());
        foreach (var spec in specs.Values)
        {
            if (!totals.TryGetValue(spec.SourceId, out var source))
            {
                continue;
            }

            var length = source.Length;
            var remainingTotals = (double[])source.Clone();
            var remainingClasses = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            if (classSeries != null && classSeries.TryGetValue(spec.SourceId, out var sourceClasses))
            {
                foreach (var (classId, series) in sourceClasses)
                {
                    remainingClasses[classId] = (double[])series.Clone();
                }
            }

            foreach (var route in spec.Routes.Where(r => r.Classes.Count > 0))
            {
                var routeTotal = new double[length];
                foreach (var classId in route.Classes)
                {
                    if (remainingClasses.TryGetValue(classId, out var classSeriesForClass))
                    {
                        AddSeries(routeTotal, classSeriesForClass);
                        SubtractSeries(remainingTotals, classSeriesForClass);
                        remainingClasses.Remove(classId);
                    }
                }

                AccumulateOverride(overrides, route.TargetId, routeTotal);
            }

            var weightRoutes = spec.Routes.Where(r => r.Classes.Count == 0).ToList();
            if (weightRoutes.Count > 0)
            {
                var totalWeight = weightRoutes.Sum(r => r.Weight);
                if (totalWeight <= 0)
                {
                    totalWeight = weightRoutes.Count;
                }

                foreach (var route in weightRoutes)
                {
                    var fraction = totalWeight <= 0 ? 0d : route.Weight / totalWeight;
                    var routeTotal = ScaleSeries(remainingTotals, fraction);
                    AccumulateOverride(overrides, route.TargetId, routeTotal);
                }
            }
        }

        return overrides;
    }

    private static void AccumulateOverride(
        IDictionary<NodeId, double[]> overrides,
        NodeId targetId,
        double[] values)
    {
        if (overrides.TryGetValue(targetId, out var existing))
        {
            AddSeries(existing, values);
            return;
        }

        overrides[targetId] = (double[])values.Clone();
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

    private static double[] ScaleSeries(double[] source, double fraction)
    {
        var series = new double[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            series[i] = source[i] * fraction;
        }

        return series;
    }
}
