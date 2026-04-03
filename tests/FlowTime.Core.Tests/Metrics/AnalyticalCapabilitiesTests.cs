using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for AnalyticalCapabilities resolution — AC-1 and AC-3.
/// Verifies that capability flags are correctly derived from node kind,
/// logicalType, and available input series.
/// </summary>
public class AnalyticalCapabilitiesTests
{
    // ── AC-1: Capability resolution from kind ──

    [Fact]
    public void ServiceWithBuffer_Explicit_HasAllCapabilities()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        Assert.True(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
        Assert.True(caps.HasCycleTimeDecomposition);
        Assert.True(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceWithBuffer_LogicalType_IdenticalToExplicit()
    {
        var explicit_ = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");
        var resolved = AnalyticalCapabilities.Resolve(kind: "service", logicalType: "serviceWithBuffer");

        Assert.Equal(explicit_.HasQueueSemantics, resolved.HasQueueSemantics);
        Assert.Equal(explicit_.HasServiceSemantics, resolved.HasServiceSemantics);
        Assert.Equal(explicit_.HasCycleTimeDecomposition, resolved.HasCycleTimeDecomposition);
        Assert.Equal(explicit_.StationarityWarningApplicable, resolved.StationarityWarningApplicable);
    }

    [Fact]
    public void Queue_HasQueueSemanticsOnly()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");

        Assert.True(caps.HasQueueSemantics);
        Assert.False(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.True(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Dlq_HasQueueSemanticsOnly()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "dlq");

        Assert.True(caps.HasQueueSemantics);
        Assert.False(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.True(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Service_HasServiceSemanticsOnly()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");

        Assert.False(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.False(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Const_HasNoAnalyticalCapabilities()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "const");

        Assert.False(caps.HasQueueSemantics);
        Assert.False(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.False(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Expression_HasNoAnalyticalCapabilities()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "expression");

        Assert.False(caps.HasQueueSemantics);
        Assert.False(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.False(caps.StationarityWarningApplicable);
    }

    [Fact]
    public void Pmf_HasNoAnalyticalCapabilities()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "pmf");

        Assert.False(caps.HasQueueSemantics);
        Assert.False(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
        Assert.False(caps.StationarityWarningApplicable);
    }

    // ── AC-1: Case insensitivity and normalization ──

    [Theory]
    [InlineData("ServiceWithBuffer")]
    [InlineData("SERVICEWITHBUFFER")]
    [InlineData(" serviceWithBuffer ")]
    public void KindResolution_IsCaseInsensitiveAndTrimmed(string kind)
    {
        var caps = AnalyticalCapabilities.Resolve(kind: kind);

        Assert.True(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NullOrEmptyKind_DefaultsToService(string? kind)
    {
        var caps = AnalyticalCapabilities.Resolve(kind: kind);

        Assert.False(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
        Assert.False(caps.HasCycleTimeDecomposition);
    }

    // ── AC-1: LogicalType overrides kind for capability resolution ──

    [Fact]
    public void LogicalType_ServiceWithBuffer_OverridesServiceKind()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service", logicalType: "serviceWithBuffer");

        Assert.True(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
        Assert.True(caps.HasCycleTimeDecomposition);
    }

    [Fact]
    public void LogicalType_Null_DoesNotOverride()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service", logicalType: null);

        Assert.False(caps.HasQueueSemantics);
        Assert.True(caps.HasServiceSemantics);
    }

    // ── AC-1: CycleTimeDecomposition requires both queue AND service ──

    [Fact]
    public void CycleTimeDecomposition_RequiresBothQueueAndService()
    {
        var queueOnly = AnalyticalCapabilities.Resolve(kind: "queue");
        var serviceOnly = AnalyticalCapabilities.Resolve(kind: "service");
        var both = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        Assert.False(queueOnly.HasCycleTimeDecomposition);
        Assert.False(serviceOnly.HasCycleTimeDecomposition);
        Assert.True(both.HasCycleTimeDecomposition);
    }

    // ── AC-1: EffectiveKind reflects the resolved type ──

    [Fact]
    public void EffectiveKind_ReflectsLogicalTypeWhenPresent()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service", logicalType: "serviceWithBuffer");
        Assert.Equal("servicewithbuffer", caps.EffectiveKind);
    }

    [Fact]
    public void EffectiveKind_ReflectsKindWhenNoLogicalType()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");
        Assert.Equal("queue", caps.EffectiveKind);
    }

    // ── AC-3: Finite-value safety in computation ──

    [Fact]
    public void ComputeBin_NaNInputs_ReturnsNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        var result = caps.ComputeBin(
            queueDepth: double.NaN,
            served: double.NaN,
            processingTimeMsSum: double.NaN,
            servedCount: double.NaN,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Null(result.CycleTimeMs);
        Assert.Null(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_InfinityInputs_ReturnsNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        var result = caps.ComputeBin(
            queueDepth: double.PositiveInfinity,
            served: 5,
            processingTimeMsSum: double.NegativeInfinity,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Null(result.CycleTimeMs);
        Assert.Null(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_ValidInputs_ServiceWithBuffer_ReturnsAllMetrics()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        var result = caps.ComputeBin(
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(120_050.0, result.CycleTimeMs);
        Assert.NotNull(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_ServiceOnly_NoQueueMetrics()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "service");

        var result = caps.ComputeBin(
            queueDepth: 10,  // provided but should be ignored — service nodes don't have queue semantics
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(50.0, result.CycleTimeMs);  // falls back to service time only
        Assert.Equal(1.0, result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_QueueOnly_NoServiceMetrics()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "queue");

        var result = caps.ComputeBin(
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,  // provided but should be ignored — queue nodes don't have service semantics
            servedCount: 10,
            binMs: 60_000);

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Equal(120_000.0, result.CycleTimeMs);  // queue time only
        Assert.Null(result.FlowEfficiency);  // no service time → no efficiency
    }

    [Fact]
    public void ComputeBin_ComputedNode_ReturnsAllNulls()
    {
        var caps = AnalyticalCapabilities.Resolve(kind: "const");

        var result = caps.ComputeBin(
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Null(result.CycleTimeMs);
        Assert.Null(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_NaN_ProducedByDivision_ReturnsNull()
    {
        // 0/0 in IEEE 754 produces NaN — must be caught
        var caps = AnalyticalCapabilities.Resolve(kind: "serviceWithBuffer");

        var result = caps.ComputeBin(
            queueDepth: 0,
            served: 0,
            processingTimeMsSum: 0,
            servedCount: 0,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Null(result.CycleTimeMs);
        Assert.Null(result.FlowEfficiency);
    }
}
