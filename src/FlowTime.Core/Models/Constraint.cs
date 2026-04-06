namespace FlowTime.Core.Models;

public sealed record Constraint
{
    public required string Id { get; init; }
    public required ConstraintSemantics Semantics { get; init; }
}

public sealed record ConstraintSemantics
{
    public required CompiledSeriesReference Arrivals { get; init; }
    public required CompiledSeriesReference Served { get; init; }
    public CompiledSeriesReference? Errors { get; init; }
    public CompiledSeriesReference? LatencyMinutes { get; init; }
}
