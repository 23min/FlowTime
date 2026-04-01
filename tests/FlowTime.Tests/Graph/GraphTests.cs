using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Tests.GraphEvaluation;

public class GraphTests
{
    [Fact]
    public void TopoOrder_NoCycles_Works()
    {
        var grid = new TimeGrid(4, 60, TimeUnit.Minutes);
        var a = new ConstSeriesNode("a", new double[]{1,1,1,1});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 2);
        var g = new Graph(new INode[]{ a, b });
        var order = g.TopologicalOrder();
        Assert.Equal([new NodeId("a"), new NodeId("b")], order);
    }

    [Fact]
    public void TopologicalOrder_ReturnsCachedInstance()
    {
        var a = new ConstSeriesNode("a", new double[]{1,1,1,1});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 2);
        var g = new Graph(new INode[]{ a, b });

        var order1 = g.TopologicalOrder();
        var order2 = g.TopologicalOrder();

        Assert.Same(order1, order2);
    }

    [Fact]
    public void Constructor_CyclicGraph_Throws()
    {
        // a depends on b, b depends on a — cycle
        var a = new BinaryOpNode("a", new NodeId("b"), new NodeId("__scalar__"), BinOp.Mul, 1);
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 1);

        var ex = Assert.Throws<InvalidOperationException>(() => new Graph(new INode[] { a, b }));
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_MulScalar_ProducesExpected()
    {
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var a = new ConstSeriesNode("a", new double[]{2,3,4});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 0.5);
        var g = new Graph(new INode[]{ a, b });
        var ctx = g.Evaluate(grid);
        var s = ctx[new NodeId("b")];
        Assert.Equal(new[]{1.0, 1.5, 2.0}, s.ToArray());
    }
}
