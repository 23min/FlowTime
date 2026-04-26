using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FlowTime.Contracts.Dtos;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Transforms validated templates into the unified post-substitution model
/// (<see cref="ModelDto"/>) per E-24 m-E24-02. Leaked-state root fields
/// (<c>window</c>, top-level <c>generator</c>, top-level <c>metadata</c>,
/// top-level <c>mode</c>) are dropped; per Q5/A4, <c>generator</c> and
/// <c>mode</c> survive inside <c>provenance</c> only. The scalar
/// <c>nodes[].initial</c> field (D-m-E24-02-01) is dead and not propagated.
/// The <c>nodes[].source</c> field (Q4) is dropped from emission and not
/// declared on <see cref="NodeDto"/>; <see cref="TemplateNode.Source"/> stays
/// as an authoring-only field.
/// </summary>
internal static class SimModelBuilder
{
    public static ModelDto Build(Template template, Dictionary<string, object?> parameterValues, string substitutedYaml)
    {
        ArgumentNullException.ThrowIfNull(template);

        var model = new ModelDto
        {
            SchemaVersion = template.SchemaVersion,
            Grid = BuildGrid(template.Grid, template.Window),
            Topology = BuildTopology(template.Topology),
            Classes = BuildClasses(template.Classes),
            Traffic = BuildTraffic(template.Traffic, template.Classes),
            Nodes = BuildNodes(template),
            Outputs = BuildOutputs(template.Outputs)
        };

        model.Provenance = BuildProvenance(template, parameterValues, substitutedYaml);
        return model;
    }

    private static GridDto BuildGrid(TemplateGrid grid, TemplateWindow window)
    {
        var start = !string.IsNullOrWhiteSpace(grid.Start) ? grid.Start : window.Start;
        return new GridDto
        {
            Bins = grid.Bins,
            BinSize = grid.BinSize,
            BinUnit = grid.BinUnit,
            Start = string.IsNullOrWhiteSpace(start) ? null : start
        };
    }

    private static TopologyDto BuildTopology(TemplateTopology topology)
    {
        var dto = new TopologyDto
        {
            Nodes = new List<TopologyNodeDto>(),
            Edges = new List<TopologyEdgeDto>(),
            Constraints = new List<TopologyConstraintDto>()
        };

        foreach (var node in topology.Nodes)
        {
            dto.Nodes.Add(new TopologyNodeDto
            {
                Id = node.Id,
                Kind = node.Kind,
                NodeRole = node.NodeRole,
                Group = node.Group,
                Constraints = node.Constraints is null ? null : new List<string>(node.Constraints),
                Semantics = BuildSemantics(node.Semantics),
                InitialCondition = node.InitialCondition is { QueueDepth: { } qd }
                    ? new TopologyInitialConditionDto { QueueDepth = qd }
                    : null,
                Ui = node.Ui == null ? null : new UiHintsDto { X = node.Ui.X, Y = node.Ui.Y },
                DispatchSchedule = CloneDispatchSchedule(node.DispatchSchedule)
            });
        }

        foreach (var edge in topology.Edges)
        {
            dto.Edges.Add(new TopologyEdgeDto
            {
                Id = edge.Id,
                From = edge.From,
                To = edge.To,
                Weight = edge.Weight,
                Type = edge.Type,
                Measure = edge.Measure,
                Multiplier = edge.Multiplier,
                Lag = edge.Lag
            });
        }

        if (topology.Constraints is not null)
        {
            foreach (var constraint in topology.Constraints)
            {
                dto.Constraints.Add(new TopologyConstraintDto
                {
                    Id = constraint.Id,
                    Semantics = constraint.Semantics == null
                        ? new ConstraintSemanticsDto()
                        : new ConstraintSemanticsDto
                        {
                            Arrivals = constraint.Semantics.Arrivals ?? string.Empty,
                            Served = constraint.Semantics.Served ?? string.Empty,
                            Errors = constraint.Semantics.Errors,
                            LatencyMinutes = constraint.Semantics.LatencyMinutes
                        }
                });
            }
        }

        return dto;
    }

