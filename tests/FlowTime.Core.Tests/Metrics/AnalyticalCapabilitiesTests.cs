using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for compiled analytical descriptor facts and single-bin evaluation.
/// </summary>
public class AnalyticalDescriptorTests
{
    [Fact]
    public void ServiceWithBuffer_Explicit_HasAllCapabilities()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        Assert.Equal(AnalyticalIdentity.ServiceWithBuffer, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Service, descriptor.Category);
        Assert.True(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.True(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceWithBuffer_LogicalType_IdenticalToExplicit()
    {
        var explicitDescriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");
        var resolvedDescriptor = AnalyticalDescriptorTestFactory.ResolvedServiceWithBuffer();

        Assert.Equal(explicitDescriptor.Identity, resolvedDescriptor.Identity);
        Assert.Equal(explicitDescriptor.Category, resolvedDescriptor.Category);
        Assert.Equal(explicitDescriptor.HasQueueSemantics, resolvedDescriptor.HasQueueSemantics);
        Assert.Equal(explicitDescriptor.HasServiceSemantics, resolvedDescriptor.HasServiceSemantics);
        Assert.Equal(explicitDescriptor.HasCycleTimeDecomposition, resolvedDescriptor.HasCycleTimeDecomposition);
        Assert.Equal(explicitDescriptor.StationarityWarningApplicable, resolvedDescriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Queue_HasQueueSemanticsOnly()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");

        Assert.Equal(AnalyticalIdentity.Queue, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Queue, descriptor.Category);
        Assert.True(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Dlq_HasQueueSemanticsOnly()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("dlq");

        Assert.Equal(AnalyticalIdentity.Dlq, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Dlq, descriptor.Category);
        Assert.True(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Service_HasServiceSemanticsOnly()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");

        Assert.Equal(AnalyticalIdentity.Service, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Service, descriptor.Category);
        Assert.False(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Const_HasNoAnalyticalDescriptorFacts()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("const");

        Assert.Equal(AnalyticalIdentity.Constant, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Constant, descriptor.Category);
        Assert.False(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Expression_HasNoAnalyticalDescriptorFacts()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("expression");

        Assert.Equal(AnalyticalIdentity.Expression, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Expression, descriptor.Category);
        Assert.False(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void Pmf_HasNoAnalyticalDescriptorFacts()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("pmf");

        Assert.Equal(AnalyticalIdentity.Constant, descriptor.Identity);
        Assert.Equal(AnalyticalNodeCategory.Constant, descriptor.Category);
        Assert.False(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Theory]
    [InlineData("ServiceWithBuffer")]
    [InlineData("SERVICEWITHBUFFER")]
    [InlineData(" serviceWithBuffer ")]
    public void KindResolution_IsCaseInsensitiveAndTrimmed(string kind)
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind(kind);

        Assert.Equal(AnalyticalIdentity.ServiceWithBuffer, descriptor.Identity);
        Assert.True(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NullOrEmptyKind_DefaultsToService(string? kind)
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind(kind);

        Assert.Equal(AnalyticalIdentity.Service, descriptor.Identity);
        Assert.False(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
    }

    [Fact]
    public void LogicalType_ServiceWithBuffer_OverridesServiceKind()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ResolvedServiceWithBuffer();

        Assert.Equal(AnalyticalIdentity.ServiceWithBuffer, descriptor.Identity);
        Assert.Equal("BufferNode", descriptor.QueueSourceNodeId);
        Assert.True(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.True(descriptor.HasCycleTimeDecomposition);
    }

    [Fact]
    public void LogicalType_Null_DoesNotOverride()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");

        Assert.False(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
    }

    [Fact]
    public void CycleTimeDecomposition_RequiresBothQueueAndService()
    {
        var queueOnly = AnalyticalDescriptorTestFactory.ForKind("queue");
        var serviceOnly = AnalyticalDescriptorTestFactory.ForKind("service");
        var both = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        Assert.False(queueOnly.HasCycleTimeDecomposition);
        Assert.False(serviceOnly.HasCycleTimeDecomposition);
        Assert.True(both.HasCycleTimeDecomposition);
    }

    [Fact]
    public void ComputeBin_NaNInputs_ReturnsNulls()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
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
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
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
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
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
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("service");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(50.0, result.CycleTimeMs);
        Assert.Equal(1.0, result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_QueueOnly_NoServiceMetrics()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("queue");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 5,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Null(result.ServiceTimeMs);
        Assert.Equal(120_000.0, result.CycleTimeMs);
        Assert.Null(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_ComputedNode_NoMetrics()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("const");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
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
    public void ComputeBin_ZeroServed_NoQueueTimeOrLatency()
    {
        var descriptor = AnalyticalDescriptorTestFactory.ForKind("serviceWithBuffer");

        var result = AnalyticalEvaluator.ComputeBin(
            descriptor,
            queueDepth: 10,
            served: 0,
            processingTimeMsSum: 500,
            servedCount: 10,
            binMs: 60_000);

        Assert.Null(result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(50.0, result.CycleTimeMs);
        Assert.Null(result.LatencyMinutes);
    }
}