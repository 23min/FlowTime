namespace FlowTime.Core.Models;

public sealed record Node
{
    public required string Id { get; init; }
    public required NodeSemantics Semantics { get; init; }
    public InitialCondition? InitialCondition { get; init; }
}
