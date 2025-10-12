using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests;

public class MetricsTests
{
    [Fact]
    public void Utilization_WithCapacity_ComputesRatio()
    {
        var utilization = UtilizationComputer.Calculate(served: 90, capacity: 100);
        Assert.Equal(0.9, utilization);
    }

    [Fact]
    public void Utilization_NoCapacity_ReturnsNull()
    {
        var utilization = UtilizationComputer.Calculate(served: 90, capacity: null);
        Assert.Null(utilization);
    }

    [Fact]
    public void Latency_WithServed_ComputesLittleLaw()
    {
        var latency = LatencyComputer.Calculate(queue: 8, served: 140, binMinutes: 5);
        Assert.NotNull(latency);
        Assert.Equal(0.2857142857142857, latency.Value, 8);
    }

    [Fact]
    public void Latency_NoServed_ReturnsNull()
    {
        var latency = LatencyComputer.Calculate(queue: 8, served: 0, binMinutes: 5);
        Assert.Null(latency);
    }

    [Theory]
    [InlineData(0.65, "green")]
    [InlineData(0.75, "yellow")]
    [InlineData(0.95, "red")]
    public void Coloring_ServiceThresholds(double utilization, string expected)
    {
        var color = ColoringRules.PickServiceColor(utilization);
        Assert.Equal(expected, color);
    }

    [Theory]
    [InlineData(4.0, 5.0, "green")]
    [InlineData(7.0, 5.0, "yellow")]
    [InlineData(10.0, 5.0, "red")]
    public void Coloring_QueueSla(double latency, double sla, string expected)
    {
        var color = ColoringRules.PickQueueColor(latency, sla);
        Assert.Equal(expected, color);
    }

    [Fact]
    public void Coloring_NoCapacity_ReturnsGray()
    {
        var color = ColoringRules.PickServiceColor(null);
        Assert.Equal("gray", color);
    }
}
