namespace FlowTime.Core.Models;

public sealed record NodeSemantics
{
    public required string Arrivals { get; init; }
    public required string Served { get; init; }
    public required string Errors { get; init; }
    public string? ExternalDemand { get; init; }
    public string? QueueDepth { get; init; }
}
