namespace FlowTime.Contracts.Dtos;

/// <summary>
/// RNG configuration for FlowTime-Sim (ignored by FlowTime Engine)
/// </summary>
public sealed class RngDto
{
    public string Kind { get; set; } = "pcg32";
    public int? Seed { get; set; }
}

/// <summary>
/// Root model definition for YAML deserialization
/// </summary>
public sealed class ModelDto
{
    public int? SchemaVersion { get; set; }
    public GridDto Grid { get; set; } = new();
    public List<NodeDto> Nodes { get; set; } = new();
    public List<OutputDto> Outputs { get; set; } = new();
    public RngDto? Rng { get; set; }
}

/// <summary>
/// Grid definition specifying time bins and duration
/// </summary>
public sealed class GridDto
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
}

/// <summary>
/// Node definition for different node types (const, expr, pmf, etc.)
/// </summary>
public sealed class NodeDto
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
    public Dictionary<string, double>? Pmf { get; set; }
}

/// <summary>
/// Output definition for CSV generation
/// </summary>
public sealed class OutputDto
{
    public string Series { get; set; } = "";
    public string As { get; set; } = "out.csv";
}
