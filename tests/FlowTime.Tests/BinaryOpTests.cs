using FlowTime.Core;

public class BinaryOpTests
{
    [Fact]
    public void SeriesTimesSeries_Works()
    {
        var grid = new TimeGrid(3, 60);
        var a = new ConstSeriesNode("a", new double[]{2,3,4});
        var c = new ConstSeriesNode("c", new double[]{5,6,7});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("c"), BinOp.Mul);
        var g = new Graph(new INode[]{ a, c, b });
        var s = g.Evaluate(grid)[new NodeId("b")];
        Assert.Equal(new[]{10.0,18.0,28.0}, s.ToArray());
    }

    [Fact]
    public void SeriesPlusSeries_Works()
    {
        var grid = new TimeGrid(3, 60);
        var a = new ConstSeriesNode("a", new double[]{1,2,3});
        var c = new ConstSeriesNode("c", new double[]{4,5,6});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("c"), BinOp.Add);
        var g = new Graph(new INode[]{ a, c, b });
        var s = g.Evaluate(grid)[new NodeId("b")];
        Assert.Equal(new[]{5.0,7.0,9.0}, s.ToArray());
    }

    [Fact]
    public void MissingInput_ThrowsKeyNotFound()
    {
        var grid = new TimeGrid(2, 60);
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("c"), BinOp.Add);
        var g = new Graph(new INode[]{ b });
        Assert.Throws<KeyNotFoundException>(() => g.Evaluate(grid));
    }

    [Fact]
    public void ScalarRight_IgnoresRightDependency()
    {
        var grid = new TimeGrid(2, 60);
        var a = new ConstSeriesNode("a", new double[]{1,1});
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("nonexistent"), BinOp.Add, 2.0);
        var g = new Graph(new INode[]{ a, b });
        var s = g.Evaluate(grid)[new NodeId("b")];
        Assert.Equal(new[]{3.0,3.0}, s.ToArray());
    }
}
