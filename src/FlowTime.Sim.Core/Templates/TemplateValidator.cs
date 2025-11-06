using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using FlowTime.Expressions;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Performs schema, topology, and expression validation for simulation templates.
/// </summary>
internal static class TemplateValidator
{
    private const double ProbabilityTolerance = 1e-10;
    private const double IntegerTolerance = 1e-9;
    private static readonly Regex SemanticVersionPattern = new("^\\d+\\.\\d+\\.\\d+(-[0-9A-Za-z\\.-]+)?$", RegexOptions.Compiled);

    public static void Validate(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        ValidateGenerator(template.Generator);
        ValidateMetadata(template.Metadata);
        ValidateWindow(template.Window);
        ValidateGrid(template.Grid);

        var nodeIds = ValidateNodes(template, out var nodesRequiringInitial);
        ValidateOutputs(template.Outputs, nodeIds);
        ValidateTopology(template, nodeIds, nodesRequiringInitial, template.Mode);
        ValidateRng(template.Rng);
    }

    private static void ValidateGenerator(string generator)
    {
        if (string.IsNullOrWhiteSpace(generator))
        {
            throw new TemplateValidationException("Template generator must be provided (expected 'flowtime-sim').");
        }

        if (!generator.StartsWith("flowtime-sim", StringComparison.OrdinalIgnoreCase))
        {
            throw new TemplateValidationException($"Template generator '{generator}' must start with 'flowtime-sim'.");
        }
    }

    private static void ValidateMetadata(TemplateMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            throw new TemplateValidationException("Template metadata.id is required.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            throw new TemplateValidationException("Template metadata.title is required.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            throw new TemplateValidationException("Template metadata.version is required.");
        }

        if (!SemanticVersionPattern.IsMatch(metadata.Version))
        {
            throw new TemplateValidationException($"Template metadata.version '{metadata.Version}' must follow semantic versioning (e.g., 1.0.0 or 1.0.0-beta.1).");
        }
    }

    private static void ValidateWindow(TemplateWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (string.IsNullOrWhiteSpace(window.Start))
        {
            throw new TemplateValidationException("Template window.start is required (UTC ISO 8601).");
        }

        if (!DateTimeOffset.TryParse(window.Start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start))
        {
            throw new TemplateValidationException($"Template window.start '{window.Start}' must be a valid ISO 8601 timestamp.");
        }

        if (start.Offset != TimeSpan.Zero)
        {
            throw new TemplateValidationException($"Template window.start '{window.Start}' must be expressed in UTC (offset 0).");
        }

        if (string.IsNullOrWhiteSpace(window.Timezone))
        {
            throw new TemplateValidationException("Template window.timezone is required.");
        }

        if (!string.Equals(window.Timezone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            throw new TemplateValidationException($"Template window.timezone must be 'UTC' (received '{window.Timezone}').");
        }
    }

    private static void ValidateGrid(TemplateGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.Bins <= 0)
        {
            throw new TemplateValidationException("Grid bins must be greater than zero.");
        }

        if (grid.BinSize <= 0)
        {
            throw new TemplateValidationException("Grid binSize must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(grid.BinUnit))
        {
            throw new TemplateValidationException("Grid binUnit is required (e.g., 'minutes').");
        }
    }

    private static HashSet<string> ValidateNodes(Template template, out HashSet<string> nodesRequiringInitial)
    {
        if (template.Nodes == null || template.Nodes.Count == 0)
        {
            throw new TemplateValidationException("Template must define at least one node.");
        }

        var allNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in template.Nodes)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                throw new TemplateValidationException("All nodes must define an id.");
            }

            if (!allNodeIds.Add(node.Id))
            {
                throw new TemplateValidationException($"Duplicate node id '{node.Id}' detected.");
            }
        }

