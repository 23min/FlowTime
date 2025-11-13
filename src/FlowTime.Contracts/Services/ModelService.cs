using FlowTime.Contracts.Dtos;
using FlowTime.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Contracts.Services;

/// <summary>
/// Shared service for parsing YAML models and converting DTOs to Core definitions
/// </summary>
public static class ModelService
{
    /// <summary>
    /// Create a YAML deserializer with consistent configuration
    /// </summary>
    public static IDeserializer CreateYamlDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parse YAML string into ModelDto
    /// </summary>
    public static ModelDto ParseYaml(string yaml)
    {
        var deserializer = CreateYamlDeserializer();
        return deserializer.Deserialize<ModelDto>(yaml);
    }

    /// <summary>
    /// Convert ModelDto to Core ModelDefinition
    /// </summary>
    public static ModelDefinition ConvertToModelDefinition(ModelDto model)
    {
        var definition = new ModelDefinition
        {
            Grid = new GridDefinition 
            { 
                Bins = model.Grid.Bins, 
                BinSize = model.Grid.BinSize,
                BinUnit = model.Grid.BinUnit,
                StartTimeUtc = model.Grid.StartTimeUtc
            },
            Nodes = model.Nodes.Select(n => new NodeDefinition 
            { 
                Id = n.Id, 
                Kind = n.Kind, 
                Values = n.Values, 
                Expr = n.Expr,
                Pmf = n.Pmf == null ? null : new PmfDefinition
                {
                    Values = n.Pmf.Values ?? Array.Empty<double>(),
                    Probabilities = n.Pmf.Probabilities ?? Array.Empty<double>()
                },
                // backlog-specific fields (emitted in YAML as inflow/outflow/loss)
                Inflow = n.Inflow,
                Outflow = n.Outflow,
                Loss = n.Loss
            }).ToList(),
            Outputs = model.Outputs.Select(o => new OutputDefinition 
            { 
                Series = o.Series, 
                As = o.As 
            }).ToList()
        };

        if (model.Topology is not null)
        {
            definition.Topology = new TopologyDefinition
            {
                Nodes = model.Topology.Nodes.Select(node => new TopologyNodeDefinition
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Group = node.Group,
                    Ui = node.Ui != null ? new UiHintsDefinition { X = node.Ui.X, Y = node.Ui.Y } : null,
                    Semantics = new TopologyNodeSemanticsDefinition
                    {
                        Arrivals = node.Semantics.Arrivals,
                        Served = node.Semantics.Served,
                        Errors = node.Semantics.Errors,
                        Attempts = node.Semantics.Attempts,
                        Failures = node.Semantics.Failures,
                        RetryEcho = node.Semantics.RetryEcho,
                        RetryKernel = node.Semantics.RetryKernel,
                        ExternalDemand = node.Semantics.ExternalDemand,
                        QueueDepth = node.Semantics.QueueDepth,
                        Capacity = node.Semantics.Capacity,
                        ProcessingTimeMsSum = node.Semantics.ProcessingTimeMsSum,
                        ServedCount = node.Semantics.ServedCount,
                        SlaMin = node.Semantics.SlaMin,
                        Aliases = node.Semantics.Aliases
                    },
                    InitialCondition = node.InitialCondition != null
                        ? new InitialConditionDefinition { QueueDepth = node.InitialCondition.QueueDepth }
                        : null
                }).ToList(),
                Edges = model.Topology.Edges.Select(edge => new TopologyEdgeDefinition
                {
                    Id = edge.Id,
                    Source = edge.From,
                    Target = edge.To,
                    Weight = edge.Weight ?? 1.0,
                    Type = edge.Type,
                    Measure = edge.Measure,
                    Multiplier = edge.Multiplier,
                    Lag = edge.Lag
                }).ToList()
            };
        }

        return definition;
    }

    /// <summary>
    /// Parse YAML and convert to ModelDefinition in one step
    /// </summary>
    public static ModelDefinition ParseAndConvert(string yaml)
    {
        var model = ParseYaml(yaml);
        return ConvertToModelDefinition(model);
    }
}
