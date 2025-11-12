using System;
using System.Collections.Generic;

namespace FlowTime.Contracts.TimeTravel;

public sealed class GraphResponse
{
    public IReadOnlyList<GraphNode> Nodes { get; init; } = Array.Empty<GraphNode>();
    public IReadOnlyList<GraphEdge> Edges { get; init; } = Array.Empty<GraphEdge>();
}

public sealed class GraphNode
{
    public string Id { get; init; } = string.Empty;
    public string? Kind { get; init; }
    public GraphNodeSemantics Semantics { get; init; } = new();
    public GraphNodeUi? Ui { get; init; }
}

public sealed class GraphNodeSemantics
{
    public string Arrivals { get; init; } = string.Empty;
    public string Served { get; init; } = string.Empty;
    public string Errors { get; init; } = string.Empty;
    public string? Attempts { get; init; }
    public string? Failures { get; init; }
    public string? RetryEcho { get; init; }
    public string? Queue { get; init; }
    public string? Capacity { get; init; }
    public string? Series { get; init; }
    public string? Expression { get; init; }
    public GraphNodeDistribution? Distribution { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<double>? InlineValues { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}

public sealed class GraphNodeUi
{
    public double? X { get; init; }
    public double? Y { get; init; }
    public int? Layer { get; init; }
    public int? Order { get; init; }
}

public sealed class GraphEdge
{
    public string Id { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public double Weight { get; init; }
    public string? EdgeType { get; init; }
    public string? Field { get; init; }
    public double? Multiplier { get; init; }
    public int? Lag { get; init; }
}

public sealed class GraphNodeDistribution
{
    public IReadOnlyList<double> Values { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> Probabilities { get; init; } = Array.Empty<double>();
}
