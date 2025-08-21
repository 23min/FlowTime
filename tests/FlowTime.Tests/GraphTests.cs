using FlowTime.Core;

public class GraphTests
{
    [Fact]
    public void TopoOrder_NoCycles_Works()
    {
        var grid = new TimeGrid(4, 60);
        var a = new ConstSeriesNode("a", new double[]{1,1,1,1});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 2);
        var g = new Graph(new INode[]{ a, b });
        var order = g.TopologicalOrder();
        Assert.Equal([new NodeId("a"), new NodeId("b")], order);
    }

    [Fact]
    public void Evaluate_MulScalar_ProducesExpected()
    {
        var grid = new TimeGrid(3, 60);
        var a = new ConstSeriesNode("a", new double[]{2,3,4});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 0.5);
        var g = new Graph(new INode[]{ a, b });
        var ctx = g.Evaluate(grid);
        var s = ctx[new NodeId("b")];
        Assert.Equal(new[]{1.0, 1.5, 2.0}, s.ToArray());
    }
}
