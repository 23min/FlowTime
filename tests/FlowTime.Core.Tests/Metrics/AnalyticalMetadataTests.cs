using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for analytical metadata and stationarity warning eligibility.
/// </summary>
public class AnalyticalMetadataTests
{
    [Fact]
    public void ServiceWithBuffer_AdvertisesAllAnalyticalKeys()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var keys = AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Contains("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void ServiceOnly_DoesNotAdvertiseQueueTimeMs()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");
        var keys = AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.DoesNotContain("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void QueueOnly_DoesNotAdvertiseServiceOrEfficiency()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");
        var keys = AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Contains("queueTimeMs", keys);
        Assert.DoesNotContain("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.DoesNotContain("flowEfficiency", keys);
    }

    [Fact]
    public void ComputedNode_AdvertisesNoAnalyticalKeys()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("const");
        var keys = AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(descriptor);

        Assert.Empty(keys);
    }

    [Fact]
    public void LogicalType_ServiceWithBuffer_SameAsExplicit()
    {
        var explicitDescriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var resolvedDescriptor = AnalyticalDescriptorTestFactory.ResolvedServiceWithBuffer();

        Assert.Equal(
            AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(explicitDescriptor),
            AnalyticalEvaluator.GetAdvertisedAnalyticalKeys(resolvedDescriptor));
    }

    [Fact]
    public void ServiceWithBuffer_StationarityApplicable()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceOnly_StationarityNotApplicable()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Queue_StationarityApplicable()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ComputedNode_StationarityNotApplicable()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("expression");
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void CheckStationarity_NonStationary_ReturnsTrue()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        double[] arrivals = [10, 10, 10, 50, 50, 50];

        Assert.True(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_Stationary_ReturnsFalse()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        double[] arrivals = [10, 10, 10, 10, 10, 10];

        Assert.False(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_NotApplicable_ReturnsFalse()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");
        double[] arrivals = [10, 10, 10, 50, 50, 50];

        Assert.False(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_CustomTolerance_Respected()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");
        double[] arrivals = [10, 10, 14, 14];

        Assert.True(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
        Assert.False(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.30));
    }

    [Fact]
    public void CheckStationarity_TooFewBins_ReturnsFalse()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");
        double[] arrivals = [10];

        Assert.False(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_QueueCapable_NonStationary_ReturnsTrue()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        double[] arrivals = [10, 10, 50, 50];

        Assert.True(AnalyticalEvaluator.CheckStationarity(descriptor, arrivals, tolerance: 0.25));
    }

    [Fact]
    public void ComputeBin_QueueCapable_IncludesLatencyMinutes()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        var result = AnalyticalEvaluator.ComputeBin(
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
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.LatencyMinutes);
    }
}