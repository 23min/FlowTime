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

    // --- CycleTimeComputer: AC-1 ---

    [Fact]
    public void CycleTime_QueueTime_NormalCase()
    {
        // queueDepth=10, served=5, binMs=60000 (1 min) → (10/5)*60000 = 120000 ms
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: 60000);
        Assert.Equal(120000.0, qt);
    }

    [Fact]
    public void CycleTime_QueueTime_ZeroServed_ReturnsNull()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 0, binMs: 60000);
        Assert.Null(qt);
    }

    [Fact]
    public void CycleTime_QueueTime_NegativeServed_ReturnsNull()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: -1, binMs: 60000);
        Assert.Null(qt);
    }

    [Fact]
    public void CycleTime_QueueTime_ZeroBinMs_ReturnsNull()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: 0);
        Assert.Null(qt);
    }

    [Fact]
    public void CycleTime_ServiceTime_NormalCase()
    {
        // processingTimeMsSum=500, servedCount=10 → 50 ms avg
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: 10);
        Assert.Equal(50.0, st);
    }

    [Fact]
    public void CycleTime_ServiceTime_NullProcessingTime_ReturnsNull()
    {
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: 10);
        Assert.Null(st);
    }

    [Fact]
    public void CycleTime_ServiceTime_NullServedCount_ReturnsNull()
    {
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: null);
        Assert.Null(st);
    }

    [Fact]
    public void CycleTime_ServiceTime_ZeroServedCount_ReturnsNull()
    {
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: 0);
        Assert.Null(st);
    }

    [Fact]
    public void CycleTime_BothComponents_SumsCorrectly()
    {
        // queueTime=120000, serviceTime=50 → 120050
        var ct = CycleTimeComputer.CalculateCycleTime(queueTimeMs: 120000, serviceTimeMs: 50);
        Assert.Equal(120050.0, ct);
    }

    [Fact]
    public void CycleTime_ServiceTimeUnavailable_DegradesToQueueTime()
    {
        var ct = CycleTimeComputer.CalculateCycleTime(queueTimeMs: 120000, serviceTimeMs: null);
        Assert.Equal(120000.0, ct);
    }

    [Fact]
    public void CycleTime_QueueTimeNull_ReturnsServiceTime()
    {
        var ct = CycleTimeComputer.CalculateCycleTime(queueTimeMs: null, serviceTimeMs: 50);
        Assert.Equal(50.0, ct);
    }

    [Fact]
    public void CycleTime_BothNull_ReturnsNull()
    {
        var ct = CycleTimeComputer.CalculateCycleTime(queueTimeMs: null, serviceTimeMs: null);
        Assert.Null(ct);
    }

    // --- CycleTimeComputer: AC-2 (per-class) ---

    [Fact]
    public void CycleTime_PerClass_DifferentClassesGetDifferentValues()
    {
        // Class A: high queue, low service
        var qtA = CycleTimeComputer.CalculateQueueTime(queueDepth: 20, served: 10, binMs: 60000);
        var stA = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 100, servedCount: 10);
        var ctA = CycleTimeComputer.CalculateCycleTime(qtA, stA);

        // Class B: low queue, high service
        var qtB = CycleTimeComputer.CalculateQueueTime(queueDepth: 2, served: 10, binMs: 60000);
        var stB = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 5000, servedCount: 10);
        var ctB = CycleTimeComputer.CalculateCycleTime(qtB, stB);

        Assert.Equal(120000.0, qtA);   // (20/10)*60000
        Assert.Equal(10.0, stA);       // 100/10
        Assert.Equal(120010.0, ctA);   // 120000+10

        Assert.Equal(12000.0, qtB);    // (2/10)*60000
        Assert.Equal(500.0, stB);      // 5000/10
        Assert.Equal(12500.0, ctB);    // 12000+500

        Assert.NotEqual(ctA, ctB);
    }

    [Fact]
    public void CycleTime_PerClass_OneClassMissingServiceTime()
    {
        // Class A: has service time
        var qtA = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: 60000);
        var stA = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 250, servedCount: 5);
        var ctA = CycleTimeComputer.CalculateCycleTime(qtA, stA);

        // Class B: no service time (synthetic model)
        var qtB = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: 60000);
        var stB = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null);
        var ctB = CycleTimeComputer.CalculateCycleTime(qtB, stB);

        Assert.Equal(120050.0, ctA);   // 120000 + 50
        Assert.Equal(120000.0, ctB);   // queue time only (degrades gracefully)
        Assert.Null(stB);
    }

    // --- CycleTimeComputer: AC-3 (flow efficiency) ---

    [Fact]
    public void FlowEfficiency_NormalCase()
    {
        // serviceTime=50, cycleTime=200 → 0.25 (25% of cycle is processing)
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 50, cycleTimeMs: 200);
        Assert.Equal(0.25, fe);
    }

    [Fact]
    public void FlowEfficiency_AllProcessing_ReturnsOne()
    {
        // serviceTime equals cycleTime (no queue time = 0)
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 100, cycleTimeMs: 100);
        Assert.Equal(1.0, fe);
    }

    [Fact]
    public void FlowEfficiency_ServiceTimeNull_ReturnsNull()
    {
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: null, cycleTimeMs: 120000);
        Assert.Null(fe);
    }

    [Fact]
    public void FlowEfficiency_CycleTimeNull_ReturnsNull()
    {
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 50, cycleTimeMs: null);
        Assert.Null(fe);
    }

    [Fact]
    public void FlowEfficiency_CycleTimeZero_ReturnsNull()
    {
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 50, cycleTimeMs: 0);
        Assert.Null(fe);
    }

    [Fact]
    public void FlowEfficiency_BothNull_ReturnsNull()
    {
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: null, cycleTimeMs: null);
        Assert.Null(fe);
    }
}