    private static TopologySemanticsDto BuildSemantics(TemplateNodeSemantics semantics)
    {
        var dto = new TopologySemanticsDto
        {
            Arrivals = semantics.Arrivals ?? string.Empty,
            Served = semantics.Served ?? string.Empty,
            // Errors is nullable on TopologySemanticsDto — leave null when source is null
            // so OmitNull suppresses `errors: ''` for sink nodes that legitimately don't
            // declare errors. Wire shape: matches BEFORE m-E24-02 emission for these nodes.
            Errors = string.IsNullOrWhiteSpace(semantics.Errors) ? null : semantics.Errors,
            Capacity = semantics.Capacity,
            Parallelism = semantics.Parallelism,
            Attempts = semantics.Attempts,
            Failures = semantics.Failures,
            RetryEcho = semantics.RetryEcho,
            RetryKernel = semantics.RetryKernel?.ToArray(),
            ExternalDemand = semantics.ExternalDemand,
            ProcessingTimeMsSum = semantics.ProcessingTimeMsSum,
            ServedCount = semantics.ServedCount,
            Aliases = semantics.Aliases is null
                ? null
                : new Dictionary<string, string>(semantics.Aliases, StringComparer.OrdinalIgnoreCase)
        };

        if (!string.IsNullOrWhiteSpace(semantics.QueueDepth))
        {
            dto.QueueDepth = semantics.QueueDepth;
        }
        return dto;
    }

    private static List<ClassDto> BuildClasses(List<TemplateClass> classes) =>
        classes?.Select(c => new ClassDto
        {
            Id = c.Id,
            DisplayName = c.DisplayName,
            Description = c.Description
        }).ToList() ?? new List<ClassDto>();

    private static TrafficDto? BuildTraffic(TemplateTraffic? traffic, List<TemplateClass> classes)
    {
        if (traffic?.Arrivals == null || traffic.Arrivals.Count == 0)
        {
            return null;
        }

        var declaredClasses = new HashSet<string>(classes.Select(c => c.Id), StringComparer.Ordinal);
        var arrivals = new List<ArrivalDto>();

        foreach (var arrival in traffic.Arrivals)
        {
            var classId = arrival.ClassId;
            if (string.IsNullOrWhiteSpace(classId))
            {
                classId = declaredClasses.Count > 0 ? throw new TemplateValidationException($"Arrival targeting '{arrival.NodeId}' must declare classId because classes are defined.") : "*";
            }
            else if (declaredClasses.Count > 0 && !declaredClasses.Contains(classId))
            {
                throw new TemplateValidationException($"Class '{classId}' referenced by arrivals is not declared under classes.");
            }

            arrivals.Add(new ArrivalDto
            {
                NodeId = arrival.NodeId,
                ClassId = classId,
                Pattern = new ArrivalPatternDto
                {
                    Kind = arrival.Pattern?.Kind ?? string.Empty,
                    RatePerBin = arrival.Pattern?.RatePerBin,
                    Rate = arrival.Pattern?.Rate
                }
            });
        }

        return new TrafficDto { Arrivals = arrivals };
    }

    private static List<NodeDto> BuildNodes(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(template.Grid);

        var nodes = new List<NodeDto>(template.Nodes.Count);
        foreach (var node in template.Nodes)
        {
            var kind = node.Kind?.Trim().ToLowerInvariant();

            if (string.Equals(kind, "pmf", StringComparison.OrdinalIgnoreCase) &&
                node.Pmf != null)
            {
                var resolution = TemplateProfileResolver.TryResolve(node.Profile, template.Grid);
                if (resolution != null)
                {
                    nodes.Add(BuildProfiledConstNode(node, resolution, template.Grid));
                    continue;
                }
            }

            nodes.Add(BuildDefaultNode(node));
        }

        return nodes;
    }

    private static NodeDto BuildProfiledConstNode(TemplateNode node, ProfileResolution resolution, TemplateGrid grid)
    {
        if (grid.Bins != resolution.Weights.Length)
        {
            throw new InvalidOperationException($"Profile weights length ({resolution.Weights.Length}) must match grid.bins ({grid.Bins}).");
        }

        var expectedValue = ComputeExpectedValue(node.Pmf!);
        var values = new double[resolution.Weights.Length];
        for (int i = 0; i < resolution.Weights.Length; i++)
        {
            values[i] = expectedValue * resolution.Weights[i];
        }

        var profileMetadata = BuildProfileMetadata(node, resolution, expectedValue);
        var mergedMetadata = MergeMetadata(CloneMetadata(node.Metadata), profileMetadata);

        return new NodeDto
        {
            Id = node.Id,
            Kind = "const",
            Values = values,
            Metadata = mergedMetadata
        };
    }

    private static double ComputeExpectedValue(PmfSpec pmf)
    {
        double sum = 0;
        for (int i = 0; i < pmf.Values.Length; i++)
        {
            sum += pmf.Values[i] * pmf.Probabilities[i];
        }
        return sum;
    }