        nodesRequiringInitial = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in template.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Kind))
            {
                throw new TemplateValidationException($"Node '{node.Id}' must define a kind (const, pmf, expr).");
            }

            switch (node.Kind)
            {
                case "const":
                    ValidateConstNode(node);
                    break;

                case "pmf":
                    ValidatePmfNode(node);
                    break;

                case "expr":
                    if (ValidateExpressionNode(node, allNodeIds))
                    {
                        nodesRequiringInitial.Add(node.Id);
                    }
                    break;

                default:
                    throw new TemplateValidationException($"Unsupported node kind '{node.Kind}' for node '{node.Id}'.");
            }
        }

        return allNodeIds;
    }

    private static void ValidateConstNode(TemplateNode node)
    {
        if ((node.Values == null || node.Values.Length == 0) && string.IsNullOrWhiteSpace(node.Source))
        {
            throw new TemplateValidationException($"Const node '{node.Id}' must specify values or a telemetry source.");
        }
    }

    private static void ValidatePmfNode(TemplateNode node)
    {
        if (node.Pmf == null)
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' must define a pmf section.");
        }

        if (node.Pmf.Values == null || node.Pmf.Values.Length == 0)
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' must define at least one value.");
        }

        if (node.Pmf.Probabilities == null || node.Pmf.Probabilities.Length == 0)
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' must define probabilities.");
        }

        if (node.Pmf.Values.Length != node.Pmf.Probabilities.Length)
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' values and probabilities must have the same length.");
        }

        var sum = node.Pmf.Probabilities.Sum();
        if (Math.Abs(sum - 1.0) > ProbabilityTolerance)
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' probabilities must sum to 1.0 (received {sum}).");
        }

        if (node.Pmf.Probabilities.Any(p => p < 0))
        {
            throw new TemplateValidationException($"PMF node '{node.Id}' probabilities must be non-negative.");
        }
    }

    private static bool ValidateExpressionNode(TemplateNode node, HashSet<string> allNodeIds)
    {
        if (string.IsNullOrWhiteSpace(node.Expr))
        {
            throw new TemplateValidationException($"Expression node '{node.Id}' must define expr.");
        }

        ExpressionNode ast;
        try
        {
            var parser = new ExpressionParser(node.Expr);
            ast = parser.Parse();
        }
        catch (ExpressionParseException ex)
        {
            throw new TemplateValidationException($"Expression node '{node.Id}' failed to parse: {ex.Message}");
        }

        var referencedNodes = ExpressionReferenceCollector.Collect(ast);
        foreach (var referenced in referencedNodes)
        {
            if (!allNodeIds.Contains(referenced))
            {
                throw new TemplateValidationException($"Expression node '{node.Id}' references unknown node '{referenced}'.");
            }
        }

        if (node.Dependencies != null && node.Dependencies.Count > 0)
        {
            var declared = new HashSet<string>(node.Dependencies, StringComparer.Ordinal);
            if (!declared.SetEquals(referencedNodes))
            {
                var missing = referencedNodes.Except(declared).ToArray();
                var extra = declared.Except(referencedNodes).ToArray();

                if (missing.Length > 0)
                {
                    throw new TemplateValidationException($"Expression node '{node.Id}' is missing dependencies for: {string.Join(", ", missing)}.");
                }

                if (extra.Length > 0)
                {
                    throw new TemplateValidationException($"Expression node '{node.Id}' declares unused dependencies: {string.Join(", ", extra)}.");
                }
            }
        }

        var validation = ExpressionSemanticValidator.Validate(ast, node.Id);
        if (!validation.IsValid)
        {
            var errors = string.Join(" ", validation.Errors.Select(e => e.Message));
            if (validation.Errors.Any(e => e.Code == ExpressionValidationErrorCodes.SelfShiftRequiresInitialCondition))
            {
                // Track for later topology validation but do not throw immediately.
                return true;
            }

            throw new TemplateValidationException($"Expression node '{node.Id}' failed semantic validation: {errors}");
        }

        return false;
    }

    private static void ValidateOutputs(List<TemplateOutput> outputs, HashSet<string> nodeIds)
    {
        if (outputs == null || outputs.Count == 0)
        {
            throw new TemplateValidationException("Template must define at least one output.");
        }

        foreach (var output in outputs)
        {
            ArgumentNullException.ThrowIfNull(output);

            if (string.IsNullOrWhiteSpace(output.Series))
            {
                throw new TemplateValidationException("Output series must be specified (use '*' for wildcard).");
            }

            if (!string.Equals(output.Series, "*", StringComparison.Ordinal) &&
                !nodeIds.Contains(output.Series))
            {
                throw new TemplateValidationException($"Output references unknown series '{output.Series}'.");
            }
        }
    }

    private static void ValidateTopology(
        Template template,
        HashSet<string> nodeIds,
        HashSet<string> nodesRequiringInitial,
        TemplateMode mode)
    {
        if (template.Topology == null)
        {
            throw new TemplateValidationException("Template topology is required.");
        }

        if (template.Topology.Nodes == null || template.Topology.Nodes.Count == 0)
        {
            throw new TemplateValidationException("Topology must define at least one node.");
        }

        var topologyNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var topoNode in template.Topology.Nodes)
        {
            ArgumentNullException.ThrowIfNull(topoNode);

            if (string.IsNullOrWhiteSpace(topoNode.Id))
            {
                throw new TemplateValidationException("Topology nodes must define an id.");
            }

            if (!topologyNodeIds.Add(topoNode.Id))
            {
                throw new TemplateValidationException($"Duplicate topology node id '{topoNode.Id}' detected.");
            }

            if (string.IsNullOrWhiteSpace(topoNode.Kind))
            {
                throw new TemplateValidationException($"Topology node '{topoNode.Id}' must define kind.");
            }

            ValidateTopologySemantics(topoNode, nodeIds, mode);
        }

        if (template.Topology.Nodes.Count > 1)
        {
            if (template.Topology.Edges == null || template.Topology.Edges.Count == 0)
            {
                throw new TemplateValidationException("Topology edges are required when more than one topology node exists.");
            }
        }

        if (template.Topology.Edges != null)
        {
            foreach (var edge in template.Topology.Edges)
            {
                ArgumentNullException.ThrowIfNull(edge);

                if (string.IsNullOrWhiteSpace(edge.From) || string.IsNullOrWhiteSpace(edge.To))
                {
                    throw new TemplateValidationException("Topology edges must define 'from' and 'to' endpoints.");
                }

                var fromNode = ExtractTopologyNodeId(edge.From);
                var toNode = ExtractTopologyNodeId(edge.To);

                if (!topologyNodeIds.Contains(fromNode))
                {
                    throw new TemplateValidationException($"Topology edge references unknown source node '{edge.From}'.");
                }

                if (!topologyNodeIds.Contains(toNode))
                {
                    throw new TemplateValidationException($"Topology edge references unknown target node '{edge.To}'.");
                }
            }
        }

        EnsureInitialConditions(template.Topology, nodesRequiringInitial);
    }

    private static void ValidateTopologySemantics(TemplateTopologyNode topologyNode, HashSet<string> nodeIds, TemplateMode mode)
    {
        if (topologyNode.Semantics == null)
        {
            throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics mapping.");
        }

        var semantics = topologyNode.Semantics;
        var mappedSeries = new[]
        {
            ("arrivals", semantics.Arrivals),
            ("served", semantics.Served),
            ("errors", semantics.Errors),
            ("attempts", semantics.Attempts),
            ("failures", semantics.Failures),
            ("retryEcho", semantics.RetryEcho),
            ("queue", semantics.Queue),
            ("capacity", semantics.Capacity),
            ("external_demand", semantics.ExternalDemand)
        };

        foreach (var (name, value) in mappedSeries)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!nodeIds.Contains(value))
            {
                throw new TemplateValidationException($"Topology node '{topologyNode.Id}' semantics.{name} references unknown series '{value}'.");
            }
        }

        if (semantics.RetryKernel is { Length: > 0 })
        {
            for (var i = 0; i < semantics.RetryKernel.Length; i++)
            {
                var value = semantics.RetryKernel[i];
                if (!double.IsFinite(value))
                {
                    throw new TemplateValidationException($"Topology node '{topologyNode.Id}' semantics.retryKernel contains non-finite value at index {i}.");
                }
            }
        }

        if (mode == TemplateMode.Simulation)
        {
            if (string.IsNullOrWhiteSpace(semantics.Arrivals))
            {
                throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.arrivals in simulation mode.");
            }

            if (string.IsNullOrWhiteSpace(semantics.Served) &&
                string.IsNullOrWhiteSpace(semantics.Queue))
            {
                throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.served or semantics.queue in simulation mode.");
            }
        }
    }

    private static string ExtractTopologyNodeId(string endpoint)
    {
        var separatorIndex = endpoint.IndexOf(':');
        return separatorIndex > 0 ? endpoint[..separatorIndex] : endpoint;
    }

    private static void EnsureInitialConditions(TemplateTopology topology, HashSet<string> nodesRequiringInitial)
    {
        if (nodesRequiringInitial.Count == 0)
        {
            return;
        }

        var nodesWithInitial = new HashSet<string>(StringComparer.Ordinal);

        foreach (var topologyNode in topology.Nodes)
        {
            if (string.IsNullOrWhiteSpace(topologyNode.Semantics?.Queue))
            {
                continue;
            }

            var queueSeries = topologyNode.Semantics.Queue;
            if (queueSeries != null && nodesRequiringInitial.Contains(queueSeries))
            {
                if (topologyNode.InitialCondition?.QueueDepth is null)
                {
                    throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define initialCondition.queueDepth for queue series '{queueSeries}' due to self-referential SHIFT.");
                }

                nodesWithInitial.Add(queueSeries);
            }
        }

        foreach (var nodeId in nodesRequiringInitial)
        {
            if (!nodesWithInitial.Contains(nodeId))
            {
                throw new TemplateValidationException($"Expression node '{nodeId}' requires an initial condition (topology.nodes[].initialCondition.queueDepth).");
            }
        }
    }

    internal static void ValidateArrayParameters(Template template, Dictionary<string, object?> parameterValues)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameterValues);

        foreach (var parameter in template.Parameters)
        {
            if (!IsArrayParameter(parameter))
            {
                continue;
            }

            if (!parameterValues.TryGetValue(parameter.Name, out var value) || value is null)
            {
                throw new TemplateValidationException($"Parameter '{parameter.Name}' requires an array value.");
            }

            ValidateArrayParameterValue(parameter, value);
        }
    }

    private static void ValidateArrayParameterValue(TemplateParameter parameter, object value)
    {
        if (value is string)
        {
            throw new TemplateValidationException($"Parameter '{parameter.Name}' expects an array value.");
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new TemplateValidationException($"Parameter '{parameter.Name}' expects an array value.");
            }

            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateArrayElement(parameter, item, index);
                index++;
            }
            return;
        }

        if (value is IEnumerable enumerable)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                ValidateArrayElement(parameter, item, index);
                index++;
            }
            return;
        }

        throw new TemplateValidationException($"Parameter '{parameter.Name}' expects an array value.");
    }

    private static void ValidateArrayElement(TemplateParameter parameter, object? element, int index)
    {
        var kind = ResolveArrayElementKind(parameter);
        double numericValue;

        switch (kind)
        {
            case ArrayElementKind.Int:
                var intValue = ConvertToInt(parameter, element, index);
                numericValue = intValue;
                break;
            default:
                numericValue = ConvertToDouble(parameter, element, index);
                break;
        }

        if (parameter.Min.HasValue && numericValue < parameter.Min.Value)
        {
            throw new TemplateValidationException(
                $"Parameter '{parameter.Name}' element at index {index} ({numericValue.ToString(CultureInfo.InvariantCulture)}) is below minimum {parameter.Min.Value.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (parameter.Max.HasValue && numericValue > parameter.Max.Value)
        {
            throw new TemplateValidationException(
                $"Parameter '{parameter.Name}' element at index {index} ({numericValue.ToString(CultureInfo.InvariantCulture)}) exceeds maximum {parameter.Max.Value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static double ConvertToDouble(TemplateParameter parameter, object? element, int index)
    {
        return element switch
        {
            null => throw new TemplateValidationException($"Parameter '{parameter.Name}' element at index {index} is null."),
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetDouble(),
            JsonElement json when json.ValueKind == JsonValueKind.String && double.TryParse(json.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new TemplateValidationException($"Parameter '{parameter.Name}' element at index {index} must be a number.")
        };
    }

    private static int ConvertToInt(TemplateParameter parameter, object? element, int index)
    {
        return element switch
        {
            null => throw new TemplateValidationException($"Parameter '{parameter.Name}' element at index {index} is null."),
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            double d when Math.Abs(d - Math.Round(d)) <= IntegerTolerance => (int)Math.Round(d),
            float f when Math.Abs(f - Math.Round(f)) <= IntegerTolerance => (int)Math.Round(f),
            decimal m when Math.Abs((double)m - Math.Round((double)m)) <= IntegerTolerance => (int)Math.Round((double)m),
            JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var integer) => checked((int)integer),
            JsonElement json when json.ValueKind == JsonValueKind.Number => ConvertDoubleToInt(parameter, json.GetDouble(), index),
            JsonElement json when json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt,
            JsonElement json when json.ValueKind == JsonValueKind.String && double.TryParse(json.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedDouble) => ConvertDoubleToInt(parameter, parsedDouble, index),
            _ => throw new TemplateValidationException($"Parameter '{parameter.Name}' element at index {index} must be an integer.")
        };
    }

    private static int ConvertDoubleToInt(TemplateParameter parameter, double value, int index)
    {
        var rounded = Math.Round(value);
        if (Math.Abs(value - rounded) > IntegerTolerance)
        {
            throw new TemplateValidationException($"Parameter '{parameter.Name}' element at index {index} must be an integer.");
        }

        return checked((int)rounded);
    }

    private static bool IsArrayParameter(TemplateParameter parameter) =>
        parameter.Type.Equals("array", StringComparison.OrdinalIgnoreCase);

    private static ArrayElementKind ResolveArrayElementKind(TemplateParameter parameter) =>
        parameter.ArrayOf != null && parameter.ArrayOf.Equals("int", StringComparison.OrdinalIgnoreCase)
            ? ArrayElementKind.Int
            : ArrayElementKind.Double;

    private enum ArrayElementKind
    {
        Double,
        Int
    }

    private static void ValidateRng(TemplateRng? rng)
    {
        if (rng == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rng.Kind))
        {
            throw new TemplateValidationException("RNG kind is required when rng block is specified.");
        }

        if (!string.Equals(rng.Kind, "pcg32", StringComparison.OrdinalIgnoreCase))
        {
            throw new TemplateValidationException($"Unsupported RNG kind '{rng.Kind}'. Expected 'pcg32'.");
        }

        if (string.IsNullOrWhiteSpace(rng.Seed))
        {
            throw new TemplateValidationException("RNG seed is required when rng block is specified.");
        }
    }

    private sealed class ExpressionReferenceCollector : IExpressionVisitor<object?>
    {
        private readonly HashSet<string> references = new(StringComparer.Ordinal);

        public static HashSet<string> Collect(ExpressionNode ast)
        {
            var collector = new ExpressionReferenceCollector();
            ast.Accept(collector);
            return collector.references;
        }

        public object? VisitBinaryOp(BinaryOpNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            foreach (var argument in node.Arguments)
            {
                argument.Accept(this);
            }

            return null;
        }

        public object? VisitNodeReference(NodeReferenceNode node)
        {
            if (!string.IsNullOrWhiteSpace(node.NodeId))
            {
                references.Add(node.NodeId);
            }

            return null;
        }

        public object? VisitLiteral(LiteralNode node) => null;

        public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
    }
}
