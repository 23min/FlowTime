using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

public class RuntimeAnalyticalDescriptorMetadataTests
{
    [Fact]
    public void ServiceWithBuffer_AdvertisesAllAnalyticalKeys()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        var keys = RuntimeAnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Contains("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void ServiceOnly_DoesNotAdvertiseQueueTimeMs()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);
        var keys = RuntimeAnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.DoesNotContain("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void QueueOnly_DoesNotAdvertiseServiceOrEfficiency()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);
        var keys = RuntimeAnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Contains("queueTimeMs", keys);
        Assert.DoesNotContain("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.DoesNotContain("flowEfficiency", keys);
    }

    [Fact]
    public void ComputedNode_AdvertisesNoAnalyticalKeys()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Constant);
        var keys = RuntimeAnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Empty(keys);
    }

    [Fact]
    public void ServiceWithBuffer_StationarityApplicable()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceOnly_StationarityNotApplicable()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Queue_StationarityApplicable()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ComputedNode_StationarityNotApplicable()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Expression);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void CheckStationarity_NonStationary_ReturnsTrue()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        double[] arrivals = [10, 10, 10, 50, 50, 50];

        Assert.True(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_Stationary_ReturnsFalse()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        double[] arrivals = [10, 10, 10, 10, 10, 10];

        Assert.False(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_NotApplicable_ReturnsFalse()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);
        double[] arrivals = [10, 10, 10, 50, 50, 50];

        Assert.False(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_CustomTolerance_Respected()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);
        double[] arrivals = [10, 10, 14, 14];

        Assert.True(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
        Assert.False(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.30));
    }

    [Fact]
    public void CheckStationarity_TooFewBins_ReturnsFalse()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);
        double[] arrivals = [10];

        Assert.False(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_QueueCapable_NonStationary_ReturnsTrue()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        double[] arrivals = [10, 10, 50, 50];

        Assert.True(RuntimeAnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void ComputeBin_QueueCapable_IncludesLatencyMinutes()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Equal(2.0, result.LatencyMinutes);
    }

    [Fact]
    public void ComputeBin_ServiceOnly_NoLatencyMinutes()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.LatencyMinutes);
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
}