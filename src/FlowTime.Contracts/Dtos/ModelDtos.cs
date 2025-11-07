using YamlDotNet.Serialization;

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
    public TopologyDto? Topology { get; set; }
}

/// <summary>
/// Grid definition specifying time bins and duration
/// </summary>
public sealed class GridDto
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public string? StartTimeUtc { get; set; }

    [YamlMember(Alias = "start", ApplyNamingConventions = false)]
    public string? LegacyStart
    {
        get => StartTimeUtc;
        set => StartTimeUtc = value;
    }

    public bool ShouldSerializeLegacyStart() => false;
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
    public PmfDto? Pmf { get; set; }
}

public sealed class PmfDto
{
    public double[] Values { get; set; } = Array.Empty<double>();
    public double[] Probabilities { get; set; } = Array.Empty<double>();
}

/// <summary>
/// Output definition for CSV generation
/// </summary>
public sealed class OutputDto
{
    public string Series { get; set; } = "";
    public string As { get; set; } = "out.csv";
}

public sealed class TopologyDto
{
    public List<TopologyNodeDto> Nodes { get; set; } = new();
    public List<TopologyEdgeDto> Edges { get; set; } = new();
}

public sealed class TopologyNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "service";
    public string? Group { get; set; }
    public UiHintsDto? Ui { get; set; }
    public TopologySemanticsDto Semantics { get; set; } = new();
    public TopologyInitialConditionDto? InitialCondition { get; set; }
}

public sealed class TopologySemanticsDto
{
    public string Arrivals { get; set; } = string.Empty;
    public string Served { get; set; } = string.Empty;
    public string Errors { get; set; } = string.Empty;
    public string? Attempts { get; set; }
    public string? Failures { get; set; }
    public string? RetryEcho { get; set; }
    public double[]? RetryKernel { get; set; }
    public string? ExternalDemand { get; set; }
    public string? Queue { get; set; }
    public string? Capacity { get; set; }
    public string? ProcessingTimeMsSum { get; set; }
    public string? ServedCount { get; set; }
    public double? SlaMin { get; set; }
}

public sealed class TopologyInitialConditionDto
{
    public double QueueDepth { get; set; }
}

public sealed class TopologyEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Type { get; set; }
    public string? Measure { get; set; }
    public double? Multiplier { get; set; }
    public int? Lag { get; set; }
}

public sealed class UiHintsDto
{
    public double? X { get; set; }
    public double? Y { get; set; }
}
