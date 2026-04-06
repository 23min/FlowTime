using System;
using System.Linq;
using FlowTime.Core.Compiler;
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
    public void GetOutgoingEdges_UnknownNode_ReturnsEmpty()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") }
            },
            Edges = new[]
            {
                new Edge { Source = "A", Target = "B", Weight = 1.0 }
            }
        };

        var outgoing = topology.GetOutgoingEdges("unknown");

        Assert.Empty(outgoing);
    }

    [Fact]
    public void GetIncomingEdges_UnknownNode_ReturnsEmpty()
    {
        var topology = new Topology
        {
            Nodes = new[]
            {
                new Node { Id = "A", Semantics = CreateSemantics("a_in", "a_out", "a_err") }
            },
            Edges = new[]
            {
                new Edge { Source = "A", Target = "B", Weight = 1.0 }
            }
        };

        var incoming = topology.GetIncomingEdges("unknown");

        Assert.Empty(incoming);
    }

    [Fact]
    public void NodeSemantics_AllowsOptionalFields()
    {
        var semantics = new NodeSemantics
        {
            Arrivals = Ref("orders_arrivals"),
            Served = Ref("orders_served"),
            Errors = Ref("orders_errors"),
            ExternalDemand = null,
            QueueDepth = Ref("orders_queue")
        };

        Assert.Equal("orders_queue", semantics.QueueDepth!.RawText);
        Assert.Null(semantics.ExternalDemand);
    }

    [Fact]
    public void NodeSemantics_AllowsTypedParallelismReference()
    {
        var semantics = new NodeSemantics
        {
            Arrivals = Ref("orders_arrivals"),
            Served = Ref("orders_served"),
            Parallelism = ParallelismReference.Literal(2d)
        };

        Assert.NotNull(semantics.Parallelism);
        Assert.Equal(2d, semantics.Parallelism!.Constant);
        Assert.Null(semantics.Parallelism.SeriesReference);
    }

    private static NodeSemantics CreateSemantics(string arrivals, string served, string errors) => new()
    {
        Arrivals = Ref(arrivals),
        Served = Ref(served),
        Errors = Ref(errors)
    };

    private static CompiledSeriesReference Ref(string value) =>
        SemanticReferenceResolver.ParseSeriesReference(value);
}
