using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for the Core analytical computation surface — AC-2.
/// Verifies that Core can compute derived metrics for windows (multi-bin)
/// and per-class breakdowns, gated by capabilities.
/// </summary>
public class AnalyticalComputationTests
{
    private const double BinMs = 60_000; // 1-minute bin

    // ── Window computation (multi-bin series) ──

    [Fact]
    public void ComputeWindow_ServiceWithBuffer_ReturnsAllSeries()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-1",
            Arrivals = [5, 10, 8, 0],
            Served = [5, 10, 8, 0],
            QueueDepth = [10, 20, 5, 0],
            ProcessingTimeMsSum = [500, 1000, 400, 0],
            ServedCount = [10, 10, 8, 0],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 4, binMs: BinMs);

        Assert.Equal(4, result.QueueTimeMs.Length);
        Assert.Equal(4, result.ServiceTimeMs.Length);
        Assert.Equal(4, result.CycleTimeMs.Length);
        Assert.Equal(4, result.FlowEfficiency.Length);

        // Bin 0: qt = (10/5)*60000 = 120000, st = 500/10 = 50
        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(120_050.0, result.CycleTimeMs[0]);
        Assert.NotNull(result.FlowEfficiency[0]);

        // Bin 3: zero served → nulls
        Assert.Null(result.QueueTimeMs[3]);
        Assert.Null(result.ServiceTimeMs[3]);
        Assert.Null(result.CycleTimeMs[3]);
        Assert.Null(result.FlowEfficiency[3]);
    }

    [Fact]
    public void ComputeWindow_ServiceOnly_NoQueueSeries()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");
        var data = new NodeData
        {
            NodeId = "svc-1",
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20], // present but ignored for service-only
            ProcessingTimeMsSum = [500, 1000],
            ServedCount = [10, 10],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 2, binMs: BinMs);

        // Queue time should be null for all bins
        Assert.All(result.QueueTimeMs, v => Assert.Null(v));

        // Service time should be computed
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(100.0, result.ServiceTimeMs[1]);
    }

    [Fact]
    public void ComputeWindow_QueueOnly_NoServiceSeries()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        var data = new NodeData
        {
            NodeId = "q-1",
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 2, binMs: BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.All(result.ServiceTimeMs, v => Assert.Null(v));
        Assert.All(result.FlowEfficiency, v => Assert.Null(v));
    }

    [Fact]
    public void ComputeWindow_ComputedNode_AllNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "const");
        var data = new NodeData
        {
            NodeId = "c-1",
            Arrivals = [5, 10],
            Served = [5, 10],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 2, binMs: BinMs);

        Assert.All(result.QueueTimeMs, v => Assert.Null(v));
        Assert.All(result.ServiceTimeMs, v => Assert.Null(v));
        Assert.All(result.CycleTimeMs, v => Assert.Null(v));
        Assert.All(result.FlowEfficiency, v => Assert.Null(v));
    }

    [Fact]
    public void ComputeWindow_StartBinOffset_ReadsCorrectSlice()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-2",
            Arrivals = [0, 0, 5, 10],
            Served = [0, 0, 5, 10],
            QueueDepth = [0, 0, 10, 20],
            ProcessingTimeMsSum = [0, 0, 500, 1000],
            ServedCount = [0, 0, 10, 10],
        };

        var result = caps.ComputeWindow(data, startBin: 2, count: 2, binMs: BinMs);

        Assert.Equal(2, result.QueueTimeMs.Length);
        Assert.Equal(120_000.0, result.QueueTimeMs[0]); // bin index 2: (10/5)*60000
        Assert.Equal(120_000.0, result.QueueTimeMs[1]); // bin index 3: (20/10)*60000
    }

    [Fact]
    public void ComputeWindow_BeyondDataLength_ReturnsNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-3",
            Arrivals = [5],
            Served = [5],
            QueueDepth = [10],
            ProcessingTimeMsSum = [500],
            ServedCount = [10],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 3, binMs: BinMs);

        Assert.Equal(3, result.QueueTimeMs.Length);
        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Null(result.QueueTimeMs[1]); // beyond data
        Assert.Null(result.QueueTimeMs[2]); // beyond data
    }

    [Fact]
    public void ComputeWindow_NaNInSeries_ProducesNull()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-nan",
            Arrivals = [5, 5],
            Served = [5, 5],
            QueueDepth = [double.NaN, 10],
            ProcessingTimeMsSum = [500, double.PositiveInfinity],
            ServedCount = [10, 10],
        };

        var result = caps.ComputeWindow(data, startBin: 0, count: 2, binMs: BinMs);

        // Bin 0: NaN queue depth → null queue time
        Assert.Null(result.QueueTimeMs[0]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]); // service is fine

        // Bin 1: Infinity processingTimeMsSum → null service time
        Assert.Equal(120_000.0, result.QueueTimeMs[1]); // queue is fine
        Assert.Null(result.ServiceTimeMs[1]);
    }

    // ── Per-class single-bin computation ──

    [Fact]
    public void ComputeClassBin_ServiceWithBuffer_ReturnsAllMetrics()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var snapshot = new ClassMetricsSnapshot(
            Arrivals: 10,
            Served: 5,
            Errors: 0,
            Queue: 10,
            Capacity: 20,
            ProcessingTimeMsSum: 500,
            ServedCount: 10);

        var result = caps.ComputeClassBin(snapshot, BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(120_050.0, result.CycleTimeMs);
        Assert.NotNull(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeClassBin_ServiceOnly_NoQueueMetrics()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");
        var snapshot = new ClassMetricsSnapshot(
            Arrivals: 10,
            Served: 5,
            Errors: 0,
            Queue: 10,
            ProcessingTimeMsSum: 500,
            ServedCount: 10);

        var result = caps.ComputeClassBin(snapshot, BinMs);

        Assert.Null(result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
    }

    // ── Per-class window computation ──

    [Fact]
    public void ComputeClassWindow_ServiceWithBuffer_ReturnsAllSeries()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var classData = new NodeClassData
        {
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20],
            ProcessingTimeMsSum = [500, 1000],
            ServedCount = [10, 10],
        };

        var result = caps.ComputeClassWindow(classData, startBin: 0, count: 2, binMs: BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Equal(120_000.0, result.QueueTimeMs[1]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(100.0, result.ServiceTimeMs[1]);
    }

    [Fact]
    public void ComputeClassWindow_NullClassData_ReturnsNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var classData = new NodeClassData
        {
            Arrivals = [5],
            Served = [5],
            // no queue or service data
        };

        var result = caps.ComputeClassWindow(classData, startBin: 0, count: 1, binMs: BinMs);

        Assert.Null(result.QueueTimeMs[0]);
        Assert.Null(result.ServiceTimeMs[0]);
    }
}
