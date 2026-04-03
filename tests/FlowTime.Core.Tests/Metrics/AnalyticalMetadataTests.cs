using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for analytical metadata and stationarity warning generation — AC-4 and AC-5.
/// Metadata describes which derived keys a node can produce, driven by capabilities.
/// </summary>
public class AnalyticalMetadataTests
{
    // ── AC-4: Metadata driven by capabilities ──

    [Fact]
    public void ServiceWithBuffer_AdvertisesAllAnalyticalKeys()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var keys = caps.GetAdvertisedAnalyticalKeys();

        Assert.Contains("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void ServiceOnly_DoesNotAdvertiseQueueTimeMs()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");
        var keys = caps.GetAdvertisedAnalyticalKeys();

        Assert.DoesNotContain("queueTimeMs", keys);
        Assert.Contains("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.Contains("flowEfficiency", keys);
    }

    [Fact]
    public void QueueOnly_DoesNotAdvertiseServiceOrEfficiency()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        var keys = caps.GetAdvertisedAnalyticalKeys();

        Assert.Contains("queueTimeMs", keys);
        Assert.DoesNotContain("serviceTimeMs", keys);
        Assert.Contains("cycleTimeMs", keys);
        Assert.DoesNotContain("flowEfficiency", keys);
    }

    [Fact]
    public void ComputedNode_AdvertisesNoAnalyticalKeys()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "const");
        var keys = caps.GetAdvertisedAnalyticalKeys();

        Assert.Empty(keys);
    }

    [Fact]
    public void LogicalType_ServiceWithBuffer_SameAsExplicit()
    {
        var explicit_ = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var resolved = AnalyticalCapabilities.Resolve(kind: "service", logicalType: "serviceWithBuffer");

        Assert.Equal(explicit_.GetAdvertisedAnalyticalKeys(), resolved.GetAdvertisedAnalyticalKeys());
    }

    // ── AC-5: Stationarity warning eligibility ──

    [Fact]
    public void ServiceWithBuffer_StationarityApplicable()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        Assert.True(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceOnly_StationarityNotApplicable()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");
        Assert.False(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Queue_StationarityApplicable()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        Assert.True(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void ComputedNode_StationarityNotApplicable()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "expression");
        Assert.False(caps.StationarityWarningApplicable);
    }

    // ── AC-5: Stationarity check with configurable tolerance ──

    [Fact]
    public void CheckStationarity_NonStationary_ReturnsTrue()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        double[] arrivals = [10, 10, 10, 50, 50, 50]; // significant divergence

        Assert.True(caps.CheckStationarity(arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_Stationary_ReturnsFalse()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        double[] arrivals = [10, 10, 10, 10, 10, 10]; // no divergence

        Assert.False(caps.CheckStationarity(arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_NotApplicable_ReturnsFalse()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");
        double[] arrivals = [10, 10, 10, 50, 50, 50]; // would be non-stationary, but not applicable

        Assert.False(caps.CheckStationarity(arrivals, tolerance: 0.25));
    }

    [Fact]
    public void CheckStationarity_CustomTolerance_Respected()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        double[] arrivals = [10, 10, 14, 14]; // divergence = 4/14 ≈ 28.6%

        // 25% tolerance → non-stationary
        Assert.True(caps.CheckStationarity(arrivals, tolerance: 0.25));
        // 30% tolerance → stationary
        Assert.False(caps.CheckStationarity(arrivals, tolerance: 0.30));
    }

    [Fact]
    public void CheckStationarity_TooFewBins_ReturnsFalse()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        double[] arrivals = [10];

        Assert.False(caps.CheckStationarity(arrivals, tolerance: 0.25));
    }

    // ── Stationarity is a capability-level gate; payload-level gating is in the adapter ──

    [Fact]
    public void CheckStationarity_QueueCapable_NonStationary_ReturnsTrue()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        double[] arrivals = [10, 10, 50, 50];

        Assert.True(caps.CheckStationarity(arrivals, tolerance: 0.25));
    }

    // ── Finding 5: LatencyMinutes in Core computation ──

    [Fact]
    public void ComputeBin_QueueCapable_IncludesLatencyMinutes()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        var result = caps.ComputeBin(
            queueDepth: 10, served: 5,
            processingTimeMsSum: 500, servedCount: 10,
            binMs: 60_000);

        // latencyMinutes = (10/5) * 1.0 = 2.0 (binMinutes = binMs/60000)
        Assert.Equal(2.0, result.LatencyMinutes);
    }

    [Fact]
    public void ComputeBin_ServiceOnly_NoLatencyMinutes()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");

        var result = caps.ComputeBin(
            queueDepth: 10, served: 5,
            processingTimeMsSum: 500, servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.LatencyMinutes);
    }
}
