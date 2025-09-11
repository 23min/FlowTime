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
        return new ModelDefinition
        {
            Grid = new GridDefinition 
            { 
                Bins = model.Grid.Bins, 
                BinMinutes = model.Grid.BinMinutes 
            },
            Nodes = model.Nodes.Select(n => new NodeDefinition 
            { 
                Id = n.Id, 
                Kind = n.Kind, 
                Values = n.Values, 
                Expr = n.Expr,
                Pmf = n.Pmf
            }).ToList(),
            Outputs = model.Outputs.Select(o => new OutputDefinition 
            { 
                Series = o.Series, 
                As = o.As 
            }).ToList()
        };
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
