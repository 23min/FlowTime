using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using FlowTime.Core.Compiler;
using System.Linq;
using FlowTime.Core.Dispatching;
using FlowTime.Core.Execution;
using FlowTime.Core.Nodes;
using FlowTime.Core.Expressions;
using FlowTime.Expressions;

namespace FlowTime.Core.Models;

/// <summary>
/// Service for parsing FlowTime model definitions into executable node graphs.
/// Shared between CLI and API to avoid code duplication.
/// </summary>
public static class ModelParser
{
    /// <summary>
    /// Parse a model definition into a Graph ready for evaluation.
    /// </summary>
    /// <param name="model">The model definition with grid and nodes</param>
    /// <returns>Parsed TimeGrid and Graph ready for evaluation</returns>
    /// <exception cref="ModelParseException">Thrown when parsing fails</exception>
    public static (TimeGrid Grid, Graph Graph) ParseModel(ModelDefinition model)
    {
        ValidateInitialConditions(model);

        if (model.Grid == null)
            throw new ModelParseException("Model must have a grid definition");

        // Require binSize + binUnit (no legacy support)
        if (model.Grid.BinSize <= 0 || string.IsNullOrEmpty(model.Grid.BinUnit))
            throw new ModelParseException("Grid must specify binSize and binUnit");

        var unit = TimeUnitExtensions.Parse(model.Grid.BinUnit);
        var grid = new TimeGrid(model.Grid.Bins, model.Grid.BinSize, unit);

        var nodes = ParseNodes(model);
        var graph = new Graph(nodes);

        return (grid, graph);
    }

    public static ModelMetadata ParseMetadata(ModelDefinition model, string? modelDirectory = null)
    {
        ValidateInitialConditions(model);

        if (model.Grid == null)
            throw new ModelParseException("Model must have a grid definition");
        if (model.Grid.BinSize <= 0 || string.IsNullOrEmpty(model.Grid.BinUnit))
            throw new ModelParseException("Grid must specify binSize and binUnit");

        var unit = TimeUnitExtensions.Parse(model.Grid.BinUnit);
        var window = new Window
        {
            Bins = model.Grid.Bins,
            BinSize = model.Grid.BinSize,
            BinUnit = unit,
            StartTime = ParseStartTime(model.Grid.Start)
        };
        var nodeDefinitionsById = (model.Nodes ?? new List<NodeDefinition>())
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
            .ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

        Topology? topology = null;
        if (model.Topology != null)
        {
            var nodeList = model.Topology.Nodes.Select(ConvertNode).ToList();
            var edgeList = model.Topology.Edges.Select(ConvertEdge).ToList();
            var constraintList = model.Topology.Constraints.Select(ConvertConstraint).ToList();
            topology = new Topology
            {
                Nodes = nodeList,
                Edges = edgeList,
                Constraints = constraintList
            };
        }

        return new ModelMetadata
        {
            Window = window,
            Topology = topology
        };

        Node ConvertNode(TopologyNodeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new ModelParseException("Topology nodes must specify an id");

            if (definition.Semantics == null)
                throw new ModelParseException($"Topology node '{definition.Id}' must include semantics");

            var semantics = new NodeSemantics
            {
                Arrivals = RequireSeriesReference(definition.Semantics.Arrivals, definition.Id, "arrivals", "node"),
                Served = RequireSeriesReference(definition.Semantics.Served, definition.Id, "served", "node"),
                Errors = OptionalSeriesReference(definition.Semantics.Errors, definition.Id, "errors", "node"),
                Attempts = OptionalSeriesReference(definition.Semantics.Attempts, definition.Id, "attempts", "node"),
                Failures = OptionalSeriesReference(definition.Semantics.Failures, definition.Id, "failures", "node"),
                ExhaustedFailures = OptionalSeriesReference(definition.Semantics.ExhaustedFailures, definition.Id, "exhaustedFailures", "node"),
                RetryEcho = OptionalSeriesReference(definition.Semantics.RetryEcho, definition.Id, "retryEcho", "node"),
                RetryBudgetRemaining = OptionalSeriesReference(definition.Semantics.RetryBudgetRemaining, definition.Id, "retryBudgetRemaining", "node"),
                RetryKernel = definition.Semantics.RetryKernel,
                ExternalDemand = OptionalSeriesReference(definition.Semantics.ExternalDemand, definition.Id, "externalDemand", "node"),
                QueueDepth = OptionalSeriesReference(definition.Semantics.QueueDepth, definition.Id, "queueDepth", "node"),
                Capacity = OptionalSeriesReference(definition.Semantics.Capacity, definition.Id, "capacity", "node"),
                Parallelism = definition.Semantics.Parallelism,
                ProcessingTimeMsSum = OptionalSeriesReference(definition.Semantics.ProcessingTimeMsSum, definition.Id, "processingTimeMsSum", "node"),
                ServedCount = OptionalSeriesReference(definition.Semantics.ServedCount, definition.Id, "servedCount", "node"),
                SlaMinutes = definition.Semantics.SlaMin,
                MaxAttempts = definition.Semantics.MaxAttempts,
                BackoffStrategy = definition.Semantics.BackoffStrategy,
                ExhaustedPolicy = definition.Semantics.ExhaustedPolicy,
                Metadata = definition.Semantics.Metadata,
                Aliases = NormalizeAliases(definition.Semantics.Aliases)
            };

            return new Node
            {
                Id = definition.Id,
                Kind = string.IsNullOrWhiteSpace(definition.Kind) ? "service" : definition.Kind,
                Analytical = RuntimeAnalyticalDescriptorCompiler.Compile(
                    definition.Id,
                    definition.Kind,
                    definition.NodeRole,
                    semantics,
                    nodeDefinitionsById),
                NodeRole = string.IsNullOrWhiteSpace(definition.NodeRole) ? null : definition.NodeRole,
                Group = definition.Group,
                Ui = definition.Ui != null ? new UiHints { X = definition.Ui.X, Y = definition.Ui.Y } : null,
                Constraints = definition.Constraints,
                DispatchSchedule = definition.DispatchSchedule,
                Semantics = semantics,
                InitialCondition = definition.InitialCondition != null
                    ? new InitialCondition { QueueDepth = definition.InitialCondition.QueueDepth }
                    : null
            };
        }

        Edge ConvertEdge(TopologyEdgeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Source) || string.IsNullOrWhiteSpace(definition.Target))
                throw new ModelParseException("Topology edges must specify source and target");

