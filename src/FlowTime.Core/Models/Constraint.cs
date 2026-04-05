namespace FlowTime.Core.Models;

public sealed record Constraint
{
    public required string Id { get; init; }
    public required ConstraintSemantics Semantics { get; init; }
}

public sealed record ConstraintSemantics
{
    public required string Arrivals { get; init; }
    public CompiledSeriesReference? ArrivalsRef { get; init; }
    public required string Served { get; init; }
    public CompiledSeriesReference? ServedRef { get; init; }
    public string? Errors { get; init; }
    public CompiledSeriesReference? ErrorsRef { get; init; }
    public string? LatencyMinutes { get; init; }
    public CompiledSeriesReference? LatencyMinutesRef { get; init; }
}
