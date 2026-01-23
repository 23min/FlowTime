namespace FlowTime.Core.Models;

public sealed record Node
{
    public required string Id { get; init; }
    public string Kind { get; init; } = "service";
    public string? NodeRole { get; init; }
    public string? Group { get; init; }
    public UiHints? Ui { get; init; }
    public IReadOnlyList<string>? Constraints { get; init; }
    public required NodeSemantics Semantics { get; init; }
    public InitialCondition? InitialCondition { get; init; }
    public DispatchScheduleDefinition? DispatchSchedule { get; init; }
}
