namespace FlowTime.Core.Models;

public sealed record Edge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public double Weight { get; init; } = 1.0;
    public string? Id { get; init; }
    public string? EdgeType { get; init; }
    public string? Field { get; init; }
    public double? Multiplier { get; init; }
    public int? Lag { get; init; }
}
