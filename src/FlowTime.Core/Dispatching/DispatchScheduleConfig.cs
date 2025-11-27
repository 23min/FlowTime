using FlowTime.Core.Nodes;

namespace FlowTime.Core.Dispatching;

public sealed class DispatchScheduleConfig
{
    public DispatchScheduleConfig(int periodBins, int phaseOffset, NodeId? capacitySeriesId)
    {
        if (periodBins <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodBins));
        }

        PeriodBins = periodBins;
        PhaseOffset = DispatchScheduleProcessor.NormalizePhase(phaseOffset, periodBins);
        CapacitySeriesId = capacitySeriesId;
    }

    public int PeriodBins { get; }
    public int PhaseOffset { get; }
    public NodeId? CapacitySeriesId { get; }
}
