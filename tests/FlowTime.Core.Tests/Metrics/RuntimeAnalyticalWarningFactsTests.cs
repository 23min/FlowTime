using FlowTime.Core.Compiler;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

public class RuntimeAnalyticalWarningFactsTests
{
    private const double BinMs = 60_000;

    [Fact]
    public void ComputeWindow_NonStationaryArrivals_EmitsStationarityWarningFact()
    {
        var descriptor = ServiceWithBufferDescriptor();
        var semantics = Semantics();
        var data = Data(
            served: [5d, 5d, 5d, 5d],
            arrivals: [10d, 10d, 50d, 50d],
            queueDepth: [10d, 10d, 10d, 10d],
            processingTimeMsSum: [500d, 500d, 500d, 500d],
            servedCount: [10d, 10d, 10d, 10d]);

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 4, BinMs);

        Assert.True(result.WarningFacts.NonStationary);
    }

    [Fact]
    public void ComputeWindow_NonStationaryArrivals_RespectsCustomStationarityTolerance()
    {
        var descriptor = ServiceWithBufferDescriptor();
        var semantics = Semantics();
        var data = Data(
            served: [5d, 5d, 5d, 5d],
            arrivals: [10d, 10d, 16d, 16d],
            queueDepth: [10d, 10d, 10d, 10d],
            processingTimeMsSum: [500d, 500d, 500d, 500d],
            servedCount: [10d, 10d, 10d, 10d]);

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(
            descriptor,
            semantics,
            data,
            0,
            4,
            BinMs,
            stationarityTolerance: 0.5);

        Assert.False(result.WarningFacts.NonStationary);
    }

    [Fact]
    public void ComputeWindow_SustainedQueueGrowth_EmitsBacklogGrowthWarningFact()
    {
        var descriptor = ServiceWithBufferDescriptor();
        var semantics = Semantics();
        var data = Data(
            served: [5d, 5d, 5d, 5d],
            arrivals: [5d, 5d, 5d, 5d],
            queueDepth: [1d, 2d, 3d, 4d],
            processingTimeMsSum: [500d, 500d, 500d, 500d],
            servedCount: [10d, 10d, 10d, 10d]);

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 4, BinMs);

        Assert.NotNull(result.WarningFacts.BacklogGrowth);
        Assert.Equal(0, result.WarningFacts.BacklogGrowth!.StartBin);
        Assert.Equal(3, result.WarningFacts.BacklogGrowth.EndBin);
        Assert.Equal(3, result.WarningFacts.BacklogGrowth.Length);
    }

    [Fact]
    public void ComputeWindow_ArrivalsExceedEffectiveCapacity_EmitsOverloadWarningFact()
    {
        var descriptor = ServiceWithBufferDescriptor();
        var semantics = Semantics();
        var data = Data(
            served: [5d, 5d, 5d, 5d],
            arrivals: [20d, 20d, 20d, 20d],
            capacity: [10d, 10d, 10d, 10d],
            queueDepth: [10d, 10d, 10d, 10d],
            processingTimeMsSum: [500d, 500d, 500d, 500d],
            servedCount: [10d, 10d, 10d, 10d]);

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 4, BinMs);

        Assert.NotNull(result.WarningFacts.Overload);
        Assert.Equal(0, result.WarningFacts.Overload!.StartBin);
        Assert.Equal(3, result.WarningFacts.Overload.EndBin);
        Assert.Equal(4, result.WarningFacts.Overload.Length);
    }

    [Fact]
    public void ComputeWindow_BacklogLatencyExceedsSla_EmitsAgeRiskWarningFact()
    {
        var descriptor = ServiceWithBufferDescriptor();
        var semantics = Semantics(slaMinutes: 5d);
        var data = Data(
            served: [1d, 1d, 1d, 1d],
            arrivals: [1d, 1d, 1d, 1d],
            queueDepth: [10d, 10d, 10d, 10d],
            processingTimeMsSum: [500d, 500d, 500d, 500d],
            servedCount: [10d, 10d, 10d, 10d]);

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 4, BinMs);

        Assert.NotNull(result.WarningFacts.AgeRisk);
        Assert.Equal(0, result.WarningFacts.AgeRisk!.StartBin);
        Assert.Equal(3, result.WarningFacts.AgeRisk.EndBin);
        Assert.Equal(4, result.WarningFacts.AgeRisk.Length);
    }

    private static RuntimeAnalyticalDescriptor ServiceWithBufferDescriptor()
    {
        return Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
    }

    private static RuntimeAnalyticalDescriptor Descriptor(
        RuntimeAnalyticalNodeCategory category,
        bool? hasQueueSemantics = null,
        bool? hasServiceSemantics = null,
        RuntimeAnalyticalIdentity? identity = null)
    {
        var queue = hasQueueSemantics ?? category is RuntimeAnalyticalNodeCategory.Queue or RuntimeAnalyticalNodeCategory.Dlq;
        var service = hasServiceSemantics ?? category == RuntimeAnalyticalNodeCategory.Service;

        return new RuntimeAnalyticalDescriptor
        {
            Identity = identity ?? ResolveIdentity(category, queue, service),
            Category = category,
            HasQueueSemantics = queue,
            HasServiceSemantics = service,
            HasCycleTimeDecomposition = queue && service,
            StationarityWarningApplicable = queue
        };
    }

    private static RuntimeAnalyticalIdentity ResolveIdentity(
        RuntimeAnalyticalNodeCategory category,
        bool hasQueueSemantics,
        bool hasServiceSemantics)
    {
        return category switch
        {
            RuntimeAnalyticalNodeCategory.Service when hasQueueSemantics && hasServiceSemantics => RuntimeAnalyticalIdentity.ServiceWithBuffer,
            RuntimeAnalyticalNodeCategory.Service => RuntimeAnalyticalIdentity.Service,
            RuntimeAnalyticalNodeCategory.Queue => RuntimeAnalyticalIdentity.Queue,
            RuntimeAnalyticalNodeCategory.Dlq => RuntimeAnalyticalIdentity.Dlq,
            RuntimeAnalyticalNodeCategory.Router => RuntimeAnalyticalIdentity.Router,
            RuntimeAnalyticalNodeCategory.Dependency => RuntimeAnalyticalIdentity.Dependency,
            RuntimeAnalyticalNodeCategory.Sink => RuntimeAnalyticalIdentity.Sink,
            RuntimeAnalyticalNodeCategory.Constant => RuntimeAnalyticalIdentity.Constant,
            RuntimeAnalyticalNodeCategory.Expression => RuntimeAnalyticalIdentity.Expression,
            _ => RuntimeAnalyticalIdentity.Service
        };
    }

    private static NodeSemantics Semantics(ParallelismReference? parallelism = null, double? slaMinutes = null)
    {
        return new NodeSemantics
        {
            Arrivals = SemanticReferenceResolver.ParseSeriesReference("self"),
            Served = SemanticReferenceResolver.ParseSeriesReference("self"),
            Parallelism = parallelism,
            SlaMinutes = slaMinutes
        };
    }

    private static NodeData Data(
        double[] served,
        double[]? capacity = null,
        double[]? parallelism = null,
        double[]? arrivals = null,
        double[]? queueDepth = null,
        double[]? processingTimeMsSum = null,
        double[]? servedCount = null)
    {
        return new NodeData
        {
            NodeId = "node",
            Arrivals = arrivals ?? new double[served.Length],
            Served = served,
            Capacity = capacity,
            Parallelism = parallelism,
            QueueDepth = queueDepth,
            ProcessingTimeMsSum = processingTimeMsSum,
            ServedCount = servedCount
        };
    }
}