using System.Globalization;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Logging;

namespace FlowTime.Core.Compiler;

public static class ModelCompiler
{
    private static readonly HashSet<string> queueLikeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "servicewithbuffer",
        "queue",
        "dlq"
    };

    public static ModelDefinition Compile(ModelDefinition model, ILogger? logger = null)
    {
        if (model.Topology?.Nodes is not { Count: > 0 })
        {
            return model;
        }

        var nodes = model.Nodes.ToList();
        var topology = CloneTopology(model.Topology);
        var existingNodeIds = new HashSet<string>(nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var topoNode in topology.Nodes)
        {
            var semantics = topoNode.Semantics ?? new TopologyNodeSemanticsDefinition();
            topoNode.Semantics = semantics;
            var queueReference = SemanticReferenceResolver.ParseOptionalSeriesReference(semantics.QueueDepth);
            var retryEchoReference = SemanticReferenceResolver.ParseOptionalSeriesReference(semantics.RetryEcho);

            if (IsQueueLikeKind(topoNode.Kind))
            {
                if (queueReference?.Kind != CompiledSeriesReferenceKind.File)
                {
                    var requestedQueueId = queueReference?.ResolveProducerId(topoNode.Id);
                    var needsQueueNode = queueReference is null ||
                                         queueReference.Kind == CompiledSeriesReferenceKind.Self ||
                                         string.IsNullOrWhiteSpace(requestedQueueId) ||
                                         !existingNodeIds.Contains(requestedQueueId);

                    if (needsQueueNode)
                    {
                        var inflow = RequireSeries(semantics.Arrivals, topoNode.Id, "semantics.arrivals");
                        var outflow = ResolveQueueOutflow(semantics, topoNode.Id);
                        var loss = string.IsNullOrWhiteSpace(semantics.Errors) ? null : semantics.Errors.Trim();
                        var queueNodeId = DetermineQueueNodeId(topoNode.Id, queueReference, existingNodeIds);

                        semantics.QueueDepth = queueNodeId;
                        nodes.Add(new NodeDefinition
                        {
                            Id = queueNodeId,
                            Kind = "serviceWithBuffer",
                            Inflow = inflow,
                            Outflow = outflow,
                            Loss = loss,
                            DispatchSchedule = CloneSchedule(topoNode.DispatchSchedule),
                            WipLimit = topoNode.WipLimit,
                            WipOverflow = topoNode.WipOverflow,
                            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["graph.hidden"] = "true",
                                ["series.origin"] = "derived"
                            }
                        });
                        existingNodeIds.Add(queueNodeId);
                        changed = true;
                    }
                }
            }

            var retryEchoSeries = retryEchoReference?.ResolveProducerId(topoNode.Id);
            if (!string.IsNullOrWhiteSpace(retryEchoSeries) &&
                retryEchoReference?.Kind != CompiledSeriesReferenceKind.File &&
                !existingNodeIds.Contains(retryEchoSeries))
            {
                var failuresSeries = !string.IsNullOrWhiteSpace(semantics.Failures)
                    ? semantics.Failures!.Trim()
                    : !string.IsNullOrWhiteSpace(semantics.Errors)
                        ? semantics.Errors!.Trim()
                        : null;

                if (!string.IsNullOrWhiteSpace(failuresSeries))
                {
                    var policyResult = RetryKernelPolicy.Apply(semantics.RetryKernel);
                    if (policyResult.HasMessages)
                    {
                        foreach (var message in policyResult.Messages)
                        {
                            logger?.LogDebug("Retry kernel policy for '{SeriesId}': {Message}", retryEchoSeries, message);
                        }
                    }

                    if (policyResult.Kernel.Length > 0)
                    {
                        var kernelLiteral = FormatKernelLiteral(policyResult.Kernel);
                        nodes.Add(new NodeDefinition
                        {
                            Id = retryEchoSeries,
                            Kind = "expr",
                            Expr = $"CONV({failuresSeries}, {kernelLiteral})",
                            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["graph.hidden"] = "true"
                            }
                        });
                        existingNodeIds.Add(retryEchoSeries);
                        changed = true;
                    }
                }
            }
        }

        if (!changed)
        {
            return model;
        }

        return new ModelDefinition
        {
            SchemaVersion = model.SchemaVersion,
            Grid = model.Grid,
            Classes = model.Classes.ToList(),
            Traffic = model.Traffic,
            Nodes = nodes,
            Outputs = model.Outputs.ToList(),
            Topology = topology
        };
    }

    private static bool IsQueueLikeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return queueLikeKinds.Contains(kind.Trim());
    }

    private static string RequireSeries(string? value, string nodeId, string field)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new InvalidOperationException($"Topology node '{nodeId}' must define {field} to synthesize queue depth.");
    }

    private static string ResolveQueueOutflow(TopologyNodeSemanticsDefinition semantics, string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(semantics.Served))
        {
            return semantics.Served.Trim();
        }

        if (!string.IsNullOrWhiteSpace(semantics.Capacity))
        {
            return semantics.Capacity.Trim();
        }

        throw new InvalidOperationException(
            $"Topology node '{nodeId}' must define semantics.served (or capacity) to synthesize queue depth.");
    }

    private static string DetermineQueueNodeId(
        string topologyNodeId,
        CompiledSeriesReference? requestedReference,
        HashSet<string> existingNodeIds)
    {
        if (requestedReference is { Kind: CompiledSeriesReferenceKind.Node })
        {
            var requestedId = requestedReference.ResolveProducerId(topologyNodeId);
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                return requestedId;
            }
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

    private static string FormatKernelLiteral(double[] kernel)
    {
        var parts = kernel.Select(v => v.ToString("G", CultureInfo.InvariantCulture));
        return $"[{string.Join(", ", parts)}]";
    }

    private static DispatchScheduleDefinition? CloneSchedule(DispatchScheduleDefinition? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        return new DispatchScheduleDefinition
        {
            Kind = schedule.Kind,
            PeriodBins = schedule.PeriodBins,
            PhaseOffset = schedule.PhaseOffset,
            CapacitySeries = schedule.CapacitySeries
        };
    }

    private static TopologyDefinition CloneTopology(TopologyDefinition source)
    {
        return new TopologyDefinition
        {
            Nodes = source.Nodes.Select(CloneTopologyNode).ToList(),
            Edges = source.Edges.Select(CloneTopologyEdge).ToList()
        };
    }

    private static TopologyNodeDefinition CloneTopologyNode(TopologyNodeDefinition source)
    {
        return new TopologyNodeDefinition
        {
            Id = source.Id,
            Kind = source.Kind,
            NodeRole = source.NodeRole,
            Group = source.Group,
            Ui = source.Ui is null ? null : new UiHintsDefinition { X = source.Ui.X, Y = source.Ui.Y },
            Constraints = source.Constraints is null ? null : new List<string>(source.Constraints),
            Semantics = CloneSemantics(source.Semantics),
            InitialCondition = source.InitialCondition is null
                ? null
                : new InitialConditionDefinition { QueueDepth = source.InitialCondition.QueueDepth },
            DispatchSchedule = CloneSchedule(source.DispatchSchedule),
            WipLimit = source.WipLimit,
            WipOverflow = source.WipOverflow
        };
    }

    private static TopologyNodeSemanticsDefinition CloneSemantics(TopologyNodeSemanticsDefinition source)
    {
        return new TopologyNodeSemanticsDefinition
        {
            Arrivals = source.Arrivals,
            Served = source.Served,
            Errors = source.Errors,
            Attempts = source.Attempts,
            Failures = source.Failures,
            ExhaustedFailures = source.ExhaustedFailures,
            RetryEcho = source.RetryEcho,
            RetryBudgetRemaining = source.RetryBudgetRemaining,
            RetryKernel = source.RetryKernel?.ToArray(),
            ExternalDemand = source.ExternalDemand,
            QueueDepth = source.QueueDepth,
            Capacity = source.Capacity,
            Parallelism = source.Parallelism,
            ProcessingTimeMsSum = source.ProcessingTimeMsSum,
            ServedCount = source.ServedCount,
            SlaMin = source.SlaMin,
            MaxAttempts = source.MaxAttempts,
            BackoffStrategy = source.BackoffStrategy,
            ExhaustedPolicy = source.ExhaustedPolicy,
            Aliases = source.Aliases is null
                ? null
                : new Dictionary<string, string>(source.Aliases, StringComparer.OrdinalIgnoreCase),
            Metadata = source.Metadata is null
                ? null
                : new Dictionary<string, string>(source.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static TopologyEdgeDefinition CloneTopologyEdge(TopologyEdgeDefinition source)
    {
        return new TopologyEdgeDefinition
        {
            Source = source.Source,
            Target = source.Target,
            Weight = source.Weight,
            Id = source.Id,
            Type = source.Type,
            Measure = source.Measure,
            Multiplier = source.Multiplier,
            Lag = source.Lag
        };
    }
}
