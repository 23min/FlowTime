using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

public class RuntimeAnalyticalEvaluatorTests
{
    [Fact]
    public void ServiceWithBufferDescriptor_HasAllCapabilities()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        Assert.True(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.True(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void QueueDescriptor_HasQueueSemanticsOnly()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);

        Assert.True(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void DlqDescriptor_HasQueueSemanticsOnly()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Dlq);

        Assert.True(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.True(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ServiceDescriptor_HasServiceSemanticsOnly()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);

        Assert.False(descriptor.HasQueueSemantics);
        Assert.True(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ConstantDescriptor_HasNoAnalyticalCapabilities()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Constant);

        Assert.False(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ExpressionDescriptor_HasNoAnalyticalCapabilities()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Expression);

        Assert.False(descriptor.HasQueueSemantics);
        Assert.False(descriptor.HasServiceSemantics);
        Assert.False(descriptor.HasCycleTimeDecomposition);
        Assert.False(descriptor.StationarityWarningApplicable);
    }

    [Fact]
    public void ToLogicalType_ReflectsServiceWithBufferIdentity()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        Assert.Equal("servicewithbuffer", descriptor.ToLogicalType());
    }

    [Fact]
    public void ToLogicalType_ReflectsQueueIdentity()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);

        Assert.Equal("queue", descriptor.ToLogicalType());
    }

    [Fact]
    public void ComputeBin_NaNInputs_ReturnsNulls()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
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
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
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

        Assert.Equal(120_000.0, result.QueueTimeMs);
        Assert.Equal(50.0, result.ServiceTimeMs);
        Assert.Equal(120_050.0, result.CycleTimeMs);
        Assert.NotNull(result.FlowEfficiency);
    }

    [Fact]
    public void ComputeBin_ServiceOnly_NoQueueMetrics()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
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
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Queue);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
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
    public void ComputeBin_ComputedNode_ReturnsAllNulls()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Constant);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
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
    public void ComputeBin_NaN_ProducedByDivision_ReturnsNull()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);

        var result = RuntimeAnalyticalEvaluator.ComputeBin(
            descriptor,
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