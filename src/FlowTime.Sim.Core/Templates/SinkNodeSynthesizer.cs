using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

internal static class SinkNodeSynthesizer
{
    public static void Apply(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Topology?.Nodes is null || template.Topology.Nodes.Count == 0)
        {
            return;
        }

        if (template.Grid == null || template.Grid.Bins <= 0)
        {
            throw new TemplateValidationException("Template grid must be defined to synthesize sink metrics.");
        }

        var existingNodeIds = new HashSet<string>(
            template.Nodes.Select(n => n.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var topologyNode in template.Topology.Nodes)
        {
            if (!string.Equals(topologyNode.Kind?.Trim(), "sink", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var semantics = topologyNode.Semantics
                ?? throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.");

            var arrivalsId = semantics.Arrivals?.Trim();
            if (string.IsNullOrWhiteSpace(arrivalsId))
            {
                throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.arrivals for sink nodes.");
            }

            var servedId = semantics.Served?.Trim();
            var errorsId = semantics.Errors?.Trim();
            var hasErrors = !string.IsNullOrWhiteSpace(errorsId);

            if (string.IsNullOrWhiteSpace(servedId))
            {
                if (hasErrors)
                {
                    var derivedServedId = DetermineServedNodeId(topologyNode.Id, existingNodeIds);
                    semantics.Served = derivedServedId;

                    template.Nodes.Add(new TemplateNode
                    {
                        Id = derivedServedId,
                        Kind = "expr",
                        Expr = $"MAX(0, {arrivalsId} - {errorsId})",
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["graph.hidden"] = "true"
                        }
                    });

                    existingNodeIds.Add(derivedServedId);
                }
                else
                {
                    semantics.Served = arrivalsId;
                }
            }
            else if (!hasErrors && !string.Equals(servedId, arrivalsId, StringComparison.OrdinalIgnoreCase))
            {
                throw new TemplateValidationException(
                    $"Topology node '{topologyNode.Id}' of kind sink must map semantics.served to the same series as semantics.arrivals.");
            }

            if (!hasErrors)
            {
                var errorsNodeId = DetermineErrorsNodeId(topologyNode.Id, existingNodeIds);
                semantics.Errors = errorsNodeId;

                template.Nodes.Add(new TemplateNode
                {
                    Id = errorsNodeId,
                    Kind = "const",
                    Values = Enumerable.Repeat(0d, template.Grid.Bins).ToArray(),
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["graph.hidden"] = "true"
                    }
                });

                existingNodeIds.Add(errorsNodeId);
            }
        }
    }

    private static string DetermineErrorsNodeId(string topologyNodeId, HashSet<string> existingNodeIds)
    {
        var baseId = ToSnakeCase(topologyNodeId);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = topologyNodeId;
        }

        var candidate = $"{baseId}_errors";
        var suffix = 1;
        while (existingNodeIds.Contains(candidate))
        {
            candidate = $"{baseId}_errors_{suffix++}";
        }

        return candidate;
    }

    private static string DetermineServedNodeId(string topologyNodeId, HashSet<string> existingNodeIds)
    {
        var baseId = ToSnakeCase(topologyNodeId);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = topologyNodeId;
        }

        var candidate = $"{baseId}_served";
        var suffix = 1;
        while (existingNodeIds.Contains(candidate))
        {
            candidate = $"{baseId}_served_{suffix++}";
        }

        return candidate;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length * 2);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (char.IsUpper(c))
            {
                if (i > 0 && chars.Count > 0 && chars[^1] != '_')
                {
                    chars.Add('_');
                }
                chars.Add(char.ToLowerInvariant(c));
            }
            else if (char.IsLetterOrDigit(c))
            {
                chars.Add(c);
            }
            else
            {
                chars.Add('_');
            }
        }

        var result = new string(chars.ToArray()).Trim('_');
        if (string.IsNullOrEmpty(result))
        {
            return value;
        }

        return result;
    }
}
