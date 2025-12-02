using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

internal static class ServiceWithBufferNodeSynthesizer
{
    public static void Apply(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Topology?.Nodes is null || template.Topology.Nodes.Count == 0)
        {
            return;
        }

        var existingNodeIds = new HashSet<string>(
            template.Nodes.Select(n => n.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var topologyNode in template.Topology.Nodes)
        {
            if (!string.Equals(topologyNode.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var semantics = topologyNode.Semantics
                            ?? throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.");

            var requestedQueueId = semantics.QueueDepth?.Trim();
            var needsSynthesis =
                string.IsNullOrWhiteSpace(requestedQueueId) ||
                requestedQueueId.Equals("self", StringComparison.OrdinalIgnoreCase) ||
                !existingNodeIds.Contains(requestedQueueId);

            if (!needsSynthesis)
            {
                continue;
            }

            var inflow = semantics.Arrivals;
            if (string.IsNullOrWhiteSpace(inflow))
            {
                throw new TemplateValidationException($"Topology node '{topologyNode.Id}' must define semantics.arrivals to synthesize ServiceWithBuffer behavior.");
            }

            var outflow = ResolveOutflow(topologyNode, semantics);
            if (string.IsNullOrWhiteSpace(outflow))
            {
                throw new TemplateValidationException(
                    $"Topology node '{topologyNode.Id}' must define semantics.capacity or semantics.served to synthesize ServiceWithBuffer behavior.");
            }

            var queueNodeId = DetermineQueueNodeId(topologyNode.Id, requestedQueueId, existingNodeIds);
            semantics.QueueDepth = queueNodeId;

            template.Nodes.Add(new TemplateNode
            {
                Id = queueNodeId,
                Kind = "serviceWithBuffer",
                Inflow = inflow,
                Outflow = outflow,
                Loss = string.IsNullOrWhiteSpace(semantics.Errors) ? null : semantics.Errors,
                DispatchSchedule = CloneSchedule(topologyNode.DispatchSchedule)
            });

            existingNodeIds.Add(queueNodeId);
        }
    }

    private static string DetermineQueueNodeId(string topologyNodeId, string? requestedId, HashSet<string> existingNodeIds)
    {
        if (!string.IsNullOrWhiteSpace(requestedId) && !requestedId.Equals("self", StringComparison.OrdinalIgnoreCase))
        {
            return requestedId;
        }

        var baseId = ToSnakeCase(topologyNodeId);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = topologyNodeId;
        }

        var candidate = $"{baseId}_queue";
        var suffix = 1;
        while (existingNodeIds.Contains(candidate))
        {
            candidate = $"{baseId}_queue_{suffix++}";
        }

        return candidate;
    }

    private static string? ResolveOutflow(TemplateTopologyNode topologyNode, TemplateNodeSemantics semantics)
    {
        if (!string.IsNullOrWhiteSpace(semantics.Capacity))
        {
            return semantics.Capacity.Trim();
        }

        return string.IsNullOrWhiteSpace(semantics.Served)
            ? null
            : semantics.Served.Trim();
    }

    private static TemplateDispatchSchedule? CloneSchedule(TemplateDispatchSchedule? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        return new TemplateDispatchSchedule
        {
            Kind = schedule.Kind,
            PeriodBins = schedule.PeriodBins,
            PhaseOffset = schedule.PhaseOffset,
            CapacitySeries = schedule.CapacitySeries
        };
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length * 2);
        for (int i = 0; i < value.Length; i++)
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
