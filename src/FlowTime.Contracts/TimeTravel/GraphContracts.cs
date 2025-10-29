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
    public string? Queue { get; init; }
    public string? Capacity { get; init; }
    public string? Series { get; init; }
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
}
