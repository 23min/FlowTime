using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.Compiler;
using FlowTime.Core.Dispatching;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using FlowTime.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

public sealed class GraphService
{
    private readonly IConfiguration configuration;
    private readonly ILogger<GraphService> logger;

    public GraphService(IConfiguration configuration, ILogger<GraphService> logger)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static readonly string[] defaultOperationalKinds = { "service", "serviceWithBuffer", "queue", "dlq", "router", "external", "sink", "dependency" };
    private static readonly string[] defaultFullKinds = { "service", "queue", "dlq", "router", "external", "expr", "const", "pmf", "serviceWithBuffer", "sink", "dependency" };
    private static readonly string[] defaultDependencyFields = { "arrivals", "served", "errors", "attempts", "failures", "exhaustedFailures", "retryEcho", "retryBudgetRemaining", "queue", "capacity", "expr" };

    private const string edgeTypeTopology = "topology";
    private const string edgeTypeDependency = "dependency";
    private const string dependencyFieldExpression = "expr";

    public async Task<GraphResponse> GetGraphAsync(string runId, GraphQueryOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new GraphQueryException(400, "runId must be provided.");
        }

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var runDirectory = Path.Combine(runsRoot, runId);
        if (!Directory.Exists(runDirectory))
        {
            throw new GraphQueryException(404, $"Run '{runId}' not found.");
        }

        var modelPath = Path.Combine(runDirectory, "model", "model.yaml");
        if (!File.Exists(modelPath))
        {
            throw new GraphQueryException(404, $"Model for run '{runId}' was not found.");
        }

