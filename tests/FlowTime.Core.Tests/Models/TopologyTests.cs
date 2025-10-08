using System;
using System.Linq;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Models;

public class TopologyTests
{
    [Fact]
    public void GetNode_ExistingNode_ReturnsNode()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") },
                new Node { Id = "B", Semantics = CreateSemantics("b_in", "b_out", "b_err") }
            },
            Edges = Array.Empty<Edge>()
        };

        var node = topology.GetNode("A");

        Assert.Equal("A", node.Id);
    }

    [Fact]
    public void GetNode_MissingNode_Throws()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") }
            },
            Edges = Array.Empty<Edge>()
        };

        Assert.Throws<InvalidOperationException>(() => topology.GetNode("missing"));
    }

    [Fact]
    public void GetOutgoingEdges_ReturnsEdges()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") },
                new Node { Id = "B", Semantics = CreateSemantics("b_in", "b_out", "b_err") }
            },
            Edges = new[]
            {
                new Edge { Source = "A", Target = "B", Weight = 0.75 },
                new Edge { Source = "B", Target = "A", Weight = 0.20 }
            }
        };

        var outgoing = topology.GetOutgoingEdges("A").ToList();

        Assert.Single(outgoing);
        Assert.Equal("B", outgoing[0].Target);
        Assert.Equal(0.75, outgoing[0].Weight);
    }

    [Fact]
    public void GetIncomingEdges_ReturnsEdges()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") },
                new Node { Id = "B", Semantics = CreateSemantics("b_in", "b_out", "b_err") }
            },
            Edges = new[]
            {
                new Edge { Source = "A", Target = "B", Weight = 0.75 },
                new Edge { Source = "B", Target = "A", Weight = 0.20 }
            }
        };

        var incoming = topology.GetIncomingEdges("A").ToList();

        Assert.Single(incoming);
        Assert.Equal("B", incoming[0].Source);
        Assert.Equal(0.20, incoming[0].Weight);
    }

    [Fact]
    public void NodeSemantics_AllowsOptionalFields()
    {
        var semantics = new NodeSemantics
        {
            Arrivals = "orders_arrivals",
            Served = "orders_served",
            Errors = "orders_errors",
            ExternalDemand = null,
            QueueDepth = "orders_queue"
        };

        Assert.Equal("orders_queue", semantics.QueueDepth);
        Assert.Null(semantics.ExternalDemand);
    }

    private static NodeSemantics CreateSemantics(string arrivals, string served, string errors) => new()
    {
        Arrivals = arrivals,
        Served = served,
        Errors = errors
    };
}
