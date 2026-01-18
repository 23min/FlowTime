using FlowTime.Core.Compiler;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Compiler;

public sealed class ModelCompilerTests
{
    [Fact]
    public void ModelCompiler_ExpandsQueueDepthNodes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 1d, 1d, 1d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 1d, 1d, 1d } },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 0d, 0d, 0d } }
            },
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
                            Errors = "errors",
                            QueueDepth = "queue_depth"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var queueNode = compiled.Nodes.SingleOrDefault(node => node.Id == "queue_depth");

        Assert.NotNull(queueNode);
        Assert.Equal("serviceWithBuffer", queueNode!.Kind, ignoreCase: true);
        Assert.Equal("arrivals", queueNode.Inflow);
        Assert.Equal("served", queueNode.Outflow);
        Assert.Equal("errors", queueNode.Loss);
        Assert.NotNull(queueNode.Metadata);
        Assert.Equal("true", queueNode.Metadata!["graph.hidden"]);
        Assert.Equal("derived", queueNode.Metadata!["series.origin"]);

        var topo = compiled.Topology?.Nodes.Single(node => node.Id == "QueueNode");
        Assert.NotNull(topo);
        Assert.Equal("queue_depth", topo!.Semantics.QueueDepth);
    }

    [Fact]
    public void ModelCompiler_ExpandsRetryEchoNodes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 2d, 0d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 2d, 0d } },
                new NodeDefinition { Id = "failures", Kind = "const", Values = new[] { 2d, 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "RetryNode",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Failures = "failures",
                            RetryEcho = "retry_echo",
                            RetryKernel = new[] { 0d, 1d }
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var retryNode = compiled.Nodes.SingleOrDefault(node => node.Id == "retry_echo");

        Assert.NotNull(retryNode);
        Assert.Equal("expr", retryNode!.Kind, ignoreCase: true);
        Assert.Equal("CONV(failures, [0, 1])", retryNode.Expr);
        Assert.NotNull(retryNode.Metadata);
        Assert.Equal("true", retryNode.Metadata!["graph.hidden"]);
    }

    [Fact]
    public void ModelCompiler_RespectsInitialConditions()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 1d, 1d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 1d, 1d } }
            },
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
                            QueueDepth = "self"
                        },
                        InitialCondition = new InitialConditionDefinition { QueueDepth = 2d }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var topo = compiled.Topology?.Nodes.Single(node => node.Id == "QueueNode");

        Assert.NotNull(topo);
        Assert.Equal(2d, topo!.InitialCondition!.QueueDepth);
        Assert.Equal("queue_node_queue", topo.Semantics.QueueDepth);
        Assert.Contains(compiled.Nodes, node => node.Id == "queue_node_queue");
    }
}
