using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Transforms validated templates into KISS-compliant simulation models.
/// </summary>
internal static class SimModelBuilder
{
    public static SimModelArtifact Build(Template template, Dictionary<string, object?> parameterValues, string substitutedYaml)
    {
        ArgumentNullException.ThrowIfNull(template);

        var artifact = new SimModelArtifact
        {
            SchemaVersion = template.SchemaVersion,
            Generator = template.Generator,
            Mode = template.Mode.ToSerializedValue(),
            Metadata = CloneMetadata(template.Metadata),
            Window = CloneWindow(template.Window),
            Grid = CloneGrid(template.Grid),
            Topology = CloneTopology(template.Topology),
            Nodes = BuildNodes(template.Nodes),
            Outputs = BuildOutputs(template.Outputs)
        };
        if (artifact.Grid != null && string.IsNullOrWhiteSpace(artifact.Grid.Start))
        {
            artifact.Grid.Start = artifact.Window?.Start;
        }

        artifact.Provenance = BuildProvenance(template, parameterValues, substitutedYaml);
        return artifact;
    }

    private static TemplateMetadata CloneMetadata(TemplateMetadata metadata)
    {
        return new TemplateMetadata
        {
            Id = metadata.Id,
            Title = metadata.Title,
            Description = metadata.Description,
            Version = metadata.Version,
            Tags = new List<string>(metadata.Tags)
        };
    }

    private static TemplateWindow CloneWindow(TemplateWindow window)
    {
        return new TemplateWindow
        {
            Start = window.Start,
            Timezone = window.Timezone
        };
    }

    private static TemplateGrid CloneGrid(TemplateGrid grid)
    {
        return new TemplateGrid
        {
            Bins = grid.Bins,
            BinSize = grid.BinSize,
            BinUnit = grid.BinUnit,
            Start = grid.Start
        };
    }

    private static TemplateTopology CloneTopology(TemplateTopology topology)
    {
        var clone = new TemplateTopology
        {
            Nodes = new List<TemplateTopologyNode>(),
            Edges = new List<TemplateTopologyEdge>()
        };

        foreach (var node in topology.Nodes)
        {
            clone.Nodes.Add(new TemplateTopologyNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Group = node.Group,
                Semantics = node.Semantics == null ? new TemplateNodeSemantics() : CloneSemantics(node.Semantics),
                InitialCondition = node.InitialCondition == null ? null : new TemplateInitialCondition { QueueDepth = node.InitialCondition.QueueDepth },
                Ui = node.Ui == null ? null : new TemplateUiHint { X = node.Ui.X, Y = node.Ui.Y }
            });
        }

        foreach (var edge in topology.Edges)
        {
            clone.Edges.Add(new TemplateTopologyEdge
            {
                Id = edge.Id,
                From = edge.From,
                To = edge.To,
                Weight = edge.Weight
            });
        }

        return clone;
    }

    private static TemplateNodeSemantics CloneSemantics(TemplateNodeSemantics semantics) => new()
    {
        Arrivals = semantics.Arrivals,
        Served = semantics.Served,
        Errors = semantics.Errors,
        Queue = semantics.Queue,
        Capacity = semantics.Capacity,
        ExternalDemand = semantics.ExternalDemand
    };

    private static List<SimNode> BuildNodes(List<TemplateNode> nodes)
    {
        return nodes.Select(node => new SimNode
        {
            Id = node.Id,
            Kind = node.Kind,
            Values = node.Values?.ToArray(),
            Expr = node.Expr,
            Source = node.Source,
            Pmf = node.Pmf == null ? null : new PmfSpec
            {
                Values = node.Pmf.Values?.ToArray() ?? Array.Empty<double>(),
                Probabilities = node.Pmf.Probabilities?.ToArray() ?? Array.Empty<double>()
            },
            Initial = node.Initial
        }).ToList();
    }

    private static List<SimOutput> BuildOutputs(List<TemplateOutput> outputs)
    {
        return outputs.Select(output => new SimOutput
        {
            Series = output.Series,
            Exclude = output.Exclude == null ? null : new List<string>(output.Exclude),
            As = output.As
        }).ToList();
    }

    private static SimProvenance BuildProvenance(Template template, Dictionary<string, object?> parameterValues, string substitutedYaml)
    {
        var now = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var generator = ResolveGeneratorIdentifier(template);
        var modelId = ComputeModelId(substitutedYaml);
        var templateVersion = !string.IsNullOrWhiteSpace(template.Provenance?.TemplateVersion)
            ? template.Provenance!.TemplateVersion
            : template.Metadata.Version;

        var parameters = template.Provenance?.Parameters != null && template.Provenance.Parameters.Count > 0
            ? new Dictionary<string, object?>(template.Provenance.Parameters, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in parameterValues)
        {
            parameters[kvp.Key] = kvp.Value;
        }

        return new SimProvenance
        {
            Source = template.Provenance?.Source ?? "flowtime-sim",
            Generator = generator,
            GeneratedAt = template.Provenance?.GeneratedAt ?? now,
            TemplateId = template.Metadata.Id,
            TemplateVersion = templateVersion,
            Mode = template.Provenance?.Mode ?? template.Mode.ToSerializedValue(),
            ModelId = template.Provenance?.ModelId ?? modelId,
            SchemaVersion = template.SchemaVersion,
            Parameters = parameters
        };
    }

    private static string ResolveGeneratorIdentifier(Template template)
    {
        if (!string.IsNullOrWhiteSpace(template.Provenance?.Generator))
        {
            return template.Provenance!.Generator;
        }

        var assemblyVersion = typeof(SimModelBuilder).Assembly.GetName().Version;
        var version = assemblyVersion == null
            ? "0.0.0"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

        return $"{template.Generator}/{version}";
    }

    private static string ComputeModelId(string substitutedYaml)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(substitutedYaml);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
