using FlowTime.Expressions;

namespace FlowTime.Expressions.Tests;

public class ExpressionParserTests
{
    [Fact]
    public void ParseLiteral_ReturnsLiteralNode()
    {
        var parser = new ExpressionParser("42.5");
        var result = parser.Parse();
        
        var literal = Assert.IsType<LiteralNode>(result);
        Assert.Equal(42.5, literal.Value, precision: 5);
    }
    
    [Fact]
    public void ParseNodeReference_ReturnsNodeReferenceNode()
    {
        var parser = new ExpressionParser("demand");
        var result = parser.Parse();
        
        var nodeRef = Assert.IsType<NodeReferenceNode>(result);
        Assert.Equal("demand", nodeRef.NodeId);
    }
    
    [Theory]
    [InlineData("a + b", BinaryOperator.Add)]
    [InlineData("a - b", BinaryOperator.Subtract)]  
    [InlineData("a * b", BinaryOperator.Multiply)]
    [InlineData("a / b", BinaryOperator.Divide)]
    public void ParseBinaryOperation_ReturnsCorrectOperator(string expression, BinaryOperator expectedOp)
    {
        var parser = new ExpressionParser(expression);
        var result = parser.Parse();
        
        var binaryOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(expectedOp, binaryOp.Operator);
        
        var left = Assert.IsType<NodeReferenceNode>(binaryOp.Left);
        Assert.Equal("a", left.NodeId);
        
        var right = Assert.IsType<NodeReferenceNode>(binaryOp.Right);
        Assert.Equal("b", right.NodeId);
    }
    
    [Fact]
    public void ParseOperatorPrecedence_MultiplicationBeforeAddition()
    {
        var parser = new ExpressionParser("a + b * c");
        var result = parser.Parse();
        
        // Should parse as: a + (b * c)
        var addOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Add, addOp.Operator);
        
        var left = Assert.IsType<NodeReferenceNode>(addOp.Left);
        Assert.Equal("a", left.NodeId);
        
