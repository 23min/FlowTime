using FlowTime.Core.Execution;
using FlowTime.Core.Expressions;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;

namespace FlowTime.Core.Tests.Expressions;

public class ExprNodeReferenceTests
{
    [Fact]
    public void Evaluate_NodeReference_ReturnsIndependentSeries()
    {
        var parser = new ExpressionParser("base");
        var ast = parser.Parse();
        var node = new ExprNode("alias", ast, new[] { new NodeId("base") });

        var grid = new TimeGrid(3, 1, TimeUnit.Minutes);
        var sourceSeries = new Series(new[] { 1.0, 2.0, 3.0 });

        Series Selector(NodeId _) => sourceSeries;

        var result = node.Evaluate(grid, Selector);

        Assert.NotSame(sourceSeries, result);
        Assert.Equal(sourceSeries.ToArray(), result.ToArray());

        // Series is immutable — mutating the copied array does not affect the source
        var resultArray = result.ToArray();
        resultArray[0] = 10.0;
        Assert.Equal(1.0, sourceSeries[0]);
        Assert.Equal(1.0, result[0]);
    }
}
