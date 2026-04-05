using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

public sealed class AnalyticalDescriptorCompilationTests
{
    [Fact]
    public void ParseMetadata_ServiceWithBufferNode_CarriesCompiledAnalyticalDescriptor()
    {
        var metadata = ModelParser.ParseMetadata(BuildDescriptorModel());

        var bufferNode = Assert.Single(metadata.Topology!.Nodes, node => node.Id == "BufferNode");

        Assert.Equal(AnalyticalIdentity.ServiceWithBuffer, bufferNode.Analytical.Identity);
        Assert.Equal(AnalyticalNodeCategory.Service, bufferNode.Analytical.Category);
        Assert.True(bufferNode.Analytical.HasQueueSemantics);
        Assert.True(bufferNode.Analytical.HasServiceSemantics);
        Assert.True(bufferNode.Analytical.HasCycleTimeDecomposition);
        Assert.True(bufferNode.Analytical.StationarityWarningApplicable);
        Assert.Equal("BufferNode", bufferNode.Analytical.QueueSourceNodeId);
        Assert.Equal(AnalyticalQueueOrigin.Explicit, bufferNode.Analytical.QueueOrigin);
        Assert.NotNull(bufferNode.Analytical.Parallelism);
        Assert.Equal(2d, bufferNode.Analytical.Parallelism!.Constant);
    }

    [Fact]
    public void ParseMetadata_ReferenceResolvedQueueBackedNode_MatchesExplicitServiceWithBufferDescriptor()
    {
        var metadata = ModelParser.ParseMetadata(BuildDescriptorModel());

        var bufferNode = Assert.Single(metadata.Topology!.Nodes, node => node.Id == "BufferNode");
        var queueReader = Assert.Single(metadata.Topology.Nodes, node => node.Id == "QueueReader");

        Assert.Equal(AnalyticalIdentity.ServiceWithBuffer, queueReader.Analytical.Identity);
        Assert.Equal(AnalyticalNodeCategory.Service, queueReader.Analytical.Category);
        Assert.Equal("BufferNode", queueReader.Analytical.QueueSourceNodeId);
        Assert.Equal(bufferNode.Analytical.HasQueueSemantics, queueReader.Analytical.HasQueueSemantics);
        Assert.Equal(bufferNode.Analytical.HasServiceSemantics, queueReader.Analytical.HasServiceSemantics);
        Assert.Equal(bufferNode.Analytical.HasCycleTimeDecomposition, queueReader.Analytical.HasCycleTimeDecomposition);
        Assert.Equal(bufferNode.Analytical.StationarityWarningApplicable, queueReader.Analytical.StationarityWarningApplicable);
    }

    [Theory]
    [InlineData("service", AnalyticalIdentity.Service, AnalyticalNodeCategory.Service)]
    [InlineData("queue", AnalyticalIdentity.Queue, AnalyticalNodeCategory.Queue)]
    [InlineData("dlq", AnalyticalIdentity.Dlq, AnalyticalNodeCategory.Dlq)]
    [InlineData("router", AnalyticalIdentity.Router, AnalyticalNodeCategory.Router)]
    [InlineData("expr", AnalyticalIdentity.Expression, AnalyticalNodeCategory.Expression)]
    [InlineData("const", AnalyticalIdentity.Constant, AnalyticalNodeCategory.Constant)]
    public void ParseMetadata_CompilesNodeCategoryFromRuntimeIdentity(
        string kind,
        AnalyticalIdentity expectedIdentity,
        AnalyticalNodeCategory expectedCategory)
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "minutes" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "NodeA",
                        Kind = kind,
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:arrivals.csv",
                            Served = "file:served.csv",
                            Errors = "file:errors.csv"
                        }
                    }
                }
            }
        };

        var metadata = ModelParser.ParseMetadata(model);
        var node = Assert.Single(metadata.Topology!.Nodes);

        Assert.Equal(expectedIdentity, node.Analytical.Identity);
        Assert.Equal(expectedCategory, node.Analytical.Category);
    }

    private static ModelDefinition BuildDescriptorModel()
    {
        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 4, BinSize = 1, BinUnit = "minutes" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "BufferNode",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:BufferNode_arrivals.csv",
                            Served = "file:BufferNode_served.csv",
                            Errors = "file:BufferNode_errors.csv",
                            QueueDepth = "file:BufferNode_queue.csv",
                            Capacity = "file:BufferNode_capacity.csv",
                            Parallelism = 2d,
                            ProcessingTimeMsSum = "file:BufferNode_processingTimeMsSum.csv",
                            ServedCount = "file:BufferNode_servedCount.csv"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "QueueReader",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:QueueReader_arrivals.csv",
                            Served = "file:QueueReader_served.csv",
                            Errors = "file:QueueReader_errors.csv",
                            QueueDepth = "file:BufferNode.csv",
                            Capacity = "file:QueueReader_capacity.csv",
                            ProcessingTimeMsSum = "file:QueueReader_processingTimeMsSum.csv",
                            ServedCount = "file:QueueReader_servedCount.csv"
                        }
                    }
                }
            }
        };
    }
}