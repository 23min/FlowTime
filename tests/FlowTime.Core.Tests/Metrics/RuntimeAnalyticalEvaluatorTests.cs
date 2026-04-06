using System.Collections.Generic;
using FlowTime.Core.Compiler;
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
    public void ComputeBin_WithConstantParallelism_ComputesCapacitySection()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        var semantics = Semantics(parallelism: ParallelismReference.Literal(2d));
        var data = Data(served: new[] { 5d }, capacity: new[] { 10d });

        var result = RuntimeAnalyticalEvaluator.ComputeBin(descriptor, semantics, data, 0, 60_000);

        Assert.NotNull(result.Capacity);
        Assert.Equal(10d, result.Capacity!.BaseCapacity);
        Assert.Equal(2d, result.Capacity.Parallelism);
        Assert.Equal(20d, result.Capacity.EffectiveCapacity);
        Assert.Equal(0.25d, result.Capacity.Utilization!.Value, 5);
    }

    [Fact]
    public void ComputeWindow_WithServedOverride_ComputesUtilizationFromEffectiveCapacity()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        var semantics = Semantics(parallelism: ParallelismReference.Literal(2d));
        var data = Data(served: new[] { 1d, 1d, 1d, 1d }, capacity: new[] { 10d, 10d, 10d, 10d });
        var servedOverride = new double?[] { 5d, 10d, 2.5d, 5d };

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 4, 60_000, servedOverride);

        Assert.NotNull(result.Capacity);
        Assert.Equal(new double?[] { 10d, 10d, 10d, 10d }, result.Capacity!.BaseCapacity);
        Assert.Equal(new double?[] { 2d, 2d, 2d, 2d }, result.Capacity.Parallelism);
        Assert.Equal(new double?[] { 20d, 20d, 20d, 20d }, result.Capacity.EffectiveCapacity);
        Assert.Equal(new double?[] { 0.25d, 0.5d, 0.125d, 0.25d }, result.Capacity.Utilization);
    }

    [Fact]
    public void ComputeWindow_WithClassEntries_ProjectsByClassAnalyticalValues()
    {
        var descriptor = Descriptor(RuntimeAnalyticalNodeCategory.Service);
        var semantics = Semantics();
        var data = Data(served: new[] { 8d, 8d });
        var classEntries = new[]
        {
            ClassEntry<NodeClassData>.Specific("vip", new NodeClassData
            {
                Arrivals = new[] { 6d, 5d },
                Served = new[] { 5d, 4d },
                Errors = new[] { 1d, 0d },
                ProcessingTimeMsSum = new[] { 1500d, 1200d },
                ServedCount = new[] { 5d, 4d }
            }),
            ClassEntry<NodeClassData>.Specific("standard", new NodeClassData
            {
                Arrivals = new[] { 4d, 5d },
                Served = new[] { 3d, 4d },
                Errors = new[] { 0d, 1d },
                ProcessingTimeMsSum = new[] { 750d, 1000d },
                ServedCount = new[] { 3d, 4d }
            })
        };

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(
            descriptor,
            semantics,
            data,
            0,
            2,
            60_000,
            classEntries: classEntries);

        Assert.NotNull(result.ByClass);

        var vip = Assert.Single(result.ByClass!, entry => entry.ContractKey == "vip").Payload;
        Assert.Equal(new double?[] { 300d, 300d }, vip.ServiceTimeMs);
        Assert.Equal(new double?[] { 300d, 300d }, vip.CycleTimeMs);
        Assert.Equal(new double?[] { 1d, 1d }, vip.FlowEfficiency);

        var standard = Assert.Single(result.ByClass, entry => entry.ContractKey == "standard").Payload;
        Assert.Equal(new double?[] { 250d, 250d }, standard.ServiceTimeMs);
        Assert.Equal(new double?[] { 250d, 250d }, standard.CycleTimeMs);
        Assert.Equal(new double?[] { 1d, 1d }, standard.FlowEfficiency);
    }

    [Fact]
    public void ComputeWindow_ServiceWithBufferMissingProcessingInputs_DoesNotEmitFlowEfficiency()
    {
        var descriptor = Descriptor(
            category: RuntimeAnalyticalNodeCategory.Service,
            hasQueueSemantics: true,
            hasServiceSemantics: true,
            identity: RuntimeAnalyticalIdentity.ServiceWithBuffer);
        var semantics = Semantics();
        var data = Data(
            served: new[] { 5d, 4d },
            queueDepth: new[] { 10d, 8d });

        var result = RuntimeAnalyticalEvaluator.ComputeWindow(descriptor, semantics, data, 0, 2, 60_000);

        Assert.True(result.Emission.EmitCycleTimeMs);
        Assert.True(result.Emission.EmitQueueTimeMs);
        Assert.False(result.Emission.EmitServiceTimeMs);
        Assert.False(result.Emission.EmitFlowEfficiency);
        Assert.All(result.FlowEfficiency, value => Assert.Null(value));
    }

    [Fact]
    public void ComputeFlowLatency_UsesWeightedUpstreamCycleTime()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node
                {
                    Id = "UpstreamFast",
                    Kind = "service",
                    Analytical = Descriptor(RuntimeAnalyticalNodeCategory.Service),
                    Semantics = Semantics(),
                },
                new Node
                {
                    Id = "UpstreamSlow",
                    Kind = "service",
                    Analytical = Descriptor(RuntimeAnalyticalNodeCategory.Service),
                    Semantics = Semantics(),
                },
                new Node
                {
                    Id = "Sink",
                    Kind = "sink",
                    Analytical = Descriptor(RuntimeAnalyticalNodeCategory.Sink),
                    Semantics = Semantics(),
                }
            },
            Edges = new[]
            {
                new Edge { Id = "fast_to_sink", Source = "UpstreamFast:out", Target = "Sink:in" },
                new Edge { Id = "slow_to_sink", Source = "UpstreamSlow:out", Target = "Sink:in" }
            }
        };

        var cycleTimeByNode = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["UpstreamFast"] = new double?[] { 1_000d, 1_000d },
            ["UpstreamSlow"] = new double?[] { 5_000d, 5_000d },
            ["Sink"] = new double?[] { 200d, 200d }
        };
        var edgeFlows = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fast_to_sink"] = new double?[] { 1d, 1d },
            ["slow_to_sink"] = new double?[] { 3d, 3d }
        };
        var nodeData = new Dictionary<string, NodeData>(StringComparer.OrdinalIgnoreCase)
        {
            ["UpstreamFast"] = Data(served: new[] { 1d, 1d }, arrivals: new[] { 1d, 1d }),
            ["UpstreamSlow"] = Data(served: new[] { 3d, 3d }, arrivals: new[] { 3d, 3d }),
            ["Sink"] = Data(served: new[] { 4d, 4d }, arrivals: new[] { 4d, 4d })
        };

        var result = RuntimeAnalyticalEvaluator.ComputeFlowLatency(topology, cycleTimeByNode, edgeFlows, nodeData);

        Assert.Equal(new double?[] { 4_200d, 4_200d }, result["Sink"]);
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

    private static NodeSemantics Semantics(ParallelismReference? parallelism = null)
    {
        return new NodeSemantics
        {
            Arrivals = SemanticReferenceResolver.ParseSeriesReference("self"),
            Served = SemanticReferenceResolver.ParseSeriesReference("self"),
            Parallelism = parallelism
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