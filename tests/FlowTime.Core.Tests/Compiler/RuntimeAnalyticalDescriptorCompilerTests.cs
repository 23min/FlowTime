using FlowTime.Core.Metrics;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Compiler;

public sealed class RuntimeAnalyticalDescriptorCompilerTests
{
    [Fact]
    public void ParseMetadata_ExplicitServiceWithBufferNode_CompilesDescriptorFacts()
    {
        var metadata = ModelParser.ParseMetadata(new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "QueueNode",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            QueueDepth = "self",
                            Parallelism = ParallelismReference.Literal(3d)
                        }
                    }
                }
            }
        });

        var node = Assert.Single(metadata.Topology!.Nodes);

        Assert.Equal(RuntimeAnalyticalIdentity.ServiceWithBuffer, node.Analytical.Identity);
        Assert.Equal("servicewithbuffer", node.Analytical.ToLogicalType());
        Assert.Equal(RuntimeAnalyticalNodeCategory.Service, node.Analytical.Category);
        Assert.True(node.Analytical.HasQueueSemantics);
        Assert.True(node.Analytical.HasServiceSemantics);
        Assert.True(node.Analytical.HasCycleTimeDecomposition);
        Assert.True(node.Analytical.StationarityWarningApplicable);
        Assert.Equal("QueueNode", node.Analytical.QueueSourceNodeId);
        Assert.NotNull(node.Analytical.Parallelism);
        Assert.Equal(3d, node.Analytical.Parallelism!.Constant);
        Assert.Null(node.Analytical.Parallelism.SeriesSourceNodeId);
    }

    [Fact]
    public void ParseMetadata_QueueBackedServiceNode_CompilesQueueFactsWithoutFileInference()
    {
        var metadata = ModelParser.ParseMetadata(new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition
                {
                    Id = "queue_helper",
                    Kind = "serviceWithBuffer",
                    Inflow = "arrivals",
                    Outflow = "served"
                }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ExplicitQueueService",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            QueueDepth = "self"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "ReferencedQueueService",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            QueueDepth = "queue_helper"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "FileQueueService",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            QueueDepth = "file:telemetry/queue.csv"
                        }
                    }
                }
            }
        });

        var nodes = metadata.Topology!.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var explicitNode = nodes["ExplicitQueueService"];
        var referencedNode = nodes["ReferencedQueueService"];
        var fileNode = nodes["FileQueueService"];

        Assert.Equal(explicitNode.Analytical.Category, referencedNode.Analytical.Category);
        Assert.Equal(explicitNode.Analytical.HasQueueSemantics, referencedNode.Analytical.HasQueueSemantics);
        Assert.Equal(explicitNode.Analytical.HasServiceSemantics, referencedNode.Analytical.HasServiceSemantics);
        Assert.Equal(explicitNode.Analytical.HasCycleTimeDecomposition, referencedNode.Analytical.HasCycleTimeDecomposition);
        Assert.Equal(explicitNode.Analytical.StationarityWarningApplicable, referencedNode.Analytical.StationarityWarningApplicable);
        Assert.Equal(RuntimeAnalyticalIdentity.ServiceWithBuffer, explicitNode.Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalIdentity.ServiceWithBuffer, referencedNode.Analytical.Identity);
        Assert.Equal("servicewithbuffer", explicitNode.Analytical.ToLogicalType());
        Assert.Equal("servicewithbuffer", referencedNode.Analytical.ToLogicalType());

        Assert.Equal("ExplicitQueueService", explicitNode.Analytical.QueueSourceNodeId);
        Assert.Equal("queue_helper", referencedNode.Analytical.QueueSourceNodeId);

        Assert.Equal(RuntimeAnalyticalIdentity.Service, fileNode.Analytical.Identity);
        Assert.Equal("service", fileNode.Analytical.ToLogicalType());
        Assert.Equal(RuntimeAnalyticalNodeCategory.Service, fileNode.Analytical.Category);
        Assert.False(fileNode.Analytical.HasQueueSemantics);
        Assert.True(fileNode.Analytical.HasServiceSemantics);
        Assert.False(fileNode.Analytical.HasCycleTimeDecomposition);
        Assert.Null(fileNode.Analytical.QueueSourceNodeId);
    }

    [Fact]
    public void ParseMetadata_CompilesExpectedCategories_ForNonServiceTopologyKinds()
    {
        var metadata = ModelParser.ParseMetadata(new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "RouterNode",
                        Kind = "router",
                        Semantics = new TopologyNodeSemanticsDefinition { Arrivals = "arrivals", Served = "served" }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "DependencyNode",
                        Kind = "dependency",
                        Semantics = new TopologyNodeSemanticsDefinition { Arrivals = "arrivals", Served = "served" }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "DeadLetter",
                        Kind = "dlq",
                        Semantics = new TopologyNodeSemanticsDefinition { Arrivals = "arrivals", Served = "served" }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Terminal",
                        Kind = "service",
                        NodeRole = "sink",
                        Semantics = new TopologyNodeSemanticsDefinition { Arrivals = "arrivals", Served = "served" }
                    }
                }
            }
        });

        var nodes = metadata.Topology!.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(RuntimeAnalyticalIdentity.Router, nodes["RouterNode"].Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalIdentity.Dependency, nodes["DependencyNode"].Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalIdentity.Dlq, nodes["DeadLetter"].Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalIdentity.Sink, nodes["Terminal"].Analytical.Identity);
        Assert.Equal("router", nodes["RouterNode"].Analytical.ToLogicalType());
        Assert.Equal("dependency", nodes["DependencyNode"].Analytical.ToLogicalType());
        Assert.Equal("dlq", nodes["DeadLetter"].Analytical.ToLogicalType());
        Assert.Equal("sink", nodes["Terminal"].Analytical.ToLogicalType());
        Assert.Equal(RuntimeAnalyticalNodeCategory.Router, nodes["RouterNode"].Analytical.Category);
        Assert.Equal(RuntimeAnalyticalNodeCategory.Dependency, nodes["DependencyNode"].Analytical.Category);
        Assert.Equal(RuntimeAnalyticalNodeCategory.Dlq, nodes["DeadLetter"].Analytical.Category);
        Assert.Equal(RuntimeAnalyticalNodeCategory.Sink, nodes["Terminal"].Analytical.Category);
    }

    [Theory]
    [InlineData(" ServiceWithBuffer ")]
    [InlineData("SERVICEWITHBUFFER")]
    public void ParseMetadata_ServiceWithBufferKind_NormalizesCaseAndWhitespace(string kind)
    {
        var metadata = ModelParser.ParseMetadata(new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "QueueNode",
                        Kind = kind,
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            QueueDepth = "self"
                        }
                    }
                }
            }
        });

        var node = Assert.Single(metadata.Topology!.Nodes);

        Assert.Equal(RuntimeAnalyticalIdentity.ServiceWithBuffer, node.Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalNodeCategory.Service, node.Analytical.Category);
        Assert.Equal("servicewithbuffer", node.Analytical.ToLogicalType());
    }

    [Fact]
    public void ParseMetadata_NullKind_DefaultsToServiceIdentity()
    {
        var metadata = ModelParser.ParseMetadata(new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceNode",
                        Kind = null,
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served"
                        }
                    }
                }
            }
        });

        var node = Assert.Single(metadata.Topology!.Nodes);

        Assert.Equal(RuntimeAnalyticalIdentity.Service, node.Analytical.Identity);
        Assert.Equal(RuntimeAnalyticalNodeCategory.Service, node.Analytical.Category);
        Assert.Equal("service", node.Analytical.ToLogicalType());
    }
}