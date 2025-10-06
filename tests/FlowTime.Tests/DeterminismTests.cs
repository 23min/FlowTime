using FlowTime.Core;

public class DeterminismTests
{
    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var grid = new TimeGrid(4, 60, TimeUnit.Minutes);
        var a = new ConstSeriesNode("a", new double[]{1,2,3,4});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Mul, 2);
        var g = new Graph(new INode[]{ a, b });
        var ctx1 = g.Evaluate(grid);
        var ctx2 = g.Evaluate(grid);
        Assert.Equal(ctx1[new NodeId("b")].ToArray(), ctx2[new NodeId("b")].ToArray());
    }

    [Fact]
    public void AddScalar_Works()
    {
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var a = new ConstSeriesNode("a", new double[]{1,1,1});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Add, 0.5);
        var g = new Graph(new INode[]{ a, b });
        var s = g.Evaluate(grid)[new NodeId("b")];
        Assert.Equal(new[]{1.5,1.5,1.5}, s.ToArray());
    }
}
