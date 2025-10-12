using FlowTime.Expressions;

namespace FlowTime.Expressions.Tests;

public class ExpressionValidationTests
{
    [Fact]
    public void HasSelfReferencingShift_ReturnsTrue_ForPositiveLag()
    {
        var ast = new ExpressionParser("MAX(0, SHIFT(queue_depth, 1) + arrivals)").Parse();
        var result = ExpressionSemanticValidator.HasSelfReferencingShift(ast, "queue_depth");
        Assert.True(result);
    }

    [Fact]
    public void HasSelfReferencingShift_IgnoresOtherNodes()
    {
        var ast = new ExpressionParser("SHIFT(other_node, 1) + queue_depth").Parse();
        var result = ExpressionSemanticValidator.HasSelfReferencingShift(ast, "queue_depth");
        Assert.False(result);
    }

    [Fact]
    public void HasSelfReferencingShift_ReturnsFalse_ForZeroLag()
    {
        var ast = new ExpressionParser("SHIFT(queue_depth, 0)").Parse();
        var result = ExpressionSemanticValidator.HasSelfReferencingShift(ast, "queue_depth");
        Assert.False(result);
    }

    [Fact]
    public void HasSelfReferencingShift_IsCaseInsensitive()
    {
        var ast = new ExpressionParser("shift(queue_depth, 1)").Parse();
        var result = ExpressionSemanticValidator.HasSelfReferencingShift(ast, "queue_depth");
        Assert.True(result);
    }

    [Fact]
    public void Validate_ReturnsError_WhenSelfShiftDetected()
    {
        var ast = new ExpressionParser("MAX(0, SHIFT(queue_depth, 1) + arrivals)").Parse();

        var result = ExpressionSemanticValidator.Validate(ast, "queue_depth");

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExpressionValidationErrorCodes.SelfShiftRequiresInitialCondition, error.Code);
        Assert.Equal("Expression node 'queue_depth' uses SHIFT on itself and requires an initial condition (topology.nodes[].initialCondition.queueDepth).", error.Message);
        Assert.Equal("queue_depth", error.NodeId);
    }

    [Fact]
    public void Validate_ReturnsSuccess_WhenNoSelfShiftDetected()
    {
        var ast = new ExpressionParser("SHIFT(other_node, 1) + queue_depth").Parse();

        var result = ExpressionSemanticValidator.Validate(ast, "queue_depth");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
