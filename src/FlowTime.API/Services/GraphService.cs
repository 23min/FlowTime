using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.Storage;
using FlowTime.Contracts.TimeTravel;
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
        string runDirectory;
        try
        {
            runDirectory = RunPathResolver.GetSafeRunDirectory(runsRoot, runId);
        }
        catch (ArgumentException)
        {
            throw new GraphQueryException(404, $"Run '{runId}' not found.");
        }

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

        if (modelDefinition.Topology is null)
        {
            throw new GraphQueryException(412, $"Run '{runId}' does not include topology information.");
        }

        ModelMetadata metadata;
        try
        {
            metadata = ModelParser.ParseMetadata(modelDefinition, Path.GetDirectoryName(modelPath));
        }
        catch (ModelParseException ex)
        {
            logger.LogError(ex, "Failed to parse typed topology metadata for run {RunId}", runId);
            throw new GraphQueryException(409, $"Model for run '{runId}' could not be projected: {ex.Message}");
        }

        if (metadata.Topology is null)
        {
            throw new GraphQueryException(412, $"Run '{runId}' does not include topology information.");
        }

        options ??= GraphQueryOptions.Default;
        var mode = options.Mode;

        var nodeDefinitions = modelDefinition.Nodes ?? new List<NodeDefinition>();
        var nodeDefinitionsById = nodeDefinitions
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id!, StringComparer.OrdinalIgnoreCase);

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
        foreach (var topoNode in metadata.Topology.Nodes)
        {
            nodeDefinitionsById.TryGetValue(topoNode.Id, out var nodeDefinition);
            var kind = string.IsNullOrWhiteSpace(topoNode.Kind)
                ? nodeDefinition?.Kind ?? "service"
                : topoNode.Kind!;
            if (!allowedKinds.Contains(kind))
            {
                continue;
            }

            var semantics = new GraphNodeSemantics
            {
                Arrivals = ToAuthoredReference(topoNode.Semantics?.Arrivals) ?? string.Empty,
                Served = ToAuthoredReference(topoNode.Semantics?.Served) ?? string.Empty,
                Errors = ToAuthoredReference(topoNode.Semantics?.Errors) ?? string.Empty,
                Attempts = ToAuthoredReference(topoNode.Semantics?.Attempts),
                Failures = ToAuthoredReference(topoNode.Semantics?.Failures),
                ExhaustedFailures = ToAuthoredReference(topoNode.Semantics?.ExhaustedFailures),
                RetryEcho = ToAuthoredReference(topoNode.Semantics?.RetryEcho),
                RetryBudgetRemaining = ToAuthoredReference(topoNode.Semantics?.RetryBudgetRemaining),
                Queue = ToAuthoredReference(topoNode.Semantics?.QueueDepth),
                Capacity = ToAuthoredReference(topoNode.Semantics?.Capacity),
                Parallelism = topoNode.Semantics?.Parallelism?.ToAuthoredValue(),
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
            if (dispatchSchedule is null && !string.IsNullOrWhiteSpace(topoNode.Analytical.QueueSourceNodeId))
            {
                dispatchSchedule = TryBuildDispatchSchedule(nodeDefinitionsById, topoNode.Analytical.QueueSourceNodeId);
            }
            if (dispatchSchedule is null && topoNode.DispatchSchedule is not null)
            {
                dispatchSchedule = ConvertSchedule(topoNode.DispatchSchedule);
            }

            AddOrReplaceNode(new GraphNode
            {
                Id = topoNode.Id,
                Kind = kind,
                Category = topoNode.Analytical.ToContractCategory(),
                Analytical = BuildAnalyticalFacts(topoNode.Analytical),
                NodeRole = string.IsNullOrWhiteSpace(topoNode.NodeRole) ? null : topoNode.NodeRole,
                Semantics = semantics,
                Ui = ui,
                DispatchSchedule = dispatchSchedule
            });
        }

        // Topology edges
        foreach (var topoEdge in metadata.Topology.Edges)
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

            var edgeType = string.IsNullOrWhiteSpace(topoEdge.EdgeType)
                ? edgeTypeTopology
                : topoEdge.EdgeType!;

            TryAddEdge(new GraphEdge
            {
                Id = edgeId,
                From = topoEdge.Source,
                To = topoEdge.Target,
                Weight = topoEdge.Weight,
                EdgeType = edgeType,
                Field = topoEdge.Field,
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

                var descriptor = BuildDescriptorFromKind(actualKind);

                AddOrReplaceNode(new GraphNode
                {
                    Id = nodeDef.Id,
                    Kind = displayKind,
                    Category = descriptor.ToContractCategory(),
                    Analytical = BuildAnalyticalFacts(descriptor),
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
            foreach (var topoNode in metadata.Topology.Nodes)
            {
                if (!graphNodes.ContainsKey(topoNode.Id))
                {
                    continue;
                }

                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Arrivals, "arrivals");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Served, "served");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Errors, "errors");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Attempts, "attempts");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Failures, "failures");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.RetryEcho, "retryEcho");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.QueueDepth, "queue");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Capacity, "capacity");
                AddSemanticsDependencyEdge(topoNode.Id, topoNode.Semantics?.Parallelism?.SeriesReference, "parallelism");
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

                var producerCandidate = reference.ResolveProducerId(consumerId);
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

    private static NodeAnalyticalFacts BuildAnalyticalFacts(RuntimeAnalyticalDescriptor descriptor)
    {
        return new NodeAnalyticalFacts
        {
            Identity = descriptor.ToContractIdentity(),
            HasQueueSemantics = descriptor.HasQueueSemantics,
            HasServiceSemantics = descriptor.HasServiceSemantics,
            HasCycleTimeDecomposition = descriptor.HasCycleTimeDecomposition,
            StationarityWarningApplicable = descriptor.StationarityWarningApplicable
        };
    }

    private static RuntimeAnalyticalDescriptor BuildDescriptorFromKind(string? kind, string? nodeRole = null)
    {
        var normalizedKind = NormalizeKind(kind);
        var category = ResolveCategory(normalizedKind, nodeRole);
        var hasQueueSemantics = category is RuntimeAnalyticalNodeCategory.Queue or RuntimeAnalyticalNodeCategory.Dlq
            || string.Equals(normalizedKind, "servicewithbuffer", StringComparison.OrdinalIgnoreCase);
        var hasServiceSemantics = category == RuntimeAnalyticalNodeCategory.Service;

        return new RuntimeAnalyticalDescriptor
        {
            Identity = ResolveIdentity(normalizedKind, category, hasQueueSemantics, hasServiceSemantics),
            Category = category,
            HasQueueSemantics = hasQueueSemantics,
            HasServiceSemantics = hasServiceSemantics,
            HasCycleTimeDecomposition = hasQueueSemantics && hasServiceSemantics,
            StationarityWarningApplicable = hasQueueSemantics,
            QueueSourceNodeId = null,
            Parallelism = null
        };
    }

    private static RuntimeAnalyticalNodeCategory ResolveCategory(string normalizedKind, string? nodeRole)
    {
        if (string.Equals(nodeRole, "sink", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedKind, "sink", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeAnalyticalNodeCategory.Sink;
        }

        return normalizedKind switch
        {
            "queue" => RuntimeAnalyticalNodeCategory.Queue,
            "dlq" => RuntimeAnalyticalNodeCategory.Dlq,
            "router" => RuntimeAnalyticalNodeCategory.Router,
            "dependency" => RuntimeAnalyticalNodeCategory.Dependency,
            "const" or "constant" or "pmf" => RuntimeAnalyticalNodeCategory.Constant,
            "expr" or "expression" => RuntimeAnalyticalNodeCategory.Expression,
            _ => RuntimeAnalyticalNodeCategory.Service
        };
    }

    private static RuntimeAnalyticalIdentity ResolveIdentity(
        string normalizedKind,
        RuntimeAnalyticalNodeCategory category,
        bool hasQueueSemantics,
        bool hasServiceSemantics)
    {
        if (category == RuntimeAnalyticalNodeCategory.Service)
        {
            return hasQueueSemantics && hasServiceSemantics
                ? RuntimeAnalyticalIdentity.ServiceWithBuffer
                : RuntimeAnalyticalIdentity.Service;
        }

        return category switch
        {
            RuntimeAnalyticalNodeCategory.Queue => RuntimeAnalyticalIdentity.Queue,
            RuntimeAnalyticalNodeCategory.Dlq => RuntimeAnalyticalIdentity.Dlq,
            RuntimeAnalyticalNodeCategory.Router => RuntimeAnalyticalIdentity.Router,
            RuntimeAnalyticalNodeCategory.Dependency => RuntimeAnalyticalIdentity.Dependency,
            RuntimeAnalyticalNodeCategory.Sink => RuntimeAnalyticalIdentity.Sink,
            RuntimeAnalyticalNodeCategory.Constant => normalizedKind == "pmf"
                ? RuntimeAnalyticalIdentity.Pmf
                : RuntimeAnalyticalIdentity.Constant,
            RuntimeAnalyticalNodeCategory.Expression => RuntimeAnalyticalIdentity.Expression,
            _ => RuntimeAnalyticalIdentity.Service
        };
    }

    private static string NormalizeKind(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? "service" : kind.Trim().ToLowerInvariant();

    private static string? ToAuthoredReference(CompiledSeriesReference? value) => value?.RawText;
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
