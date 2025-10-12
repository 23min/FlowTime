using FlowTime.Core.Expressions;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;

namespace FlowTime.Core.Tests.Expressions;

public class ExpressionCompilerTests
{
    [Fact]
    public void ExpressionCompiler_UsesSharedNodes()
    {
        var ast = new ExpressionParser("a + b").Parse();

        var compiled = ExpressionCompiler.Compile(ast, "sum");

        var exprNode = Assert.IsType<ExprNode>(compiled);
        Assert.Equal("sum", exprNode.Id.Value);

        var inputs = exprNode.Inputs.Select(id => id.Value).ToList();
        Assert.Equal(2, inputs.Count);
        Assert.Contains("a", inputs);
        Assert.Contains("b", inputs);
    }
}
