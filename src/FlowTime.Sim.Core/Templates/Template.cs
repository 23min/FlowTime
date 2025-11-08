using System;
using System.Collections.Generic;
using FlowTime.Sim.Core.Templates.Exceptions;
using YamlDotNet.Serialization;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Represents a time-travel simulation template with metadata, parameters, grid, topology, nodes, and outputs.
/// Templates are authored in YAML and converted to Engine-compatible models via the generation pipeline.
/// </summary>
public class Template
{
    [YamlMember(Alias = "schemaVersion", ApplyNamingConventions = false)]
    public int SchemaVersion { get; set; } = 1;
    public string Generator { get; set; } = "flowtime-sim";

    [YamlIgnore]
    public TemplateMode Mode { get; set; } = TemplateMode.Simulation;

    [YamlMember(Alias = "mode")]
    public string ModeValue
    {
        get => Mode.ToSerializedValue();
        set => Mode = TemplateModeExtensions.Parse(value);
    }

    public TemplateMetadata Metadata { get; set; } = new();
    public TemplateWindow Window { get; set; } = new();
    public List<TemplateParameter> Parameters { get; set; } = new();
    public TemplateGrid Grid { get; set; } = new();
    public TemplateTopology Topology { get; set; } = new();
    public List<TemplateNode> Nodes { get; set; } = new();
    public List<TemplateOutput> Outputs { get; set; } = new();
    public TemplateRng? Rng { get; set; } = null;
    public TemplateProvenance? Provenance { get; set; }
}

/// <summary>
/// Template metadata including ID, title, description, and tags.
/// </summary>
public class TemplateMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? CaptureKey { get; set; }
}

/// <summary>
/// Describes the simulation window (start timestamp, timezone).
/// </summary>
public class TemplateWindow
{
    public string Start { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
}

/// <summary>
/// Template parameter definition with type validation and constraints.
/// </summary>
public class TemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Default { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? ArrayOf { get; set; }

    // Accept alternative YAML keys used in some templates
    // Map 'minimum'/'maximum' onto canonical Min/Max
    [YamlMember(Alias = "minimum", ApplyNamingConventions = false)]
    public double? Minimum { get => Min; set => Min = value; }

    [YamlMember(Alias = "maximum", ApplyNamingConventions = false)]
    public double? Maximum { get => Max; set => Max = value; }
}

/// <summary>
/// Template grid configuration for time-based simulation bins.
/// </summary>
public class TemplateGrid
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = string.Empty;
    // Optional start timestamp (ISO 8601); not required by validator but supported in schema
    public string? Start { get; set; }
}

/// <summary>
/// Describes the topology portion of a template (nodes and edges).
/// </summary>
public class TemplateTopology
{
    public List<TemplateTopologyNode> Nodes { get; set; } = new();
    public List<TemplateTopologyEdge> Edges { get; set; } = new();
}

/// <summary>
/// Topology node enriched with semantics, group, and initial conditions.
/// </summary>
public class TemplateTopologyNode
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Group { get; set; }
    public TemplateNodeSemantics Semantics { get; set; } = new();
    public TemplateInitialCondition? InitialCondition { get; set; }
    public TemplateUiHint? Ui { get; set; }
}

/// <summary>
/// Semantic mapping for topology nodes.
/// </summary>
public class TemplateNodeSemantics
{
    public string? Arrivals { get; set; }
    public string? Served { get; set; }
    public string? Errors { get; set; }
    public string? Queue { get; set; }
    public string? Capacity { get; set; }
    public string? Attempts { get; set; }
    public string? Failures { get; set; }
    public string? RetryEcho { get; set; }
    public double[]? RetryKernel { get; set; }

    [YamlMember(Alias = "external_demand", ApplyNamingConventions = false)]
    public string? ExternalDemand { get; set; }

    public string? ProcessingTimeMsSum { get; set; }
    public string? ServedCount { get; set; }
}

/// <summary>
/// Optional initial conditions associated with a topology node.
/// </summary>
public class TemplateInitialCondition
{
    public double? QueueDepth { get; set; }
}

/// <summary>
/// Optional UI hints for topology visualization.
/// </summary>
public class TemplateUiHint
{
    public double? X { get; set; }
    public double? Y { get; set; }
}

/// <summary>
/// Directed edge describing connectivity between topology nodes.
/// </summary>
public class TemplateTopologyEdge
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public string? Type { get; set; }
    public string? Measure { get; set; }
    public double? Multiplier { get; set; }
    public int? Lag { get; set; }
}

/// <summary>
/// Template node representing computation elements (const, pmf, expr).
/// </summary>
public class TemplateNode
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    
    // For const nodes
    public double[]? Values { get; set; }

    public string? Source { get; set; }
    
    // For pmf nodes
    public PmfSpec? Pmf { get; set; }
    
    // For expr nodes
    private string? expr;

    [YamlMember(Alias = "expr")]
    public string? Expr
    {
        get => expr;
        set => expr = value;
    }

    [YamlMember(Alias = "expression")]
    public string? LegacyExpression
    {
        get => expr;
        set => expr = value;
    }

    public bool ShouldSerializeLegacyExpression() => false;

    public List<string>? Dependencies { get; set; }
    public double? Initial { get; set; }
}

/// <summary>
/// PMF (Probability Mass Function) specification for stochastic nodes.
/// </summary>
public class PmfSpec
{
    public double[] Values { get; set; } = Array.Empty<double>();
    public double[] Probabilities { get; set; } = Array.Empty<double>();
}

/// <summary>
/// Template output definition linking to node sources.
/// </summary>
public class TemplateOutput
{
    public string? Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Series { get; set; } = "*";
    public List<string>? Exclude { get; set; }
    public string? As { get; set; }

    [YamlMember(Alias = "source")]
    public string? LegacySource
    {
        get => Series;
        set => Series = value ?? "*";
    }

    public bool ShouldSerializeLegacySource() => false;

    [YamlMember(Alias = "filename")]
    public string? LegacyFilename
    {
        get => As;
        set => As = value;
    }

    public bool ShouldSerializeLegacyFilename() => false;
}

/// <summary>
/// RNG (Random Number Generator) configuration for deterministic simulation.
/// </summary>
public class TemplateRng
{
    public string Kind { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
}

/// <summary>
/// Provenance block carried alongside generated models.
/// </summary>
public class TemplateProvenance
{
    public string Source { get; set; } = "flowtime-sim";
    public string Generator { get; set; } = "flowtime-sim";
    public string? GeneratedAt { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public string? ModelId { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

/// <summary>
/// Supported template validation/generation modes.
/// </summary>
public enum TemplateMode
{
    Simulation,
    Telemetry
}

/// <summary>
/// Helpers for template mode conversions.
/// </summary>
public static class TemplateModeExtensions
{
    public static TemplateMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TemplateMode.Simulation;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "simulation" => TemplateMode.Simulation,
            "telemetry" => TemplateMode.Telemetry,
            _ => throw new TemplateValidationException($"Unsupported template mode '{value}'. Expected 'simulation' or 'telemetry'.")
        };
    }

    public static string ToSerializedValue(this TemplateMode mode) =>
        mode switch
        {
            TemplateMode.Simulation => "simulation",
            TemplateMode.Telemetry => "telemetry",
            _ => "simulation"
        };

    public static string ToYamlValue(this TemplateMode mode) => mode.ToSerializedValue();
}
