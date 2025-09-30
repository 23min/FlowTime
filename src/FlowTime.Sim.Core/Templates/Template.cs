namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Represents a template with metadata, parameters, grid, nodes, and outputs.
/// Templates are authored in YAML and converted to models for Engine execution.
/// </summary>
public class Template
{
    public TemplateMetadata Metadata { get; set; } = new();
    public List<TemplateParameter> Parameters { get; set; } = new();
    public TemplateGrid Grid { get; set; } = new();
    public List<TemplateNode> Nodes { get; set; } = new();
    public List<TemplateOutput> Outputs { get; set; } = new();
    public TemplateRng? Rng { get; set; } = null;
}

/// <summary>
/// Template metadata including ID, title, description, and tags.
/// </summary>
public class TemplateMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
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
}

/// <summary>
/// Template grid configuration for time-based simulation bins.
/// </summary>
public class TemplateGrid
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = string.Empty;
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
    
    // For pmf nodes
    public PmfSpec? Pmf { get; set; }
    
    // For expr nodes
    public string? Expression { get; set; }
    public string[]? Dependencies { get; set; }
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
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// RNG (Random Number Generator) configuration for deterministic simulation.
/// </summary>
public class TemplateRng
{
    public string Kind { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
}