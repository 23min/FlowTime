using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.Models;
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

    public async Task<GraphResponse> GetGraphAsync(string runId, CancellationToken cancellationToken = default)
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

        var nodes = modelDefinition.Topology.Nodes.Select(node => new GraphNode
        {
            Id = node.Id,
            Kind = string.IsNullOrWhiteSpace(node.Kind) ? null : node.Kind,
            Semantics = new GraphNodeSemantics
            {
                Arrivals = node.Semantics?.Arrivals ?? string.Empty,
                Served = node.Semantics?.Served ?? string.Empty,
                Errors = node.Semantics?.Errors ?? string.Empty,
                Queue = node.Semantics?.QueueDepth,
                Capacity = node.Semantics?.Capacity
            },
            Ui = node.Ui is null ? null : new GraphNodeUi { X = node.Ui.X, Y = node.Ui.Y }
        }).ToArray();

        var edges = modelDefinition.Topology.Edges.Select(edge =>
        {
            var id = edge.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"{edge.Source}->{edge.Target}";
            }

            return new GraphEdge
            {
                Id = id,
                From = edge.Source,
                To = edge.Target,
                Weight = edge.Weight
            };
        }).ToArray();

        return new GraphResponse
        {
            Nodes = nodes,
            Edges = edges
        };
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
