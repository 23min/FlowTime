using FlowTime.Core;

namespace FlowTime.Tests.ModelValidation;

public class ValidationTests
{
    [Fact]
    public void ConstSeries_LengthMismatch_Throws()
    {
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var node = new ConstSeriesNode("a", new double[] { 1, 2 });
        var g = new Graph(new INode[] { node });
        var ex = Assert.Throws<ArgumentException>(() => g.Evaluate(grid));
        Assert.Contains("values length", ex.Message);
    }

    [Fact]
    public void CycleDetection_TwoNodeCycle_Throws()
    {
        var grid = new TimeGrid(2, 60, TimeUnit.Minutes);
        var a = new BinaryOpNode("a", new NodeId("b"), new NodeId("__scalar__"), BinOp.Add, 1);
        var b = new BinaryOpNode("b", new NodeId("a"), new NodeId("__scalar__"), BinOp.Add, 1);
        Assert.Throws<InvalidOperationException>(() => new Graph(new INode[] { a, b }));
    }
}
