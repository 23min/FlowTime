using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;
using ExpressionBinaryOpNode = FlowTime.Expressions.BinaryOpNode;

namespace FlowTime.Core.Artifacts;

internal static class ClassContributionBuilder
{
    private const double Tolerance = 1e-9;

    public static IReadOnlyDictionary<NodeId, IReadOnlyDictionary<string, double[]>> Build(
        ModelDefinition model,
        TimeGrid grid,
        IReadOnlyDictionary<NodeId, double[]> totals,
        IReadOnlyDictionary<NodeId, string> classAssignments)
    {
        if (classAssignments.Count == 0)
        {
            return new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        }

        var topologySeeds = ExtractBacklogSeeds(model);
        var nodeDefinitions = model.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
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
                "backlog" => EvaluateBacklogNode(nodeDefinition, grid, totals, contributions, topologySeeds),
                _ => ClassSeries.FromTotals(totalSeries)
            };

            contributions[nodeId] = series;
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

        return result;
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

    private static ClassSeries EvaluateBacklogNode(
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
        return ClassSeries.Backlog(totalSeries, inflow, outflow, loss, initial);
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
            NormalizeToTotal(dict, total);
            return new ClassSeries(total, dict);
        }

        public static ClassSeries Subtract(ClassSeries left, ClassSeries right)
        {
            var total = Combine(left.Total, right.Total, (a, b) => a - b);
            var dict = Merge(left.ByClass, right.ByClass, (a, b) => a - b, total.Length);
            NormalizeToTotal(dict, total);
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
            double initial)
        {
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

            NormalizeToTotal(dict, totalSeries);
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

            NormalizeToTotal(dict, result);
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

        private static void NormalizeToTotal(
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