    private static Dictionary<string, string> BuildProfileMetadata(TemplateNode node, ProfileResolution resolution, double expectedValue)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["origin.kind"] = "pmf",
            ["profile.kind"] = resolution.Kind,
            ["pmf.expected"] = expectedValue.ToString("G17", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(resolution.Name))
        {
            metadata["profile.name"] = resolution.Name!;
        }

        return metadata;
    }

    private static Dictionary<string, string>? CloneMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string>? MergeMetadata(Dictionary<string, string>? primary, Dictionary<string, string>? secondary)
    {
        if ((primary == null || primary.Count == 0) && (secondary == null || secondary.Count == 0))
        {
            return null;
        }

        var merged = primary != null
            ? new Dictionary<string, string>(primary, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (secondary != null)
        {
            foreach (var (key, value) in secondary)
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    private static NodeDto BuildDefaultNode(TemplateNode node)
    {
        var kind = node.Kind?.Trim().ToLowerInvariant();
        return new NodeDto
        {
            Id = node.Id,
            Kind = node.Kind ?? string.Empty,
            Values = kind switch
            {
                "pmf" => null,
                "expr" => null,
                _ => node.Values?.ToArray()
            },
            Expr = node.Expr,
            Pmf = node.Pmf == null
                ? null
                : new PmfDto
                {
                    Values = node.Pmf.Values?.ToArray() ?? Array.Empty<double>(),
                    Probabilities = node.Pmf.Probabilities?.ToArray() ?? Array.Empty<double>()
                },
            Inflow = kind == "servicewithbuffer" ? node.Inflow : null,
            Outflow = kind == "servicewithbuffer" ? node.Outflow : null,
            Loss = kind == "servicewithbuffer" ? node.Loss : null,
            Metadata = CloneMetadata(node.Metadata),
            Inputs = kind == "router" ? CloneInputs(node.Inputs) : null,
            Routes = kind == "router" ? CloneRoutes(node.Routes) : null,
            DispatchSchedule = kind == "servicewithbuffer" ? CloneDispatchSchedule(node.DispatchSchedule) : null
        };
    }

    private static List<OutputDto> BuildOutputs(List<TemplateOutput> outputs)
    {
        return outputs.Select(output => new OutputDto
        {
            Series = output.Series,
            Exclude = output.Exclude == null ? null : new List<string>(output.Exclude),
            As = output.As
        }).ToList();
    }

    private static RouterInputsDto? CloneInputs(TemplateRouterInputs? inputs)
    {
        if (inputs is null)
        {
            return null;
        }

        return new RouterInputsDto
        {
            Queue = inputs.Queue
        };
    }

    private static List<RouterRouteDto>? CloneRoutes(List<TemplateRouterRoute>? routes)
    {
        return routes?.Select(route => new RouterRouteDto
        {
            Target = route.Target,
            Classes = route.Classes?.ToArray(),
            Weight = route.Weight
        }).ToList();
    }

    private static DispatchScheduleDto? CloneDispatchSchedule(TemplateDispatchSchedule? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        return new DispatchScheduleDto
        {
            Kind = schedule.Kind,
            PeriodBins = schedule.PeriodBins,
            PhaseOffset = schedule.PhaseOffset,
            CapacitySeries = schedule.CapacitySeries
        };
    }

    private static ProvenanceDto BuildProvenance(Template template, Dictionary<string, object?> parameterValues, string substitutedYaml)
    {
        var now = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var generator = ResolveGeneratorIdentifier(template);
        var modelId = ComputeModelId(substitutedYaml);
        var templateVersion = !string.IsNullOrWhiteSpace(template.Provenance?.TemplateVersion)
            ? template.Provenance!.TemplateVersion
            : template.Metadata.Version;

        // Per D-m-E24-02-03: Parameters is nullable on ProvenanceDto so empty
        // sets serialize as YAML omission via OmitNull. Materialize the
        // dictionary only when we actually have values to record.
        Dictionary<string, object?>? parameters = null;
        if (template.Provenance?.Parameters != null && template.Provenance.Parameters.Count > 0)
        {
            parameters = new Dictionary<string, object?>(template.Provenance.Parameters, StringComparer.Ordinal);
        }

        if (parameterValues.Count > 0)
        {
            parameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in parameterValues)
            {
                parameters[kvp.Key] = kvp.Value;
            }
        }

        return new ProvenanceDto
        {
            Generator = generator,
            GeneratedAt = template.Provenance?.GeneratedAt ?? now,
            TemplateId = template.Metadata.Id,
            TemplateVersion = templateVersion,
            Mode = template.Provenance?.Mode ?? template.Mode.ToSerializedValue(),
            ModelId = template.Provenance?.ModelId ?? modelId,
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
