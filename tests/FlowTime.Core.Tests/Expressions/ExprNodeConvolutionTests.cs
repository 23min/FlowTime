using System;
using FlowTime.Core.Execution;
using FlowTime.Core.Expressions;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;

namespace FlowTime.Core.Tests.Expressions;

public class ExprNodeConvolutionTests
{
    [Fact]
    public void EvaluateConvolution_ComputesCausalSum()
    {
        var parser = new ExpressionParser("CONV(base, [0.0, 0.6, 0.3, 0.1])");
        var ast = parser.Parse();
        var node = new ExprNode("retry", ast, new[] { new NodeId("base") });

        var grid = new TimeGrid(5, 1, TimeUnit.Hours);
        var sourceSeries = new Series(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Series ResultSelector(NodeId _) => sourceSeries;

        var result = node.Evaluate(grid, ResultSelector);
        var values = result.ToArray();

        Assert.Equal(5, values.Length);
        Assert.Equal(0.0, values[0], 6);
        Assert.Equal(0.6, values[1], 6);
        Assert.Equal(1.5, values[2], 6);
        Assert.Equal(2.5, values[3], 6);
        Assert.Equal(3.5, values[4], 6);
    }

    [Fact]
    public void EvaluateConvolution_AllowsScalarKernel()
    {
        var parser = new ExpressionParser("CONV(base, 0.5)");
        var ast = parser.Parse();
        var node = new ExprNode("half", ast, new[] { new NodeId("base") });

        var grid = new TimeGrid(3, 1, TimeUnit.Minutes);
        var sourceSeries = new Series(new[] { 2.0, 4.0, 6.0 });

        Series ResultSelector(NodeId _) => sourceSeries;

        var result = node.Evaluate(grid, ResultSelector);
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, result.ToArray());
    }

    [Fact]
    public void EvaluateConvolution_InvalidKernel_Throws()
    {
        var parser = new ExpressionParser("CONV(base, SHIFT(other, 1))");
        var ast = parser.Parse();
        var node = new ExprNode("invalid", ast, new[] { new NodeId("base"), new NodeId("other") });

        var grid = new TimeGrid(2, 1, TimeUnit.Minutes);
        var series = new Series(new[] { 1.0, 2.0 });

        Series ResultSelector(NodeId _) => series;

        Assert.Throws<ArgumentException>(() => node.Evaluate(grid, ResultSelector));
    }
}