            return new Edge
            {
                Source = definition.Source,
                Target = definition.Target,
                Weight = definition.Weight,
                Id = definition.Id,
                EdgeType = definition.Type,
                Field = definition.Measure,
                Multiplier = definition.Multiplier,
                Lag = definition.Lag
            };
        }

        Constraint ConvertConstraint(ConstraintDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ModelParseException("Topology constraints must specify an id");
            }

            if (definition.Semantics == null)
            {
                throw new ModelParseException($"Topology constraint '{definition.Id}' must include semantics");
            }

            return new Constraint
            {
                Id = definition.Id,
                Semantics = new ConstraintSemantics
                {
                    Arrivals = RequireSeriesReference(definition.Semantics.Arrivals, definition.Id, "arrivals", "constraint"),
                    Served = RequireSeriesReference(definition.Semantics.Served, definition.Id, "served", "constraint"),
                    Errors = OptionalSeriesReference(definition.Semantics.Errors, definition.Id, "errors", "constraint"),
                    LatencyMinutes = OptionalSeriesReference(definition.Semantics.LatencyMinutes, definition.Id, "latencyMinutes", "constraint")
                }
            };
        }

        static string RequireSemantic(string value, string nodeId, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ModelParseException($"Topology node '{nodeId}' must specify semantics.{name}");
            return value;
        }

        static CompiledSeriesReference RequireSeriesReference(string value, string ownerId, string name, string ownerKind)
        {
            try
            {
                return SemanticReferenceResolver.ParseSeriesReference(RequireSemantic(value, ownerId, name));
            }
            catch (ArgumentException ex)
            {
                throw new ModelParseException($"Topology {ownerKind} '{ownerId}' has invalid semantics.{name}: {ex.Message}", ex);
            }
        }

        static CompiledSeriesReference? OptionalSeriesReference(string? value, string ownerId, string name, string ownerKind)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return SemanticReferenceResolver.ParseSeriesReference(value);
            }
            catch (ArgumentException ex)
            {
                throw new ModelParseException($"Topology {ownerKind} '{ownerId}' has invalid semantics.{name}: {ex.Message}", ex);
            }
        }

        static IReadOnlyDictionary<string, string>? NormalizeAliases(Dictionary<string, string>? aliases)
        {
            if (aliases is null || aliases.Count == 0)
            {
                return null;
            }

            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in aliases)
            {
                var key = kvp.Key?.Trim();
                var value = kvp.Value?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                normalized[key] = value;
            }

            return normalized.Count == 0 ? null : new ReadOnlyDictionary<string, string>(normalized);
        }
    }

    private static DateTime? ParseStartTime(string? startTime)
    {
        if (string.IsNullOrWhiteSpace(startTime))
            return null;

        if (!DateTime.TryParse(startTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            throw new ModelParseException($"Invalid startTime value: '{startTime}'");

        if (parsed.Kind != DateTimeKind.Utc)
            parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        return parsed;
    }

    private static void ValidateInitialConditions(ModelDefinition model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Nodes == null || model.Nodes.Count == 0)
            return;

        var topologyInitials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (model.Topology?.Nodes != null)
        {
            foreach (var topoNode in model.Topology.Nodes)
            {
                if (string.IsNullOrWhiteSpace(topoNode.Id))
                    continue;

                if (topoNode.InitialCondition != null)
                {
                    topologyInitials.Add(topoNode.Id);
                    var queueDepthId = topoNode.Semantics?.QueueDepth;
                    if (!string.IsNullOrWhiteSpace(queueDepthId))
                    {
                        topologyInitials.Add(queueDepthId.Trim());
                    }
                }
            }
        }

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Kind, "expr", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Expr))
                continue;

            ExpressionNode ast;
            try
            {
                var parser = new ExpressionParser(node.Expr);
                ast = parser.Parse();
            }
            catch
            {
                // Expression parse errors are handled later when nodes are compiled.
                continue;
            }

            var validation = ExpressionSemanticValidator.Validate(ast, node.Id);
            var selfShiftError = validation.Errors.FirstOrDefault(
                e => e.Code == ExpressionValidationErrorCodes.SelfShiftRequiresInitialCondition);

            if (selfShiftError != null)
            {
                if (!topologyInitials.Contains(node.Id))
                {
                    throw new ModelParseException(selfShiftError.Message);
                }
            }
        }
    }

    /// <summary>
    /// Parse node definitions into INode objects.
    /// </summary>
    public static List<INode> ParseNodes(ModelDefinition model)
    {
        var nodes = new List<INode>();

        // Build a lookup from serviceWithBuffer series id to initial seed (from topology initialCondition)
        var serviceWithBufferInitials = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (model.Topology?.Nodes != null)
        {
            foreach (var topoNode in model.Topology.Nodes)
            {
                var queueId = topoNode.Semantics?.QueueDepth;
                if (string.IsNullOrWhiteSpace(queueId)) continue;
                var seed = topoNode.InitialCondition?.QueueDepth ?? 0d;
                serviceWithBufferInitials[queueId!.Trim()] = seed;
            }
        }

        foreach (var nodeDef in model.Nodes)
        {
            var node = ParseSingleNode(nodeDef);

            // Patch ServiceWithBuffer node with initial seed if configured via topology
            if (node is Nodes.ServiceWithBufferNode && serviceWithBufferInitials.TryGetValue(nodeDef.Id, out var seed))
            {
                // Reconstruct with seed
                var inflow = new NodeId(nodeDef.Inflow ?? throw new ModelParseException($"ServiceWithBuffer node {nodeDef.Id} requires 'inflow'."));
                var outflow = new NodeId(nodeDef.Outflow ?? throw new ModelParseException($"ServiceWithBuffer node {nodeDef.Id} requires 'outflow'."));
                NodeId? loss = string.IsNullOrWhiteSpace(nodeDef.Loss) ? null : new NodeId(nodeDef.Loss!);
                var scheduleConfig = CreateDispatchSchedule(nodeDef);
                NodeId? wipLimitSeriesId = string.IsNullOrWhiteSpace(nodeDef.WipLimitSeries) ? null : new NodeId(nodeDef.WipLimitSeries!);
                node = new Nodes.ServiceWithBufferNode(nodeDef.Id, inflow, outflow, loss, seed, scheduleConfig, nodeDef.WipLimit, nodeDef.WipOverflow, wipLimitSeriesId);
            }
            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Parse a single node definition into an INode.
    /// </summary>
    public static INode ParseSingleNode(NodeDefinition nodeDef)
    {
        if (string.IsNullOrWhiteSpace(nodeDef.Id))
            throw new ModelParseException("Node must have an id");

        var kind = nodeDef.Kind?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ModelParseException($"Node {nodeDef.Id}: kind must be specified");
        }

        var normalizedKind = kind.ToLowerInvariant();

        return normalizedKind switch
        {
            "const" => ParseConstNode(nodeDef),
            "expr" => ParseExprNode(nodeDef),
            "pmf" => ParsePmfNode(nodeDef),
            "servicewithbuffer" => ParseServiceWithBufferNode(nodeDef),
            "router" => ParseRouterNode(nodeDef),
            _ => throw new ModelParseException($"Unknown node kind: {nodeDef.Kind}")
        };
    }

    private static INode ParseConstNode(NodeDefinition nodeDef)
    {
        if (nodeDef.Values == null || nodeDef.Values.Length == 0)
            throw new ModelParseException($"Node {nodeDef.Id}: const nodes require values array");

        return new ConstSeriesNode(nodeDef.Id, nodeDef.Values);
    }

    private static INode ParseExprNode(NodeDefinition nodeDef)
    {
        if (string.IsNullOrWhiteSpace(nodeDef.Expr))
            throw new ModelParseException($"Node {nodeDef.Id}: expr nodes require expr property");

        try
        {
            var parser = new ExpressionParser(nodeDef.Expr);
            var ast = parser.Parse();
            var exprNode = ExpressionCompiler.Compile(ast, nodeDef.Id);
            return exprNode;
        }
        catch (Exception ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: error parsing expression '{nodeDef.Expr}': {ex.Message}", ex);
        }
    }

    private static INode ParsePmfNode(NodeDefinition nodeDef)
    {
        if (nodeDef.Pmf == null || nodeDef.Pmf.Values.Length == 0)
            throw new ModelParseException($"Node {nodeDef.Id}: pmf nodes require pmf.values to contain at least one entry");

        if (nodeDef.Pmf.Probabilities.Length != nodeDef.Pmf.Values.Length)
            throw new ModelParseException($"Node {nodeDef.Id}: pmf probabilities length must match values length");

        try
        {
            var distribution = new Dictionary<double, double>();
            for (int i = 0; i < nodeDef.Pmf.Values.Length; i++)
            {
                var value = nodeDef.Pmf.Values[i];
                var probability = nodeDef.Pmf.Probabilities[i];

                if (distribution.ContainsKey(value))
                    throw new ModelParseException($"Node {nodeDef.Id}: duplicate PMF value '{value}'");

                distribution[value] = probability;
            }

            var pmf = new Pmf.Pmf(distribution);
            return new Pmf.PmfNode(new NodeId(nodeDef.Id), pmf);
        }
        catch (ArgumentException ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: error creating PMF: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: unexpected error parsing PMF: {ex.Message}", ex);
        }
    }

    private static INode ParseServiceWithBufferNode(NodeDefinition nodeDef)
    {
        var inflowId = nodeDef.Inflow;
        var outflowId = nodeDef.Outflow;
        if (string.IsNullOrWhiteSpace(inflowId) || string.IsNullOrWhiteSpace(outflowId))
            throw new ModelParseException($"Node {nodeDef.Id}: serviceWithBuffer nodes require 'inflow' and 'outflow' fields");

        NodeId? loss = string.IsNullOrWhiteSpace(nodeDef.Loss) ? null : new NodeId(nodeDef.Loss!);
        var scheduleConfig = CreateDispatchSchedule(nodeDef);
        NodeId? wipLimitSeriesId = string.IsNullOrWhiteSpace(nodeDef.WipLimitSeries) ? null : new NodeId(nodeDef.WipLimitSeries!);
        // Initial seed is injected later from topology (see ParseNodes(model))
        return new Nodes.ServiceWithBufferNode(nodeDef.Id, new NodeId(inflowId), new NodeId(outflowId), loss, 0d, scheduleConfig, nodeDef.WipLimit, nodeDef.WipOverflow, wipLimitSeriesId);
    }

    private static INode ParseRouterNode(NodeDefinition nodeDef)
    {
        var queueId = nodeDef.Router?.Inputs?.Queue;
        if (string.IsNullOrWhiteSpace(queueId))
        {
            throw new ModelParseException($"Node {nodeDef.Id}: router nodes require inputs.queue");
        }

        if (nodeDef.Router?.Routes is not { Count: > 0 })
        {
            throw new ModelParseException($"Node {nodeDef.Id}: router nodes require at least one route");
        }

        foreach (var route in nodeDef.Router.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Target))
            {
                throw new ModelParseException($"Node {nodeDef.Id}: router routes must specify target");
            }

            if ((route.Classes is null || route.Classes.Length == 0) && (!route.Weight.HasValue || route.Weight.Value <= 0))
            {
                throw new ModelParseException($"Node {nodeDef.Id}: router route '{route.Target}' must declare classes or positive weight");
            }
        }

        return new Nodes.RouterNode(nodeDef.Id, new NodeId(queueId));
    }

    private static DispatchScheduleConfig? CreateDispatchSchedule(NodeDefinition nodeDef)
    {
        if (nodeDef.DispatchSchedule is null)
        {
            return null;
        }

        var schedule = nodeDef.DispatchSchedule;
        if (!string.IsNullOrWhiteSpace(schedule.Kind) &&
            !string.Equals(schedule.Kind, "time-based", StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelParseException($"Node {nodeDef.Id}: dispatchSchedule.kind '{schedule.Kind}' is not supported.");
        }

        if (schedule.PeriodBins <= 0)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: dispatchSchedule.periodBins must be positive.");
        }

        NodeId? capacityId = null;
        if (!string.IsNullOrWhiteSpace(schedule.CapacitySeries))
        {
            capacityId = new NodeId(schedule.CapacitySeries);
        }

        var phase = schedule.PhaseOffset ?? 0;
        return new DispatchScheduleConfig(schedule.PeriodBins, phase, capacityId);
    }
}

