using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for the Core analytical evaluator over window and by-class inputs.
/// </summary>
public class AnalyticalComputationTests
{
    private const double BinMs = 60_000;

    [Fact]
    public void ComputeWindow_ServiceWithBuffer_ReturnsAllSeries()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-1",
            Arrivals = [5, 10, 8, 0],
            Served = [5, 10, 8, 0],
            QueueDepth = [10, 20, 5, 0],
            ProcessingTimeMsSum = [500, 1000, 400, 0],
            ServedCount = [10, 10, 8, 0],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 4, binMs: BinMs);

        Assert.Equal(4, result.QueueTimeMs.Length);
        Assert.Equal(4, result.ServiceTimeMs.Length);
        Assert.Equal(4, result.CycleTimeMs.Length);
        Assert.Equal(4, result.FlowEfficiency.Length);
        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(120_050.0, result.CycleTimeMs[0]);
        Assert.NotNull(result.FlowEfficiency[0]);
        Assert.Null(result.QueueTimeMs[3]);
        Assert.Null(result.ServiceTimeMs[3]);
        Assert.Null(result.CycleTimeMs[3]);
        Assert.Null(result.FlowEfficiency[3]);
    }

    [Fact]
    public void ComputeWindow_ServiceOnly_NoQueueSeries()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");
        var data = new NodeData
        {
            NodeId = "svc-1",
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20],
            ProcessingTimeMsSum = [500, 1000],
            ServedCount = [10, 10],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 2, binMs: BinMs);

        Assert.All(result.QueueTimeMs, value => Assert.Null(value));
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(100.0, result.ServiceTimeMs[1]);
    }

    [Fact]
    public void ComputeWindow_QueueOnly_NoServiceSeries()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");
        var data = new NodeData
        {
            NodeId = "q-1",
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 2, binMs: BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.All(result.ServiceTimeMs, value => Assert.Null(value));
        Assert.All(result.FlowEfficiency, value => Assert.Null(value));
    }

    [Fact]
    public void ComputeWindow_ComputedNode_AllNulls()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("const");
        var data = new NodeData
        {
            NodeId = "c-1",
            Arrivals = [5, 10],
            Served = [5, 10],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 2, binMs: BinMs);

        Assert.All(result.QueueTimeMs, value => Assert.Null(value));
        Assert.All(result.ServiceTimeMs, value => Assert.Null(value));
        Assert.All(result.CycleTimeMs, value => Assert.Null(value));
        Assert.All(result.FlowEfficiency, value => Assert.Null(value));
    }

    [Fact]
    public void ComputeWindow_StartBinOffset_ReadsCorrectSlice()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-2",
            Arrivals = [0, 0, 5, 10],
            Served = [0, 0, 5, 10],
            QueueDepth = [0, 0, 10, 20],
            ProcessingTimeMsSum = [0, 0, 500, 1000],
            ServedCount = [0, 0, 10, 10],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 2, count: 2, binMs: BinMs);

        Assert.Equal(2, result.QueueTimeMs.Length);
        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Equal(120_000.0, result.QueueTimeMs[1]);
    }

    [Fact]
    public void ComputeWindow_BeyondDataLength_ReturnsNulls()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-3",
            Arrivals = [5],
            Served = [5],
            QueueDepth = [10],
            ProcessingTimeMsSum = [500],
            ServedCount = [10],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 3, binMs: BinMs);

        Assert.Equal(3, result.QueueTimeMs.Length);
        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Null(result.QueueTimeMs[1]);
        Assert.Null(result.QueueTimeMs[2]);
    }

    [Fact]
    public void ComputeWindow_NaNInSeries_ProducesNull()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var data = new NodeData
        {
            NodeId = "swb-nan",
            Arrivals = [5, 5],
            Served = [5, 5],
            QueueDepth = [double.NaN, 10],
            ProcessingTimeMsSum = [500, double.PositiveInfinity],
            ServedCount = [10, 10],
        };

        var result = AnalyticalEvaluator.ComputeWindow(descriptor, data, startBin: 0, count: 2, binMs: BinMs);

        Assert.Null(result.QueueTimeMs[0]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(120_000.0, result.QueueTimeMs[1]);
        Assert.Null(result.ServiceTimeMs[1]);
    }

    [Fact]
    public void ComputeClassBin_ServiceWithBuffer_ReturnsAllMetrics()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var snapshot = new ClassMetricsSnapshot(
            Arrivals: 10,
            Served: 5,
            Errors: 0,
            Queue: 10,
            Capacity: 20,
            ProcessingTimeMsSum: 500,
            ServedCount: 10);

        var result = AnalyticalEvaluator.ComputeClassBin(descriptor, snapshot, BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(120_050.0, result.CycleTimeMs);
        Assert.NotNull(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeClassBin_ServiceOnly_NoQueueMetrics()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");
        var snapshot = new ClassMetricsSnapshot(
            Arrivals: 10,
            Served: 5,
            Errors: 0,
            Queue: 10,
            Capacity: 20,
            ProcessingTimeMsSum: 500,
            ServedCount: 10);

        var result = AnalyticalEvaluator.ComputeClassBin(descriptor, snapshot, BinMs);

        Assert.Null(result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
    }

    [Fact]
    public void ComputeClassWindow_ServiceWithBuffer_ReturnsAllSeries()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var classData = new NodeClassData
        {
            Arrivals = [5, 10],
            Served = [5, 10],
            QueueDepth = [10, 20],
            ProcessingTimeMsSum = [500, 1000],
            ServedCount = [10, 10],
        };

        var result = AnalyticalEvaluator.ComputeClassWindow(descriptor, classData, startBin: 0, count: 2, binMs: BinMs);

        Assert.Equal(120_000.0, result.QueueTimeMs[0]);
        Assert.Equal(120_000.0, result.QueueTimeMs[1]);
        Assert.Equal(50.0, result.ServiceTimeMs[0]);
        Assert.Equal(100.0, result.ServiceTimeMs[1]);
    }

    [Fact]
    public void ComputeClassWindow_NullClassData_ReturnsNulls()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var classData = new NodeClassData
        {
            Arrivals = [5],
            Served = [5],
        };

        var result = AnalyticalEvaluator.ComputeClassWindow(descriptor, classData, startBin: 0, count: 1, binMs: BinMs);

        Assert.Null(result.QueueTimeMs[0]);
        Assert.Null(result.ServiceTimeMs[0]);
    }
}