        var right = Assert.IsType<BinaryOpNode>(addOp.Right);
        Assert.Equal(BinaryOperator.Multiply, right.Operator);
    }
    
    [Fact]
    public void ParseOperatorPrecedence_DivisionBeforeSubtraction()
    {
        var parser = new ExpressionParser("a - b / c");
        var result = parser.Parse();
        
        // Should parse as: a - (b / c)
        var subOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Subtract, subOp.Operator);
        
        var left = Assert.IsType<NodeReferenceNode>(subOp.Left);
        Assert.Equal("a", left.NodeId);
        
        var right = Assert.IsType<BinaryOpNode>(subOp.Right);
        Assert.Equal(BinaryOperator.Divide, right.Operator);
    }
    
    [Fact]
    public void ParseParentheses_OverridesPrecedence()
    {
        var parser = new ExpressionParser("(a + b) * c");
        var result = parser.Parse();
        
        // Should parse as: (a + b) * c
        var mulOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Multiply, mulOp.Operator);
        
        var left = Assert.IsType<BinaryOpNode>(mulOp.Left);
        Assert.Equal(BinaryOperator.Add, left.Operator);
        
        var right = Assert.IsType<NodeReferenceNode>(mulOp.Right);
        Assert.Equal("c", right.NodeId);
    }
    
    [Fact]
    public void ParseFunctionCallNoArgs_ReturnsFunctionCallNode()
    {
        var parser = new ExpressionParser("func()");
        var result = parser.Parse();
        
        var funcCall = Assert.IsType<FunctionCallNode>(result);
        Assert.Equal("func", funcCall.FunctionName);
        Assert.Empty(funcCall.Arguments);
    }
    
    [Fact]
    public void ParseFunctionCallOneArg_ReturnsFunctionCallNode()
    {
        var parser = new ExpressionParser("SHIFT(demand, 1)");
        var result = parser.Parse();
        
        var funcCall = Assert.IsType<FunctionCallNode>(result);
        Assert.Equal("SHIFT", funcCall.FunctionName);
        Assert.Equal(2, funcCall.Arguments.Count);
        
        var arg1 = Assert.IsType<NodeReferenceNode>(funcCall.Arguments[0]);
        Assert.Equal("demand", arg1.NodeId);
        
        var arg2 = Assert.IsType<LiteralNode>(funcCall.Arguments[1]);
        Assert.Equal(1.0, arg2.Value);
    }
    
    [Fact]
    public void ParseFunctionCallMultipleArgs_ReturnsFunctionCallNode()
    {
        var parser = new ExpressionParser("CLAMP(value, 0.0, 1.0)");
        var result = parser.Parse();
        
        var funcCall = Assert.IsType<FunctionCallNode>(result);
        Assert.Equal("CLAMP", funcCall.FunctionName);
        Assert.Equal(3, funcCall.Arguments.Count);
        
        Assert.IsType<NodeReferenceNode>(funcCall.Arguments[0]);
        Assert.IsType<LiteralNode>(funcCall.Arguments[1]);
        Assert.IsType<LiteralNode>(funcCall.Arguments[2]);
    }
    
    [Fact]
    public void ParseComplexExpression_HandlesNesting()
    {
        var parser = new ExpressionParser("(demand + SHIFT(demand, 1)) * 0.8");
        var result = parser.Parse();
        
        var mulOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Multiply, mulOp.Operator);
        
        var left = Assert.IsType<BinaryOpNode>(mulOp.Left);
        Assert.Equal(BinaryOperator.Add, left.Operator);
        
        var addLeft = Assert.IsType<NodeReferenceNode>(left.Left);
        Assert.Equal("demand", addLeft.NodeId);
        
        var addRight = Assert.IsType<FunctionCallNode>(left.Right);
        Assert.Equal("SHIFT", addRight.FunctionName);
    }
    
    [Fact]
    public void ParseWhitespace_IsIgnored()
    {
        var parser = new ExpressionParser("  a  +  b  ");
        var result = parser.Parse();
        
        var addOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Add, addOp.Operator);
        
        var left = Assert.IsType<NodeReferenceNode>(addOp.Left);
        Assert.Equal("a", left.NodeId);
        
        var right = Assert.IsType<NodeReferenceNode>(addOp.Right);
        Assert.Equal("b", right.NodeId);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseEmptyExpression_ThrowsException(string expression)
    {
        var parser = new ExpressionParser(expression);
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.Contains("Unexpected character", ex.Message);
    }
    
    [Theory]
    [InlineData("42.")]
    [InlineData(".42")]
    public void ParseInvalidNumber_ThrowsException(string expression)
    {
        var parser = new ExpressionParser(expression);
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.True(ex.Position >= 0);
    }
    
    [Theory]
    [InlineData("(a + b")]
    public void ParseMismatchedParentheses_ThrowsException(string expression)
    {
        var parser = new ExpressionParser(expression);
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.Contains("Expected ')'", ex.Message);
    }
    
    [Fact]
    public void ParseUnexpectedCloseParen_ThrowsException()
    {
        var parser = new ExpressionParser("a + b)");
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.Contains("Unexpected character", ex.Message);
    }
    
    [Theory]
    [InlineData("func(")]
    [InlineData("func(a,)")]
    [InlineData("func(a b)")]
    public void ParseInvalidFunctionCall_ThrowsException(string expression)
    {
        var parser = new ExpressionParser(expression);
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.True(ex.Position >= 0);
    }
    
    [Theory]
    [InlineData("@invalid")]
    [InlineData("a + @")]
    [InlineData("42 a")]
    public void ParseInvalidSyntax_ThrowsException(string expression)
    {
        var parser = new ExpressionParser(expression);
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.True(ex.Position >= 0);
    }
    
    [Fact]
    public void ParseException_IncludesPositionInformation()
    {
        var parser = new ExpressionParser("a + @invalid");
        
        var ex = Assert.Throws<ExpressionParseException>(() => parser.Parse());
        Assert.Equal(4, ex.Position); // Position of '@'
        Assert.Equal("a + @invalid", ex.Expression);
        Assert.Contains("Parse error at position 4", ex.Message);
    }
    
    [Fact]
    public void ParseAssociativity_LeftToRight()
    {
        var parser = new ExpressionParser("a - b - c");
        var result = parser.Parse();
        
        // Should parse as: (a - b) - c
        var subOp = Assert.IsType<BinaryOpNode>(result);
        Assert.Equal(BinaryOperator.Subtract, subOp.Operator);
        
        var left = Assert.IsType<BinaryOpNode>(subOp.Left);
        Assert.Equal(BinaryOperator.Subtract, left.Operator);
        
        var right = Assert.IsType<NodeReferenceNode>(subOp.Right);
        Assert.Equal("c", right.NodeId);
    }
}