        string modelYaml;
        try
        {
            modelYaml = await File.ReadAllTextAsync(modelPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read model.yaml for run {RunId}", runId);
            throw new GraphQueryException(500, $"Failed to read model.yaml for run '{runId}': {ex.Message}");
        }

        ModelDefinition modelDefinition;
        try
        {
            modelDefinition = ModelService.ParseAndConvert(modelYaml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse model for run {RunId}", runId);
            throw new GraphQueryException(409, $"Model for run '{runId}' could not be parsed: {ex.Message}");
        }

        var authoredModelDefinition = modelDefinition;
        modelDefinition = ModelCompiler.Compile(modelDefinition, logger);

        if (authoredModelDefinition.Topology is null)
        {
            throw new GraphQueryException(412, $"Run '{runId}' does not include topology information.");
        }

        var runtimeModel = ModelParser.ParseMetadata(modelDefinition);
        var runtimeNodesById = runtimeModel.Topology?.Nodes?
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        options ??= GraphQueryOptions.Default;
        var mode = options.Mode;

        var nodeDefinitions = modelDefinition.Nodes ?? new List<NodeDefinition>();
        var nodeDefinitionsById = nodeDefinitions
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id!, StringComparer.OrdinalIgnoreCase);

        if (modelDefinition.Topology?.Nodes != null)
        {
            foreach (var topoNode in modelDefinition.Topology.Nodes)
            {
                if (string.IsNullOrWhiteSpace(topoNode.Id) || nodeDefinitionsById.ContainsKey(topoNode.Id))
                {
                    continue;
                }

                nodeDefinitionsById[topoNode.Id] = new NodeDefinition
                {
                    Id = topoNode.Id,
                    Kind = topoNode.Kind ?? "service",
                    DispatchSchedule = topoNode.DispatchSchedule,
                    Metadata = topoNode.Semantics?.Metadata
                };
            }
        }

        var allowedKinds = options.Kinds?.Count > 0
            ? new HashSet<string>(options.Kinds, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(mode == GraphQueryMode.Full ? defaultFullKinds : defaultOperationalKinds, StringComparer.OrdinalIgnoreCase);

        var dependencyFields = options.DependencyFields?.Count > 0
            ? new HashSet<string>(options.DependencyFields, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(defaultDependencyFields, StringComparer.OrdinalIgnoreCase);

        var graphNodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var graphEdges = new Dictionary<string, GraphEdge>(StringComparer.OrdinalIgnoreCase);

        void AddOrReplaceNode(GraphNode node)
        {
            graphNodes[node.Id] = node;
        }

        void TryAddEdge(GraphEdge edge)
        {
            graphEdges[edge.Id] = edge;
        }

        // Operational topology nodes
        foreach (var topoNode in authoredModelDefinition.Topology.Nodes)
        {
            nodeDefinitionsById.TryGetValue(topoNode.Id, out var nodeDefinition);
            runtimeNodesById.TryGetValue(topoNode.Id, out var runtimeNode);
            var kind = string.IsNullOrWhiteSpace(topoNode.Kind)
                ? nodeDefinition?.Kind ?? "service"
                : topoNode.Kind!;
            var analytical = runtimeNode is not null && runtimeNode.Analytical.Identity != AnalyticalIdentity.Unknown
                ? runtimeNode.Analytical
                : runtimeNode is not null
                    ? AnalyticalDescriptorCompiler.Build(runtimeNode.Id, runtimeNode.Kind, runtimeNode.Semantics, nodeDefinitionsById)
                    : CompileAnalyticalDescriptor(topoNode.Id, kind, topoNode.Semantics?.QueueDepth, topoNode.Semantics?.Parallelism, nodeDefinitionsById);
            var logicalType = ProjectLogicalType(kind, analytical);
            if (!allowedKinds.Contains(kind) && !allowedKinds.Contains(logicalType))
            {
                continue;
            }

            var parallelismReference = runtimeNode?.Semantics.ParallelismRawText
                ?? runtimeNode?.Semantics.ParallelismRef?.CanonicalText
                ?? NormalizeParallelismLiteral(topoNode.Semantics?.Parallelism);
            var semantics = new GraphNodeSemantics
            {
                Arrivals = topoNode.Semantics?.Arrivals ?? string.Empty,
                Served = topoNode.Semantics?.Served ?? string.Empty,
                Errors = topoNode.Semantics?.Errors ?? string.Empty,
                Attempts = topoNode.Semantics?.Attempts,
                Failures = topoNode.Semantics?.Failures,
                ExhaustedFailures = topoNode.Semantics?.ExhaustedFailures,
                RetryEcho = topoNode.Semantics?.RetryEcho,
                RetryBudgetRemaining = topoNode.Semantics?.RetryBudgetRemaining,
                Queue = topoNode.Semantics?.QueueDepth,
                Capacity = topoNode.Semantics?.Capacity,
                Parallelism = parallelismReference,
                Aliases = topoNode.Semantics?.Aliases,
                Metadata = topoNode.Semantics?.Metadata,
                MaxAttempts = topoNode.Semantics?.MaxAttempts,
                BackoffStrategy = topoNode.Semantics?.BackoffStrategy,
                ExhaustedPolicy = topoNode.Semantics?.ExhaustedPolicy
            };

            var ui = topoNode.Ui is null
                ? null
                : new GraphNodeUi
                {
                    X = topoNode.Ui.X,
                    Y = topoNode.Ui.Y
                };

            var dispatchSchedule = TryBuildDispatchSchedule(nodeDefinitionsById, topoNode.Id);
            if (dispatchSchedule is null && topoNode.DispatchSchedule is not null)
            {
                dispatchSchedule = ConvertSchedule(topoNode.DispatchSchedule);
            }
            if (dispatchSchedule is null &&
                !string.IsNullOrWhiteSpace(analytical.QueueSourceNodeId) &&
                !string.Equals(analytical.QueueSourceNodeId, topoNode.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(analytical.QueueSourceNodeId, "self", StringComparison.OrdinalIgnoreCase) &&
                nodeDefinitionsById.TryGetValue(analytical.QueueSourceNodeId, out var sourceDefinition))
            {
                dispatchSchedule = ConvertSchedule(sourceDefinition.DispatchSchedule);
            }

            AddOrReplaceNode(new GraphNode
            {
                Id = topoNode.Id,
                Kind = kind,
                NodeRole = string.IsNullOrWhiteSpace(topoNode.NodeRole) ? null : topoNode.NodeRole,
                LogicalType = logicalType,
                Semantics = semantics,
                Ui = ui,
                DispatchSchedule = dispatchSchedule
            });
        }

        // Topology edges
        foreach (var topoEdge in authoredModelDefinition.Topology.Edges)
        {
            var fromNodeId = ExtractNodeId(topoEdge.Source);
            var toNodeId = ExtractNodeId(topoEdge.Target);

            if (!graphNodes.ContainsKey(fromNodeId) || !graphNodes.ContainsKey(toNodeId))
            {
                continue;
            }

            var edgeId = string.IsNullOrWhiteSpace(topoEdge.Id)
                ? $"{topoEdge.Source}->{topoEdge.Target}"
                : topoEdge.Id!;

            var edgeType = string.IsNullOrWhiteSpace(topoEdge.Type)
                ? edgeTypeTopology
                : topoEdge.Type!;

            TryAddEdge(new GraphEdge
            {
                Id = edgeId,
                From = topoEdge.Source,
                To = topoEdge.Target,
                Weight = topoEdge.Weight,
                EdgeType = edgeType,
                Field = topoEdge.Measure,
                Multiplier = topoEdge.Multiplier,
                Lag = topoEdge.Lag
            });
        }

        if (mode == GraphQueryMode.Full)
        {
            // Include non-operational nodes (const/expr/pmf) based on allowed kinds.
            foreach (var nodeDef in nodeDefinitions)
            {
                if (string.IsNullOrWhiteSpace(nodeDef.Id))
                {
                    continue;
                }

                if (IsGraphHidden(nodeDef.Metadata))
                {
                    continue;
                }

                var actualKind = string.IsNullOrWhiteSpace(nodeDef.Kind) ? "const" : nodeDef.Kind!;
                var displayKind = ResolveDisplayKind(nodeDef, actualKind);
                if (!allowedKinds.Contains(actualKind) && !allowedKinds.Contains(displayKind))
                {
                    continue;
                }

                if (graphNodes.ContainsKey(nodeDef.Id))
                {
                    continue; // already added via topology
                }

                GraphNodeDistribution? distribution = null;
                if (nodeDef.Pmf is { } pmfDef &&
                    pmfDef.Values is { Length: > 0 } &&
                    pmfDef.Probabilities is { Length: > 0 })
                {
                    distribution = new GraphNodeDistribution
                    {
                        Values = pmfDef.Values ?? Array.Empty<double>(),
                        Probabilities = pmfDef.Probabilities ?? Array.Empty<double>()
                    };
                }

                double[]? inlineValues = null;
                if (nodeDef.Values is { Length: > 0 })
                {
                    inlineValues = nodeDef.Values;
                }

                string? expression = null;
                if (string.Equals(actualKind, "expr", StringComparison.OrdinalIgnoreCase))
                {
                    expression = nodeDef.Expr;
                }

                var semantics = new GraphNodeSemantics
                {
                    Series = $"series:{nodeDef.Id}",
                    Expression = string.IsNullOrWhiteSpace(expression) ? null : expression,
                    Distribution = distribution,
                    InlineValues = inlineValues,
                    Metadata = nodeDef.Metadata
                };

                AddOrReplaceNode(new GraphNode
                {
                    Id = nodeDef.Id,
                    Kind = displayKind,
                    LogicalType = ProjectLogicalType(actualKind, CompileAnalyticalDescriptor(nodeDef.Id, actualKind, queueDepth: null, parallelism: null, nodeDefinitionsById)),
                    Semantics = semantics,
                    DispatchSchedule = TryBuildDispatchSchedule(nodeDefinitionsById, nodeDef.Id)
                });
            }

            // Expression dependencies (expr inputs).
            foreach (var nodeDef in nodeDefinitions)
            {
                if (!string.Equals(nodeDef.Kind, "expr", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!graphNodes.ContainsKey(nodeDef.Id))
                {
                    continue;
                }

                var includeExpressionDependencies = dependencyFields.Contains(dependencyFieldExpression);
                if (!includeExpressionDependencies)
                {
                    continue;
                }

                var inputs = GraphAnalyzer.GetNodeInputs(nodeDef, modelDefinition);
                foreach (var input in inputs)
                {
                    if (!graphNodes.ContainsKey(input))
                    {
                        continue;
                    }

                    var edgeId = BuildDependencyEdgeId(input, nodeDef.Id, dependencyFieldExpression);
                    TryAddEdge(new GraphEdge
                    {
                        Id = edgeId,
                        From = input,
                        To = nodeDef.Id,
                        Weight = ComputeDependencyWeight(options.EdgeWeight, input, nodeDef.Id),
                        EdgeType = edgeTypeDependency,
                        Field = dependencyFieldExpression
                    });
                }
            }

            // Service/queue dependency edges from producers.
            foreach (var topoNode in authoredModelDefinition.Topology.Nodes)
            {
                if (!graphNodes.ContainsKey(topoNode.Id))
                {
                    continue;
                }

                runtimeNodesById.TryGetValue(topoNode.Id, out var runtimeNode);
                var runtimeSemantics = runtimeNode?.Semantics;

                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.ArrivalsRef, "arrivals");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.ServedRef, "served");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.ErrorsRef, "errors");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.AttemptsRef, "attempts");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.FailuresRef, "failures");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.RetryEchoRef, "retryEcho");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.QueueDepthRef, "queue");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.CapacityRef, "capacity");
                AddSemanticsDependencyEdge(topoNode.Id, runtimeSemantics?.ParallelismRef?.Series, "parallelism");
            }

            void AddSemanticsDependencyEdge(string consumerId, CompiledSeriesReference? reference, string field)
            {
                if (reference is null)
                {
                    return;
                }

                if (!dependencyFields.Contains(field))
                {
                    return;
                }

                var producerCandidate = reference.ProducerIdCandidate;
                if (producerCandidate is null)
                {
                    return;
                }

                if (!graphNodes.ContainsKey(producerCandidate))
                {
                    return;
                }

                if (string.Equals(producerCandidate, consumerId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var edgeId = BuildDependencyEdgeId(producerCandidate, consumerId, field);
                TryAddEdge(new GraphEdge
                {
                    Id = edgeId,
                    From = producerCandidate,
                    To = consumerId,
                    Weight = ComputeDependencyWeight(options.EdgeWeight, producerCandidate, consumerId),
                    EdgeType = edgeTypeDependency,
                    Field = field
                });
            }
        }

        return new GraphResponse
        {
            Nodes = graphNodes.Values.ToArray(),
            Edges = graphEdges.Values.ToArray()
        };
    }

    private static double ComputeDependencyWeight(GraphEdgeWeightMode mode, string source, string target)
    {
        // Placeholder for future contribution inference.
        _ = source;
        _ = target;
        return 1.0;
    }

    private static string ResolveDisplayKind(NodeDefinition nodeDef, string fallbackKind)
    {
        var originKind = TryGetMetadataValue(nodeDef.Metadata, "origin.kind");
        if (!string.IsNullOrWhiteSpace(originKind))
        {
            return originKind.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackKind) ? "const" : fallbackKind;
    }

    private static string? TryGetMetadataValue(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        if (metadata is null)
        {
            return null;
        }

        if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    private static bool IsGraphHidden(IReadOnlyDictionary<string, string>? metadata)
    {
        var graphHidden = TryGetMetadataValue(metadata, "graph.hidden");
        if (!string.IsNullOrWhiteSpace(graphHidden) &&
            string.Equals(graphHidden, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var uiHidden = TryGetMetadataValue(metadata, "ui.hidden");
        return !string.IsNullOrWhiteSpace(uiHidden) &&
            string.Equals(uiHidden, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDependencyEdgeId(string from, string to, string field) =>
        $"dep:{from}->{to}:{field}";

    private static string ExtractNodeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var colon = value.IndexOf(':');
        return colon < 0 ? value.Trim() : value[..colon].Trim();
    }
    private static DispatchScheduleDescriptor? TryBuildDispatchSchedule(
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeDefinitions.TryGetValue(nodeId, out var definition))
        {
            return null;
        }

        return ConvertSchedule(definition.DispatchSchedule);
    }

    private static DispatchScheduleDescriptor? ConvertSchedule(DispatchScheduleDefinition? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        var period = schedule.PeriodBins;
        if (period <= 0)
        {
            return null;
        }

        var kind = string.IsNullOrWhiteSpace(schedule.Kind) ? "time-based" : schedule.Kind.Trim();
        var normalizedPhase = DispatchScheduleProcessor.NormalizePhase(schedule.PhaseOffset ?? 0, period);
        var capacitySeries = string.IsNullOrWhiteSpace(schedule.CapacitySeries)
            ? null
            : schedule.CapacitySeries.Trim();

        return new DispatchScheduleDescriptor
        {
            Kind = kind,
            PeriodBins = period,
            PhaseOffset = normalizedPhase,
            CapacitySeries = capacitySeries
        };
    }

    private static string ProjectLogicalType(string? kind, AnalyticalDescriptor analytical)
    {
        return analytical.Identity switch
        {
            AnalyticalIdentity.ServiceWithBuffer => "serviceWithBuffer",
            AnalyticalIdentity.Service => "service",
            AnalyticalIdentity.Queue => "queue",
            AnalyticalIdentity.Dlq => "dlq",
            AnalyticalIdentity.Router => "router",
            AnalyticalIdentity.External => "external",
            AnalyticalIdentity.Sink => "sink",
            AnalyticalIdentity.Dependency => "dependency",
            _ => NormalizeAuthoredKind(kind)
        };
    }

    private static string NormalizeAuthoredKind(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? "service" : kind.Trim().ToLowerInvariant();

    private static AnalyticalDescriptor CompileAnalyticalDescriptor(
        string nodeId,
        string? kind,
        string? queueDepth,
        object? parallelism,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions)
    {
        var normalizedQueueDepth = string.IsNullOrWhiteSpace(queueDepth) ? null : queueDepth.Trim();
        var parallelismRef = SemanticReferenceResolver.ParseParallelismReference(parallelism);

        return AnalyticalDescriptorCompiler.Build(
            nodeId,
            kind,
            new NodeSemantics
            {
                Arrivals = string.Empty,
                Served = string.Empty,
                QueueDepth = normalizedQueueDepth,
                QueueDepthRef = SemanticReferenceResolver.ParseOptionalSeriesReference(normalizedQueueDepth),
                ParallelismRawText = parallelismRef?.RawText,
                ParallelismRef = parallelismRef
            },
            nodeDefinitions);
    }

    private static string? NormalizeParallelismLiteral(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        if (value is IFormattable formattable)
        {
            var formatted = formattable.ToString(null, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }
}

public sealed class GraphQueryException : Exception
{
    public int StatusCode { get; }

    public GraphQueryException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public enum GraphQueryMode
{
    Operational,
    Full
}

public enum GraphEdgeWeightMode
{
    Uniform,
    Contribution
}

public sealed class GraphQueryOptions
{
    public static GraphQueryOptions Default { get; } = new();

    public GraphQueryMode Mode { get; init; } = GraphQueryMode.Operational;
    public IReadOnlyCollection<string>? Kinds { get; init; }
    public IReadOnlyCollection<string>? DependencyFields { get; init; }
    public GraphEdgeWeightMode EdgeWeight { get; init; } = GraphEdgeWeightMode.Uniform;
}
