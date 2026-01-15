namespace FlowTime.Contracts.TimeTravel;

public sealed class DispatchScheduleDescriptor
{
    public string Kind { get; init; } = "time-based";
    public int PeriodBins { get; init; }
    public int PhaseOffset { get; init; }
    public string? CapacitySeries { get; init; }
}
