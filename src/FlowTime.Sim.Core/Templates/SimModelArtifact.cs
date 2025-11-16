using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Represents the generated FlowTime.Sim model artifact aligned with the KISS time-travel schema.
/// </summary>
public class SimModelArtifact
{
    [YamlMember(Alias = "schemaVersion", ApplyNamingConventions = false)]
    public int SchemaVersion { get; set; } = 1;

    public string Generator { get; set; } = "flowtime-sim";

    public string Mode { get; set; } = TemplateMode.Simulation.ToSerializedValue();

    public TemplateMetadata Metadata { get; set; } = new();

    public TemplateWindow Window { get; set; } = new();

    public TemplateGrid Grid { get; set; } = new();

    public TemplateTopology Topology { get; set; } = new();

    public List<SimNode> Nodes { get; set; } = new();

    public List<SimOutput> Outputs { get; set; } = new();

    public SimProvenance Provenance { get; set; } = new();
}

/// <summary>
/// Node definition in the generated model.
/// </summary>
public class SimNode
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)]
    public double[]? Values { get; set; }

    [YamlMember(Alias = "expr")]
    public string? Expr { get; set; }

    public string? Source { get; set; }

    public PmfSpec? Pmf { get; set; }

    public double? Initial { get; set; }

    // For backlog nodes
    public string? Inflow { get; set; }
    public string? Outflow { get; set; }
    public string? Loss { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public bool ShouldSerializeValues() => Values is { Length: > 0 };
}

/// <summary>
/// Output declaration in the generated model.
/// </summary>
public class SimOutput
{
    public string Series { get; set; } = "*";
    public List<string>? Exclude { get; set; }
    public string? As { get; set; }
}

/// <summary>
/// Provenance block embedded in generated models.
/// </summary>
public class SimProvenance
{
    public string Source { get; set; } = "flowtime-sim";
    public string Generator { get; set; } = "flowtime-sim";
    public string GeneratedAt { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public string Mode { get; set; } = TemplateMode.Simulation.ToSerializedValue();
    public string ModelId { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, object?> Parameters { get; set; } = new();
}
