using FlowTime.Core.Dispatching;
using Xunit;

namespace FlowTime.Core.Tests.Aggregation;

public class ScheduledDispatchTests
{
    [Fact]
    public void ApplySchedule_ReleasesOnlyOnScheduledBins()
    {
        var available = new[] { 10d, 12d, 14d, 16d, 18d };
        var capacity = new[] { 5d, 5d, 5d, 5d, 5d };

        var result = DispatchScheduleProcessor.ApplySchedule(periodBins: 2, phaseOffset: 1, available, capacity);

        Assert.Equal(new[] { 0d, 5d, 0d, 5d, 0d }, result);
    }

    [Fact]
    public void ApplySchedule_UsesCapacityOverridesPerBin()
    {
        var available = new[] { 3d, 7d, 11d, 13d };
        var capacity = new[] { 2d, 4d, 6d, 8d };

        var result = DispatchScheduleProcessor.ApplySchedule( periodBins: 3, phaseOffset: 0, available, capacity);

        Assert.Equal(new[] { 2d, 0d, 0d, 8d }, result);
    }
}
