namespace FlowTime.Core.Models;

public sealed record Edge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public double Weight { get; init; } = 1.0;
}
