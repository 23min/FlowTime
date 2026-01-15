using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlowTime.Core.Dispatching;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Routing;
using FlowTime.Expressions;
using ExpressionBinaryOpNode = FlowTime.Expressions.BinaryOpNode;

namespace FlowTime.Core.Artifacts;

internal sealed record DispatchScheduleParameters(int PeriodBins, int PhaseOffset);

internal static class ClassContributionBuilder
{
    private const double Tolerance = 1e-9;
    private const double RouterDiagnosticsTolerance = 1e-6;

    public static IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> Build(
        ModelDefinition model,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, string> classAssignments,
        out IReadOnlyList<RouterDiagnostic> routerDiagnostics)
    {
        if (classAssignments.Count == 0)
        {
            routerDiagnostics = Array.Empty<RouterDiagnostic>();
            return new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        }

        var topologySeeds = ExtractBacklogSeeds(model);
        var nodeDefinitions = model.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var routerSpecs = RouterSpecificationBuilder.Build(model);
        var parsedNodes = ModelParser.ParseNodes(model);
        var graph = new Graph(parsedNodes);
        var order = graph.TopologicalOrder();
        var contributions = new Dictionary<NodeId, ClassSeries>();

        foreach (var nodeId in order)
        {
            if (!totals.TryGetValue(nodeId, out var totalSeries))
            {
                continue;
            }

            if (classAssignments.TryGetValue(nodeId, out var assignedClass) &&
                !string.IsNullOrWhiteSpace(assignedClass))
            {
                contributions[nodeId] = ClassSeries.FromSingleClass(assignedClass, totalSeries);
                continue;
            }

            if (!nodeDefinitions.TryGetValue(nodeId.Value, out var nodeDefinition))
            {
                contributions[nodeId] = ClassSeries.FromTotals(totalSeries);
                continue;
            }

            var series = nodeDefinition.Kind switch
            {
                "const" or "pmf" => BuildSourceSeries(nodeId, totalSeries, classAssignments),
                "expr" => EvaluateExpressionNode(nodeDefinition, grid, totals, contributions),
                "serviceWithBuffer" => EvaluateServiceWithBufferNode(nodeDefinition, grid, totals, contributions, topologySeeds),
                _ => ClassSeries.FromTotals(totalSeries)
            };

            contributions[nodeId] = series;
        }

        var routerDiagnosticsList = new List<RouterDiagnostic>();

        var overriddenNodes = ApplyRouterContributions(routerSpecs, grid, totals, contributions, routerDiagnosticsList);
        if (overriddenNodes.Count > 0)
        {
            RecomputeContributions(
                graph,
                nodeDefinitions,
                grid,
                totals,
                classAssignments,
                topologySeeds,
                overriddenNodes,
                contributions);
        }

        var outflowOverrides = ApplyServiceWithBufferOutflowContributions(nodeDefinitions, grid, totals, contributions);
        if (outflowOverrides.Count > 0)
        {
            overriddenNodes.UnionWith(outflowOverrides);
            RecomputeContributions(
                graph,
                nodeDefinitions,
                grid,
                totals,
                classAssignments,
                topologySeeds,
                overriddenNodes,
                contributions);
        }

        var topologyOverrides = ApplyTopologyServiceWithBufferContributions(model, grid, totals, contributions);
        if (topologyOverrides.Count > 0)
        {
            overriddenNodes.UnionWith(topologyOverrides);
            RecomputeContributions(
                graph,
                nodeDefinitions,
                grid,
                totals,
                classAssignments,
                topologySeeds,
                overriddenNodes,
                contributions);
        }

        var result = new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        foreach (var (nodeId, series) in contributions)
        {
            if (series.ByClass.Count == 0)
            {
                continue;
            }

            result[nodeId] = series.ByClass.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        routerDiagnostics = routerDiagnosticsList;
        return result;
    }

    private static HashSet<NodeId> ApplyTopologyServiceWithBufferContributions(
        ModelDefinition model,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        Dictionary<NodeId, ClassSeries> contributions)
    {
        var updatedNodes = new HashSet<NodeId>(new NodeIdComparer());
        if (model.Topology?.Nodes is null || model.Topology.Nodes.Count == 0)
        {
            return updatedNodes;
        }

        foreach (var node in model.Topology.Nodes)
        {
            if (!string.Equals(node.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var arrivalsId = node.Semantics?.Arrivals;
            if (string.IsNullOrWhiteSpace(arrivalsId))
            {
                continue;
            }

            var inflowId = new NodeId(arrivalsId);
            if (!contributions.ContainsKey(inflowId) && !totals.ContainsKey(inflowId))
            {
                continue;
            }

            var inflow = GetRequiredNode(arrivalsId, grid, totals, contributions);
            if (inflow.ByClass.Count == 0)
            {
                continue;
            }

            TrackContribution(node.Semantics?.Served, inflow, totals, contributions, updatedNodes);
            TrackContribution(node.Semantics?.Errors, inflow, totals, contributions, updatedNodes);
        }

        return updatedNodes;
    }

    private static HashSet<NodeId> ApplyServiceWithBufferOutflowContributions(
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        Dictionary<NodeId, ClassSeries> contributions)
    {
        var updatedNodes = new HashSet<NodeId>(new NodeIdComparer());
        foreach (var nodeDefinition in nodeDefinitions.Values)
        {
            if (!string.Equals(nodeDefinition.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(nodeDefinition.Inflow))
            {
                continue;
            }

            var inflow = GetRequiredNode(nodeDefinition.Inflow, grid, totals, contributions);
            if (inflow.ByClass.Count == 0)
            {
                continue;
            }

            TrackContribution(nodeDefinition.Outflow, inflow, totals, contributions, updatedNodes);
            TrackContribution(nodeDefinition.Loss, inflow, totals, contributions, updatedNodes);
        }

        return updatedNodes;
    }

    private static void ApplyContributionForTarget(
        string? targetId,
        ClassSeries inflow,
        IReadOnlyDictionary<NodeId, double[]> totals,
        Dictionary<NodeId, ClassSeries> contributions)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var targetNodeId = new NodeId(targetId);
        if (!totals.TryGetValue(targetNodeId, out var targetTotals))
        {
            return;
        }

        if (contributions.TryGetValue(targetNodeId, out var existing) && existing.ByClass.Count > 0)
        {
            return;
        }

        var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (classId, series) in inflow.ByClass)
        {
            dict[classId] = (double[])series.Clone();
        }

        ClassSeries.NormalizeToTotal(dict, targetTotals);
        contributions[targetNodeId] = new ClassSeries((double[])targetTotals.Clone(), dict);
    }

    private static void TrackContribution(
        string? targetId,
        ClassSeries inflow,
        IReadOnlyDictionary<NodeId, double[]> totals,
        Dictionary<NodeId, ClassSeries> contributions,
        HashSet<NodeId> updatedNodes)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var targetNodeId = new NodeId(targetId);
        var hadClasses = contributions.TryGetValue(targetNodeId, out var existing) && existing.ByClass.Count > 0;

        ApplyContributionForTarget(targetId, inflow, totals, contributions);

        var hasClasses = contributions.TryGetValue(targetNodeId, out var updated) && updated.ByClass.Count > 0;
        if (!hadClasses && hasClasses)
        {
            updatedNodes.Add(targetNodeId);
        }
    }

    private static HashSet<NodeId> ApplyRouterContributions(
        IReadOnlyDictionary<NodeId, RouterSpec> routerSpecs,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        Dictionary<NodeId, ClassSeries> contributions,
        List<RouterDiagnostic> diagnostics)
    {
        var overridden = new HashSet<NodeId>(new NodeIdComparer());
        foreach (var spec in routerSpecs.Values)
        {
            var overrides = EvaluateRouterRoutes(spec, grid, totals, contributions, diagnostics);
            foreach (var (targetId, series) in overrides)
            {
                contributions[targetId] = series;
                overridden.Add(targetId);
            }

            contributions[spec.RouterId] = GetRouterSeries(spec, grid, totals, contributions);
            overridden.Add(spec.RouterId);
        }

        return overridden;
    }

    private static void RecomputeContributions(
        Graph graph,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, string> classAssignments,
        IReadOnlyDictionary<string, double> serviceWithBufferSeeds,
        HashSet<NodeId> overriddenNodes,
        Dictionary<NodeId, ClassSeries> contributions)
    {
        foreach (var nodeId in graph.TopologicalOrder())
        {
            if (!totals.ContainsKey(nodeId))
            {
                continue;
            }

            if (classAssignments.ContainsKey(nodeId) || overriddenNodes.Contains(nodeId))
            {
                continue;
            }

            if (!nodeDefinitions.TryGetValue(nodeId.Value, out var nodeDefinition))
            {
                continue;
            }

            var series = nodeDefinition.Kind switch
            {
                "const" or "pmf" => BuildSourceSeries(nodeId, totals[nodeId], classAssignments),
                "expr" => EvaluateExpressionNode(nodeDefinition, grid, totals, contributions),
                "serviceWithBuffer" => EvaluateServiceWithBufferNode(nodeDefinition, grid, totals, contributions, serviceWithBufferSeeds),
                _ => ClassSeries.FromTotals(totals[nodeId])
            };

            contributions[nodeId] = series;
        }
    }

    private static Dictionary<NodeId, ClassSeries> EvaluateRouterRoutes(
        RouterSpec spec,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions,
        List<RouterDiagnostic> diagnostics)
    {
        var result = new Dictionary<NodeId, ClassSeries>(new NodeIdComparer());
        var source = GetSourceSeries(spec.SourceId, grid.Length, totals, contributions);
        var remainder = CloneClassDictionary(source.ByClass);

        var classRoutes = spec.Routes.Where(r => r.Classes.Count > 0).ToList();
        foreach (var route in classRoutes)
        {
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var classId in route.Classes)
            {
                if (source.ByClass.TryGetValue(classId, out var series))
                {
                    dict[classId] = (double[])series.Clone();
                }
                else
                {
                    dict[classId] = new double[grid.Length];
                }

                remainder.Remove(classId);
            }

            var total = GetTotalsCopy(route.TargetId, totals, grid.Length);
            ClassSeries.NormalizeToTotal(dict, total);
            result[route.TargetId] = new ClassSeries(total, dict);
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
                var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var (classId, series) in remainder)
                {
                    dict[classId] = ScaleSeries(series, fraction);
                }

                var total = GetTotalsCopy(route.TargetId, totals, grid.Length);
                ClassSeries.NormalizeToTotal(dict, total);
                result[route.TargetId] = new ClassSeries(total, dict);
            }
        }

        EmitRouterDiagnostics(spec, source, remainder, weightRoutes.Count > 0, result.Values, diagnostics);

        return result;
    }

    private static ClassSeries GetRouterSeries(
        RouterSpec spec,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        return GetSourceSeries(spec.SourceId, grid.Length, totals, contributions);
    }

    private static Dictionary<string, double> ExtractBacklogSeeds(ModelDefinition model)
    {
        var seeds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (model.Topology?.Nodes is null)
        {
            return seeds;
        }

        foreach (var node in model.Topology.Nodes)
        {
            var queueId = node.Semantics?.QueueDepth;
            if (string.IsNullOrWhiteSpace(queueId))
            {
                continue;
            }

            seeds[queueId.Trim()] = node.InitialCondition?.QueueDepth ?? 0d;
        }

        return seeds;
    }

    private static ClassSeries BuildSourceSeries(
        NodeId nodeId,
        double[] totalSeries,
        IReadOnlyDictionary<NodeId, string> classAssignments)
    {
        if (!classAssignments.TryGetValue(nodeId, out var classId) || string.IsNullOrWhiteSpace(classId))
        {
            return ClassSeries.FromTotals(totalSeries);
        }

        return ClassSeries.FromSingleClass(classId, totalSeries);
    }

    private static ClassSeries EvaluateExpressionNode(
        NodeDefinition nodeDefinition,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (string.IsNullOrWhiteSpace(nodeDefinition.Expr))
        {
            return ClassSeries.FromTotals(totals[new NodeId(nodeDefinition.Id)]);
        }

        ExpressionNode ast;
        try
        {
            var parser = new ExpressionParser(nodeDefinition.Expr);
            ast = parser.Parse();
        }
        catch
        {
            return ClassSeries.FromTotals(totals[new NodeId(nodeDefinition.Id)]);
        }

        return EvaluateExpression(ast, grid, totals, contributions);
    }

    private static ClassSeries EvaluateExpression(
        ExpressionNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        return node switch
        {
            LiteralNode literal => ClassSeries.FromTotals(CreateLiteralSeries(literal.Value, grid.Length)),
            NodeReferenceNode reference => CloneSeries(contributions[new NodeId(reference.NodeId)]),
            ExpressionBinaryOpNode binary => EvaluateBinary(binary, grid, totals, contributions),
            FunctionCallNode call => EvaluateFunction(call, grid, totals, contributions),
            _ => ClassSeries.Zero(grid.Length)
        };
    }

    private static ClassSeries EvaluateBinary(
        ExpressionBinaryOpNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        var left = EvaluateExpression(node.Left, grid, totals, contributions);
        var right = EvaluateExpression(node.Right, grid, totals, contributions);

        return node.Operator switch
        {
            BinaryOperator.Add => ClassSeries.Add(left, right),
            BinaryOperator.Subtract => ClassSeries.Subtract(left, right),
            BinaryOperator.Multiply => ClassSeries.Multiply(left, right),
            BinaryOperator.Divide => ClassSeries.Divide(left, right),
            _ => ClassSeries.Zero(grid.Length)
        };
    }

    private static ClassSeries EvaluateFunction(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        var name = node.FunctionName.ToUpperInvariant();
        return name switch
        {
            "SHIFT" => EvaluateShift(node, grid, totals, contributions),
            "CONV" => EvaluateConvolution(node, grid, totals, contributions),
            "MIN" => EvaluateMin(node, grid, totals, contributions),
            "MAX" => EvaluateMax(node, grid, totals, contributions),
            "CLAMP" => EvaluateClamp(node, grid, totals, contributions),
            "MOD" => EvaluateMod(node, grid, totals, contributions),
            "FLOOR" => EvaluateUnary(node, grid, totals, contributions, ClassSeries.Floor),
            "CEIL" => EvaluateUnary(node, grid, totals, contributions, ClassSeries.Ceil),
            "ROUND" => EvaluateUnary(node, grid, totals, contributions, ClassSeries.Round),
            "STEP" => EvaluateStep(node, grid, totals, contributions),
            "PULSE" => EvaluatePulse(node, grid, totals, contributions),
            _ => ClassSeries.Zero(grid.Length)
        };
    }

    private static ClassSeries EvaluateShift(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2 || node.Arguments[1] is not LiteralNode lagLiteral)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var lag = (int)lagLiteral.Value;
        if (lag < 0 || Math.Abs(lag - lagLiteral.Value) > Tolerance)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var source = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        return ClassSeries.Shift(source, lag);
    }

    private static ClassSeries EvaluateConvolution(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var kernel = ExtractKernel(node.Arguments[1]);
        if (kernel.Length == 0)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var source = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        return ClassSeries.Convolve(source, kernel);
    }

    private static ClassSeries EvaluateMin(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var left = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        var right = EvaluateExpression(node.Arguments[1], grid, totals, contributions);
        return ClassSeries.Min(left, right);
    }

    private static ClassSeries EvaluateMax(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var left = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        var right = EvaluateExpression(node.Arguments[1], grid, totals, contributions);
        return ClassSeries.Max(left, right);
    }

    private static ClassSeries EvaluateClamp(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 3)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var value = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        var min = EvaluateExpression(node.Arguments[1], grid, totals, contributions);
        var max = EvaluateExpression(node.Arguments[2], grid, totals, contributions);
        return ClassSeries.Max(ClassSeries.Min(value, max), min);
    }

    private static ClassSeries EvaluateServiceWithBufferNode(
        NodeDefinition nodeDefinition,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions,
        IReadOnlyDictionary<string, double> seeds)
    {
        var totalSeries = totals[new NodeId(nodeDefinition.Id)];
        var inflow = GetRequiredNode(nodeDefinition.Inflow, grid, totals, contributions);
        var outflow = GetRequiredNode(nodeDefinition.Outflow, grid, totals, contributions);
        var loss = string.IsNullOrWhiteSpace(nodeDefinition.Loss)
            ? ClassSeries.Zero(grid.Length)
            : GetRequiredNode(nodeDefinition.Loss, grid, totals, contributions);

        var initial = seeds.TryGetValue(nodeDefinition.Id, out var seed) ? seed : 0d;
        DispatchScheduleParameters? schedule = null;
        ClassSeries? capacity = null;
        if (nodeDefinition.DispatchSchedule is not null)
        {
            var period = nodeDefinition.DispatchSchedule.PeriodBins;
            var phase = nodeDefinition.DispatchSchedule.PhaseOffset ?? 0;
            schedule = new DispatchScheduleParameters(
                period,
                DispatchScheduleProcessor.NormalizePhase(phase, period));
            if (!string.IsNullOrWhiteSpace(nodeDefinition.DispatchSchedule.CapacitySeries))
            {
                capacity = GetRequiredNode(
                    nodeDefinition.DispatchSchedule.CapacitySeries,
                    grid,
                    totals,
                    contributions);
            }
        }

        return ClassSeries.Backlog(totalSeries, inflow, outflow, loss, initial, schedule, capacity);
    }

    private static ClassSeries GetRequiredNode(
        string? nodeId,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return ClassSeries.Zero(grid.Length);
        }

        var id = new NodeId(nodeId);
        if (!contributions.TryGetValue(id, out var series))
        {
            series = ClassSeries.FromTotals(totals[id]);
        }

        return series;
    }

    private static double[] CreateLiteralSeries(double value, int length)
    {
        var series = new double[length];
        for (var i = 0; i < length; i++)
        {
            series[i] = value;
        }

        return series;
    }

    private static double[] ExtractKernel(ExpressionNode node)
    {
        return node switch
        {
            ArrayLiteralNode array => array.Values.ToArray(),
            LiteralNode literal => new[] { literal.Value },
            _ => Array.Empty<double>()
        };
    }

    private static ClassSeries CloneSeries(ClassSeries source)
    {
        var total = (double[])source.Total.Clone();
        var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (classId, values) in source.ByClass)
        {
            dict[classId] = (double[])values.Clone();
        }

        return new ClassSeries(total, dict);
    }

    private static ClassSeries GetSourceSeries(
        NodeId sourceId,
        int length,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (contributions.TryGetValue(sourceId, out var series))
        {
            return CloneSeries(series);
        }

        if (totals.TryGetValue(sourceId, out var total))
        {
            return ClassSeries.FromTotals(total);
        }

        return ClassSeries.Zero(length);
    }

    private static Dictionary<string, double[]> CloneClassDictionary(IReadOnlyDictionary<string, double[]> source)
    {
        var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (classId, series) in source)
        {
            dict[classId] = (double[])series.Clone();
        }

        return dict;
    }

    private static double[] ScaleSeries(double[] series, double fraction)
    {
        var clone = new double[series.Length];
        for (var i = 0; i < series.Length; i++)
        {
            clone[i] = series[i] * fraction;
        }

        return clone;
    }

    private static double[] GetTotalsCopy(NodeId nodeId, IReadOnlyDictionary<NodeId, double[]> totals, int length)
    {
        if (totals.TryGetValue(nodeId, out var total))
        {
            return (double[])total.Clone();
        }

        return new double[length];
    }

    private static void EmitRouterDiagnostics(
        RouterSpec spec,
        ClassSeries source,
        Dictionary<string, double[]> remainder,
        bool hasWeightedRoutes,
        IEnumerable<ClassSeries> routedSeries,
        List<RouterDiagnostic> diagnostics)
    {
        if (!hasWeightedRoutes)
        {
            var missing = remainder
                .Where(kvp => HasNonZero(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToArray();

            if (missing.Length > 0)
            {
                diagnostics.Add(new RouterDiagnostic(
                    spec.RouterId.Value,
                    "router_missing_class_route",
                    $"Router '{spec.RouterId.Value}' is missing routes for classes: {string.Join(", ", missing)}."));
            }
        }

        var leakage = ComputeClassLeakage(source.ByClass, routedSeries);
        if (leakage.Count > 0)
        {
            var details = string.Join(", ", leakage.Select(kvp => $"{kvp.Key} (max diff {kvp.Value:0.###})"));
            diagnostics.Add(new RouterDiagnostic(
                spec.RouterId.Value,
                "router_class_leakage",
                $"Router '{spec.RouterId.Value}' routed class totals that differ from source: {details}."));
        }
    }

    private static Dictionary<string, double> ComputeClassLeakage(
        IReadOnlyDictionary<string, double[]> source,
        IEnumerable<ClassSeries> routedSeries)
    {
        var sums = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var series in routedSeries)
        {
            foreach (var (classId, values) in series.ByClass)
            {
                if (!sums.TryGetValue(classId, out var aggregate))
                {
                    aggregate = new double[values.Length];
                    sums[classId] = aggregate;
                }

                AddSeries(aggregate, values);
            }
        }

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (classId, sourceSeries) in source)
        {
            var routed = sums.TryGetValue(classId, out var aggregate) ? aggregate : new double[sourceSeries.Length];
            var diff = MaxAbsDifference(sourceSeries, routed);
            if (diff > RouterDiagnosticsTolerance)
            {
                result[classId] = diff;
            }
        }

        return result;
    }

    private static void AddSeries(double[] destination, double[] source)
    {
        var limit = Math.Min(destination.Length, source.Length);
        for (var i = 0; i < limit; i++)
        {
            destination[i] += source[i];
        }
    }

    private static bool HasNonZero(double[] series)
    {
        for (var i = 0; i < series.Length; i++)
        {
            if (Math.Abs(series[i]) > RouterDiagnosticsTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static double MaxAbsDifference(double[] left, double[] right)
    {
        var limit = Math.Min(left.Length, right.Length);
        var max = 0d;
        for (var i = 0; i < limit; i++)
        {
            var diff = Math.Abs(left[i] - right[i]);
            if (diff > max)
            {
                max = diff;
            }
        }

        return max;
    }

    internal sealed record RouterDiagnostic(string RouterId, string Code, string Message);

    private sealed class NodeIdComparer : IEqualityComparer<NodeId>
    {
        public bool Equals(NodeId x, NodeId y) =>
            string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(NodeId obj) =>
            obj.Value?.ToLowerInvariant().GetHashCode() ?? 0;
    }

    private static ClassSeries EvaluateUnary(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions,
        Func<ClassSeries, ClassSeries> op)
    {
        if (node.Arguments.Count != 1)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var operand = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        return op(operand);
    }

    private static ClassSeries EvaluateMod(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var left = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        var right = EvaluateExpression(node.Arguments[1], grid, totals, contributions);
        return ClassSeries.Mod(left, right);
    }

    private static ClassSeries EvaluateStep(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count != 2)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var value = EvaluateExpression(node.Arguments[0], grid, totals, contributions);
        var threshold = EvaluateExpression(node.Arguments[1], grid, totals, contributions);
        return ClassSeries.Step(value, threshold);
    }

    private static ClassSeries EvaluatePulse(
        FunctionCallNode node,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, ClassSeries> contributions)
    {
        if (node.Arguments.Count is < 1 or > 3)
        {
            return ClassSeries.Zero(grid.Length);
        }

        if (node.Arguments[0] is not LiteralNode periodLiteral)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var period = (int)periodLiteral.Value;
        if (period <= 0 || Math.Abs(period - periodLiteral.Value) > Tolerance)
        {
            return ClassSeries.Zero(grid.Length);
        }

        var phase = 0;
        if (node.Arguments.Count >= 2)
        {
            if (node.Arguments[1] is not LiteralNode phaseLiteral)
            {
                return ClassSeries.Zero(grid.Length);
            }

            phase = (int)phaseLiteral.Value;
            if (phase < 0 || Math.Abs(phase - phaseLiteral.Value) > Tolerance)
            {
                return ClassSeries.Zero(grid.Length);
            }
        }

        ClassSeries amplitude = node.Arguments.Count == 3
            ? EvaluateExpression(node.Arguments[2], grid, totals, contributions)
            : ClassSeries.FromTotals(CreateLiteralSeries(1d, grid.Length));

        return ClassSeries.Pulse(period, phase, amplitude);
    }

    private sealed class ClassSeries
    {
        public double[] Total { get; }
        public Dictionary<string, double[]> ByClass { get; }

        public ClassSeries(double[] total, Dictionary<string, double[]> byClass)
        {
            Total = total;
            ByClass = byClass;
        }

        public static ClassSeries FromTotals(double[] total) =>
            new((double[])total.Clone(), new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));

        public static ClassSeries FromSingleClass(string classId, double[] total)
        {
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                [classId] = (double[])total.Clone()
            };
            return new ClassSeries((double[])total.Clone(), dict);
        }

        public static ClassSeries Zero(int length) =>
            new(new double[length], new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));

        public static ClassSeries Add(ClassSeries left, ClassSeries right)
        {
            var total = Combine(left.Total, right.Total, (a, b) => a + b);
            var dict = Merge(left.ByClass, right.ByClass, (a, b) => a + b, total.Length);
        ClassSeries.NormalizeToTotal(dict, total);
        return new ClassSeries(total, dict);
    }

        public static ClassSeries Subtract(ClassSeries left, ClassSeries right)
        {
            var total = Combine(left.Total, right.Total, (a, b) => a - b);
            var dict = Merge(left.ByClass, right.ByClass, (a, b) => a - b, total.Length);
            ClassSeries.NormalizeToTotal(dict, total);
            return new ClassSeries(total, dict);
        }

        public static ClassSeries Multiply(ClassSeries left, ClassSeries right)
        {
            if (left.ByClass.Count > 0 && right.ByClass.Count == 0)
            {
                var total = Combine(left.Total, right.Total, (a, b) => a * b);
                var dict = MultiplyDictionary(left.ByClass, right.Total);
                return new ClassSeries(total, dict);
            }

            if (right.ByClass.Count > 0 && left.ByClass.Count == 0)
            {
                var total = Combine(left.Total, right.Total, (a, b) => a * b);
                var dict = MultiplyDictionary(right.ByClass, left.Total);
                return new ClassSeries(total, dict);
            }

            if (left.ByClass.Count == 0 && right.ByClass.Count == 0)
            {
                return new ClassSeries(Combine(left.Total, right.Total, (a, b) => a * b),
                    new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));
            }

            // Default: prefer left operand contributions
            var fallback = MultiplyDictionary(left.ByClass.Count > 0 ? left.ByClass : right.ByClass,
                left.ByClass.Count > 0 ? right.Total : left.Total);
            var totalSeries = Combine(left.Total, right.Total, (a, b) => a * b);
            return new ClassSeries(totalSeries, fallback);
        }

        public static ClassSeries Divide(ClassSeries left, ClassSeries right)
        {
            var total = Combine(left.Total, right.Total, (a, b) => b == 0 ? 0 : a / b);
            if (left.ByClass.Count == 0)
            {
                return new ClassSeries(total, new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));
            }

            var dict = MultiplyDictionary(left.ByClass, total, left.Total);
            return new ClassSeries(total, dict);
        }

        public static ClassSeries Shift(ClassSeries source, int lag)
        {
            if (lag <= 0)
            {
                return CloneSeries(source);
            }

            var length = source.Total.Length;
            var total = new double[length];
            Array.Copy(source.Total, 0, total, lag, Math.Max(0, length - lag));
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, values) in source.ByClass)
            {
                var shifted = new double[length];
                Array.Copy(values, 0, shifted, lag, Math.Max(0, length - lag));
                dict[classId] = shifted;
            }

            return new ClassSeries(total, dict);
        }

        public static ClassSeries Convolve(ClassSeries source, double[] kernel)
        {
            var total = ConvolveSeries(source.Total, kernel);
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, series) in source.ByClass)
            {
                dict[classId] = ConvolveSeries(series, kernel);
            }

            return new ClassSeries(total, dict);
        }

        public static ClassSeries Min(ClassSeries left, ClassSeries right)
        {
            return CombineMinMax(left, right, min: true);
        }

        public static ClassSeries Max(ClassSeries left, ClassSeries right)
        {
            return CombineMinMax(left, right, min: false);
        }

        public static ClassSeries Backlog(
            double[] totalSeries,
            ClassSeries inflow,
            ClassSeries outflow,
            ClassSeries loss,
            double initial,
            DispatchScheduleParameters? schedule = null,
            ClassSeries? capacityOverride = null)
        {
            if (schedule is not null)
            {
                ApplyDispatchSchedule(outflow, capacityOverride, schedule);
            }

            var length = totalSeries.Length;
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            var allClasses = inflow.ByClass.Keys
                .Concat(outflow.ByClass.Keys)
                .Concat(loss.ByClass.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            double totalInitial = initial;
            var inflow0 = inflow.ByClass.Sum(kvp => kvp.Value.Length > 0 ? kvp.Value[0] : 0d);
            if (Math.Abs(totalInitial) < Tolerance && inflow0 > 0)
            {
                totalInitial = inflow0;
            }

            foreach (var classId in allClasses)
            {
                var q = AllocateInitialPortion(classId, totalInitial, allClasses, inflow);
                var series = new double[length];
                var inflowSeries = inflow.ByClass.TryGetValue(classId, out var inflowArr)
                    ? inflowArr
                    : new double[length];
                var outflowSeries = outflow.ByClass.TryGetValue(classId, out var outArr)
                    ? outArr
                    : new double[length];
                var lossSeries = loss.ByClass.TryGetValue(classId, out var lossArr)
                    ? lossArr
                    : new double[length];

                for (var t = 0; t < length; t++)
                {
                    q = Math.Max(0d, q + Safe(inflowSeries, t) - Safe(outflowSeries, t) - Safe(lossSeries, t));
                    series[t] = q;
                }

                dict[classId] = series;
            }

            ClassSeries.NormalizeToTotal(dict, totalSeries);
            return new ClassSeries((double[])totalSeries.Clone(), dict);
        }

        private static double Safe(double[] source, int index)
        {
            if (index < 0 || index >= source.Length)
            {
                return 0d;
            }

            var value = source[index];
            return double.IsFinite(value) ? value : 0d;
        }

        private static double Safe(Dictionary<string, double[]> dict, string classId, int index)
        {
            if (!dict.TryGetValue(classId, out var series))
            {
                return 0d;
            }

            return Safe(series, index);
        }

        private static double AllocateInitialPortion(
            string classId,
            double totalInitial,
            IReadOnlyList<string> classIds,
            ClassSeries inflow)
        {
            var sum = 0d;
            var portions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in classIds)
            {
                var value = inflow.ByClass.TryGetValue(id, out var series) && series.Length > 0
                    ? Math.Max(0d, series[0])
                    : 0d;
                sum += value;
                portions[id] = value;
            }

            if (sum <= 0d || !double.IsFinite(sum))
            {
                return totalInitial / Math.Max(1, classIds.Count);
            }

            var share = totalInitial * (portions[classId] / sum);
            return share;
        }

        private static ClassSeries CombineMinMax(ClassSeries left, ClassSeries right, bool min)
        {
            var length = left.Total.Length;
            var result = new double[length];
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < length; i++)
            {
                var lv = left.Total[i];
                var rv = right.Total[i];
                var value = min ? Math.Min(lv, rv) : Math.Max(lv, rv);
                result[i] = value;
                var source = SelectSourceForMinMax(left, right, lv, rv, value);
                if (source is null)
                {
                    continue;
                }

                var sum = 0d;
                foreach (var arr in source.ByClass.Values)
                {
                    if (i < arr.Length)
                    {
                        var sample = arr[i];
                        if (double.IsFinite(sample))
                        {
                            sum += sample;
                        }
                    }
                }

                if (sum <= 0d || !double.IsFinite(sum))
                {
                    foreach (var classId in source.ByClass.Keys)
                    {
                        GetOrCreate(dict, classId, length);
                    }
                    continue;
                }

                var scale = value <= 0d ? 0d : value / sum;
                foreach (var (classId, series) in source.ByClass)
                {
                    var contribution = i < series.Length ? series[i] : 0d;
                    var scaled = contribution * scale;
                    if (double.IsNaN(scaled) || double.IsInfinity(scaled))
                    {
                        continue;
                    }

                    var arr = GetOrCreate(dict, classId, length);
                    arr[i] += scaled;
                }
            }

            ClassSeries.NormalizeToTotal(dict, result);
            return new ClassSeries(result, dict);
        }

        private static ClassSeries? SelectSourceForMinMax(
            ClassSeries left,
            ClassSeries right,
            double leftValue,
            double rightValue,
            double resultValue)
        {
            var leftMatches = Math.Abs(leftValue - resultValue) < Tolerance;
            var rightMatches = Math.Abs(rightValue - resultValue) < Tolerance;

            if (leftMatches && left.ByClass.Count > 0)
            {
                return left;
            }

            if (rightMatches && right.ByClass.Count > 0)
            {
                return right;
            }

            if (left.ByClass.Count > 0)
            {
                return left;
            }

            if (right.ByClass.Count > 0)
            {
                return right;
            }

            return null;
        }

        public static ClassSeries Floor(ClassSeries source) => ApplyUnary(source, Math.Floor);

        public static ClassSeries Ceil(ClassSeries source) => ApplyUnary(source, Math.Ceiling);

        public static ClassSeries Round(ClassSeries source) =>
            ApplyUnary(source, v => Math.Round(v, MidpointRounding.AwayFromZero));

        public static ClassSeries Step(ClassSeries value, ClassSeries threshold)
        {
            var length = value.Total.Length;
            var total = new double[length];
            for (var i = 0; i < length; i++)
            {
                var lhs = Safe(value.Total, i);
                var rhs = Safe(threshold.Total, i);
                total[i] = lhs >= rhs ? 1d : 0d;
            }

            return new ClassSeries(total, new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));
        }

        public static ClassSeries Mod(ClassSeries dividend, ClassSeries divisor)
        {
            var total = Combine(dividend.Total, divisor.Total, (a, b) =>
            {
                return Math.Abs(b) <= Tolerance ? 0d : Modulo(a, b);
            });

            if (dividend.ByClass.Count == 0 || divisor.ByClass.Count > 0)
            {
                return new ClassSeries(total, new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));
            }

            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, series) in dividend.ByClass)
            {
                var values = new double[series.Length];
                for (var i = 0; i < series.Length; i++)
                {
                    var divisorSample = i < divisor.Total.Length ? divisor.Total[i] : 0d;
                    values[i] = Math.Abs(divisorSample) <= Tolerance ? 0d : Modulo(series[i], divisorSample);
                }

                dict[classId] = values;
            }

            ClassSeries.NormalizeToTotal(dict, total);
            return new ClassSeries(total, dict);
        }

        public static ClassSeries Pulse(int period, int phase, ClassSeries amplitude)
        {
            if (period <= 0)
            {
                return ClassSeries.Zero(amplitude.Total.Length);
            }

            var length = amplitude.Total.Length;
            var total = new double[length];
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var classId in amplitude.ByClass.Keys)
            {
                dict[classId] = new double[length];
            }

            for (var i = 0; i < length; i++)
            {
                var delta = i - phase;
                var fire = delta >= 0 && delta % period == 0;
                if (!fire)
                {
                    continue;
                }

                total[i] = Safe(amplitude.Total, i);
                foreach (var (classId, series) in amplitude.ByClass)
                {
                    var arr = dict[classId];
                    arr[i] = Safe(series, i);
                }
            }

            return new ClassSeries(total, dict);
        }

        private static ClassSeries ApplyUnary(ClassSeries source, Func<double, double> op)
        {
            var total = new double[source.Total.Length];
            for (var i = 0; i < total.Length; i++)
            {
                total[i] = op(source.Total[i]);
            }

            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, series) in source.ByClass)
            {
                var values = new double[series.Length];
                for (var i = 0; i < series.Length; i++)
                {
                    values[i] = op(series[i]);
                }

                dict[classId] = values;
            }

            return new ClassSeries(total, dict);
        }

        private static double Modulo(double dividend, double divisor)
        {
            var remainder = dividend % divisor;
            if (remainder == 0d || Math.Sign(remainder) == Math.Sign(divisor))
            {
                return remainder;
            }

            return remainder + divisor;
        }

        internal static void NormalizeToTotal(
            IDictionary<string, double[]> contributions,
            double[] totals)
        {
            var length = totals.Length;
            for (var i = 0; i < length; i++)
            {
                var sum = 0d;
                foreach (var series in contributions.Values)
                {
                    if (i < series.Length)
                    {
                        var value = series[i];
                        if (double.IsFinite(value))
                        {
                            sum += value;
                        }
                    }
                }

                if (sum <= 0d || double.IsNaN(sum))
                {
                    continue;
                }

                var target = totals[i];
                if (Math.Abs(sum - target) <= Tolerance)
                {
                    continue;
                }

                var scale = target / sum;
                foreach (var series in contributions.Values)
                {
                    if (i < series.Length)
                    {
                        series[i] = series[i] * scale;
                    }
                }
            }
        }

        private static Dictionary<string, double[]> Merge(
            IReadOnlyDictionary<string, double[]> left,
            IReadOnlyDictionary<string, double[]> right,
            Func<double, double, double> op,
            int length)
        {
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, series) in left)
            {
                var values = GetOrCreate(dict, classId, length);
                for (var i = 0; i < length; i++)
                {
                    values[i] += op(series[i], right.TryGetValue(classId, out var other) && other.Length > i ? other[i] : 0d);
                }
            }

            foreach (var (classId, series) in right)
            {
                if (dict.ContainsKey(classId))
                {
                    continue;
                }

                var target = GetOrCreate(dict, classId, length);
                for (var i = 0; i < length; i++)
                {
                    target[i] += op(0d, series[i]);
                }
            }

            return dict;
        }

        private static Dictionary<string, double[]> MultiplyDictionary(
            IReadOnlyDictionary<string, double[]> source,
            double[] scalar,
            double[]? original = null)
        {
            var dict = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, series) in source)
            {
                var values = new double[scalar.Length];
                for (var i = 0; i < scalar.Length; i++)
                {
                    var multiplier = scalar[i];
                    if (!double.IsFinite(multiplier))
                    {
                        values[i] = 0d;
                        continue;
                    }

                    var contribution = i < series.Length ? series[i] : 0d;
                    if (original is null)
                    {
                        values[i] = contribution * multiplier;
                        continue;
                    }

                    var originalValue = original[i];
                    if (!double.IsFinite(originalValue) || Math.Abs(originalValue) < Tolerance)
                    {
                        values[i] = 0d;
                        continue;
                    }

                    values[i] = contribution * (multiplier / originalValue);
                }

                dict[classId] = values;
            }

            return dict;
        }

        private static double[] Combine(double[] left, double[] right, Func<double, double, double> op)
        {
            var length = left.Length;
            var result = new double[length];
            for (var i = 0; i < length; i++)
            {
                result[i] = op(left[i], right[i]);
            }

            return result;
        }

        private static void ApplyDispatchSchedule(
            ClassSeries outflow,
            ClassSeries? capacity,
            DispatchScheduleParameters schedule)
        {
            var normalizedPhase = schedule.PhaseOffset;
            var total = outflow.Total;
            var capacityTotals = capacity?.Total;

            for (var i = 0; i < total.Length; i++)
            {
                if (!DispatchScheduleProcessor.IsDispatchBin(i, schedule.PeriodBins, normalizedPhase))
                {
                    ZeroBin(outflow, i);
                    continue;
                }

                var requested = Safe(total, i);
                var allowed = capacityTotals is not null && i < capacityTotals.Length
                    ? capacityTotals[i]
                    : requested;

                if (!double.IsFinite(allowed))
                {
                    allowed = requested;
                }

                allowed = Math.Min(requested, allowed);
                if (Math.Abs(allowed - requested) <= Tolerance)
                {
                    total[i] = requested;
                    continue;
                }

                ScaleBin(outflow, i, allowed);
            }
        }

        private static void ZeroBin(ClassSeries source, int index)
        {
            if (index < source.Total.Length)
            {
                source.Total[index] = 0d;
            }

            foreach (var series in source.ByClass.Values)
            {
                if (index < series.Length)
                {
                    series[index] = 0d;
                }
            }
        }

        private static void ScaleBin(ClassSeries source, int index, double targetValue)
        {
            var current = index < source.Total.Length ? source.Total[index] : 0d;
            if (current <= Tolerance)
            {
                ZeroBin(source, index);
                source.Total[index] = targetValue;
                return;
            }

            var scale = targetValue / current;
            source.Total[index] = targetValue;
            foreach (var series in source.ByClass.Values)
            {
                if (index < series.Length)
                {
                    series[index] *= scale;
                }
            }
        }

        private static double[] ConvolveSeries(double[] source, double[] kernel)
        {
            var result = new double[source.Length];
            for (var t = 0; t < source.Length; t++)
            {
                double sum = 0d;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var index = t - k;
                    if (index < 0)
                    {
                        break;
                    }

                    var sample = source[index];
                    if (!double.IsFinite(sample))
                    {
                        continue;
                    }

                    sum += sample * kernel[k];
                }

                result[t] = sum;
            }

            return result;
        }

        private static double[] GetOrCreate(Dictionary<string, double[]> target, string classId, int length)
        {
            if (!target.TryGetValue(classId, out var series))
            {
                series = new double[length];
                target[classId] = series;
            }

            return series;
        }
    }
}