/// <summary>
/// Exception thrown when model parsing fails.
/// </summary>
public class ModelParseException : Exception
{
    public ModelParseException(string message) : base(message) { }
    public ModelParseException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Model definition structure - can be deserialized from YAML/JSON.
/// </summary>
public class ModelDefinition
{
    public int SchemaVersion { get; set; }
    public GridDefinition? Grid { get; set; }
    public List<ClassDefinition> Classes { get; set; } = new();
    public TrafficDefinition? Traffic { get; set; }
    public List<NodeDefinition> Nodes { get; set; } = new();
    public List<OutputDefinition> Outputs { get; set; } = new();
    public TopologyDefinition? Topology { get; set; }
}

public class ClassDefinition
{
    public string Id { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}

public class TrafficDefinition
{
    public List<ArrivalDefinition> Arrivals { get; set; } = new();
}

public class ArrivalDefinition
{
    public string NodeId { get; set; } = string.Empty;
    public string ClassId { get; set; } = "*";
    public ArrivalPatternDefinition Pattern { get; set; } = new();
}

public class ArrivalPatternDefinition
{
    public string Kind { get; set; } = string.Empty;
    public double? RatePerBin { get; set; }
    public double? Rate { get; set; }
}

public class GridDefinition
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public string? Start { get; set; }
}

public class NodeDefinition
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
    public PmfDefinition? Pmf { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    // For serviceWithBuffer nodes
    public string? Inflow { get; set; }
    public string? Outflow { get; set; }
    public string? Loss { get; set; }
    public DispatchScheduleDefinition? DispatchSchedule { get; set; }
    /// <summary>Optional WIP limit (scalar). When set, queue depth is capped at this value and overflow is tracked.</summary>
    public double? WipLimit { get; set; }
    /// <summary>Optional WIP limit series reference (node ID). When set, the limit varies per bin. Takes precedence over scalar WipLimit.</summary>
    public string? WipLimitSeries { get; set; }
    /// <summary>Optional WIP overflow target. "loss" (default) or a node ID to receive overflow as inflow.</summary>
    public string? WipOverflow { get; set; }
    // For router nodes
    public RouterDefinition? Router { get; set; }
}

public class RouterDefinition
{
    public RouterInputsDefinition Inputs { get; set; } = new();
    public List<RouterRouteDefinition> Routes { get; set; } = new();
}

public class RouterInputsDefinition
{
    public string? Queue { get; set; }
}

public class RouterRouteDefinition
{
    public string Target { get; set; } = string.Empty;
    public string[]? Classes { get; set; }
    public double? Weight { get; set; }
}

public class DispatchScheduleDefinition
{
    public string Kind { get; set; } = "time-based";
    public int PeriodBins { get; set; }
    public int? PhaseOffset { get; set; }
    public string? CapacitySeries { get; set; }
}

public class PmfDefinition
{
    public double[] Values { get; set; } = Array.Empty<double>();
    public double[] Probabilities { get; set; } = Array.Empty<double>();
}

public class OutputDefinition
{
    public string Series { get; set; } = "";
    public string As { get; set; } = "";
}

public class TopologyDefinition
{
    public List<TopologyNodeDefinition> Nodes { get; set; } = new();
    public List<TopologyEdgeDefinition> Edges { get; set; } = new();
    public List<ConstraintDefinition> Constraints { get; set; } = new();
}

public class TopologyNodeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? NodeRole { get; set; }
    public string? Group { get; set; }
    public UiHintsDefinition? Ui { get; set; }
    public List<string>? Constraints { get; set; }
    public TopologyNodeSemanticsDefinition Semantics { get; set; } = new();
    public InitialConditionDefinition? InitialCondition { get; set; }
    public DispatchScheduleDefinition? DispatchSchedule { get; set; }
    /// <summary>Optional WIP limit for queue-like nodes (scalar). When set, queue depth is capped at this value.</summary>
    public double? WipLimit { get; set; }
    /// <summary>Optional WIP limit series reference (node ID) for time-varying limits. Takes precedence over scalar WipLimit.</summary>
    public string? WipLimitSeries { get; set; }
    /// <summary>Optional WIP overflow target: "loss" (default) or a node ID to receive overflow as inflow.</summary>
    public string? WipOverflow { get; set; }
}

