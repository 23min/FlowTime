using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
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

    private static readonly string[] DefaultOperationalKinds = { "service", "queue", "router", "external" };
    private static readonly string[] DefaultFullKinds = { "service", "queue", "router", "external", "expr", "const", "pmf" };
    private static readonly string[] DefaultDependencyFields = { "arrivals", "served", "errors", "attempts", "failures", "retryEcho", "queue", "capacity", "expr" };

    private const string EdgeTypeTopology = "topology";
    private const string EdgeTypeDependency = "dependency";
    private const string DependencyFieldExpression = "expr";

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

        if (modelDefinition.Topology is null)
        {
            throw new GraphQueryException(412, $"Run '{runId}' does not include topology information.");
        }

        options ??= GraphQueryOptions.Default;
        var mode = options.Mode;

        var allowedKinds = options.Kinds?.Count > 0
            ? new HashSet<string>(options.Kinds, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(mode == GraphQueryMode.Full ? DefaultFullKinds : DefaultOperationalKinds, StringComparer.OrdinalIgnoreCase);

        var dependencyFields = options.DependencyFields?.Count > 0
            ? new HashSet<string>(options.DependencyFields, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(DefaultDependencyFields, StringComparer.OrdinalIgnoreCase);

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
        foreach (var topoNode in modelDefinition.Topology.Nodes)
        {
            var kind = string.IsNullOrWhiteSpace(topoNode.Kind) ? "service" : topoNode.Kind!;
            if (!allowedKinds.Contains(kind))
            {
                continue;
            }

            var semantics = new GraphNodeSemantics
            {
                Arrivals = topoNode.Semantics?.Arrivals ?? string.Empty,
                Served = topoNode.Semantics?.Served ?? string.Empty,
                Errors = topoNode.Semantics?.Errors ?? string.Empty,
                Attempts = topoNode.Semantics?.Attempts,
                Failures = topoNode.Semantics?.Failures,
                RetryEcho = topoNode.Semantics?.RetryEcho,
                Queue = topoNode.Semantics?.QueueDepth,
                Capacity = topoNode.Semantics?.Capacity,
                Aliases = topoNode.Semantics?.Aliases,
                Metadata = null
            };

            var ui = topoNode.Ui is null
                ? null
                : new GraphNodeUi
                {
                    X = topoNode.Ui.X,
                    Y = topoNode.Ui.Y
                };

            AddOrReplaceNode(new GraphNode
            {
                Id = topoNode.Id,
                Kind = kind,
                Semantics = semantics,
                Ui = ui
            });
        }

        // Topology edges
        foreach (var topoEdge in modelDefinition.Topology.Edges)
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
                ? EdgeTypeTopology
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
            var nodeDefinitionsById = modelDefinition.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var nodeDef in modelDefinition.Nodes)
            {
                if (string.IsNullOrWhiteSpace(nodeDef.Id))
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
                    Semantics = semantics
                });
            }

            // Expression dependencies (expr inputs).
            foreach (var nodeDef in modelDefinition.Nodes)
            {
                if (!string.Equals(nodeDef.Kind, "expr", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!graphNodes.ContainsKey(nodeDef.Id))
                {
                    continue;
                }

                var includeExpressionDependencies = dependencyFields.Contains(DependencyFieldExpression);
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

                    var edgeId = BuildDependencyEdgeId(input, nodeDef.Id, DependencyFieldExpression);
                    TryAddEdge(new GraphEdge
                    {
                        Id = edgeId,
                        From = input,
                        To = nodeDef.Id,
                        Weight = ComputeDependencyWeight(options.EdgeWeight, input, nodeDef.Id),
                        EdgeType = EdgeTypeDependency,
                        Field = DependencyFieldExpression
                    });
                }
            }

            // Service/queue dependency edges from producers.
            foreach (var topoNode in modelDefinition.Topology.Nodes)
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
            }

            void AddSemanticsDependencyEdge(string consumerId, string? reference, string field)
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    return;
                }

                if (!dependencyFields.Contains(field))
                {
                    return;
                }

                var producerCandidate = TryResolveProducerId(reference);
                if (producerCandidate is null)
                {
                    return;
                }

                if (!graphNodes.ContainsKey(producerCandidate))
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
                    EdgeType = EdgeTypeDependency,
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

    private static readonly Regex SeriesFileRegex = new(@"(?<name>[^/\\]+?)(?:\.csv)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string? TryResolveProducerId(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var value = reference.Trim();

        if (value.StartsWith("series:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["series:".Length..];
        }
        else if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var match = SeriesFileRegex.Match(value);
            if (match.Success)
            {
                value = match.Groups["name"].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var at = value.IndexOf('@');
        if (at > 0)
        {
            value = value[..at];
        }

        return value.Trim();
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