public class TopologyNodeSemanticsDefinition
{
    public string Arrivals { get; set; } = string.Empty;
    public string Served { get; set; } = string.Empty;
    public string? Errors { get; set; }
    public string? Attempts { get; set; }
    public string? Failures { get; set; }
    public string? ExhaustedFailures { get; set; }
    public string? RetryEcho { get; set; }
    public string? RetryBudgetRemaining { get; set; }
    public double[]? RetryKernel { get; set; }
    public string? ExternalDemand { get; set; }
    public string? QueueDepth { get; set; }
    public string? Capacity { get; set; }
    public ParallelismReference? Parallelism { get; set; }
    public string? ProcessingTimeMsSum { get; set; }
    public string? ServedCount { get; set; }
    public double? SlaMin { get; set; }
    public double? MaxAttempts { get; set; }
    public string? BackoffStrategy { get; set; }
    public string? ExhaustedPolicy { get; set; }
    public Dictionary<string, string>? Aliases { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class ConstraintDefinition
{
    public string Id { get; set; } = string.Empty;
    public ConstraintSemanticsDefinition Semantics { get; set; } = new();
}

public class ConstraintSemanticsDefinition
{
    public string Arrivals { get; set; } = string.Empty;
    public string Served { get; set; } = string.Empty;
    public string? Errors { get; set; }
    public string? LatencyMinutes { get; set; }
}

public class TopologyEdgeDefinition
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Measure { get; set; }
    public double? Multiplier { get; set; }
    public int? Lag { get; set; }
}

public class InitialConditionDefinition
{
    public double QueueDepth { get; set; }
}

public class UiHintsDefinition
{
    public double? X { get; set; }
    public double? Y { get; set; }
}

public sealed record ModelMetadata
{
    public required Window Window { get; init; }
    public Topology? Topology { get; init; }
}
