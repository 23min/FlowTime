using FlowTime.Core.Expressions;

namespace FlowTime.Tests.Expressions;

public class DebugParserTests
{
    [Fact]
    public void DebugSimpleIdentifier()
    {
        var parser = new ExpressionParser("a");
        var result = parser.Parse();
        
        var nodeRef = Assert.IsType<NodeReferenceNode>(result);
        Assert.Equal("a", nodeRef.NodeId);
    }
    
    [Fact]  
    public void DebugIdentifierWithSpace()
    {
        var parser = new ExpressionParser("a ");
        var result = parser.Parse();
        
        var nodeRef = Assert.IsType<NodeReferenceNode>(result);
        Assert.Equal("a", nodeRef.NodeId);
    }
    
    [Fact]
    public void DebugBinaryOp()
    {
        var parser = new ExpressionParser("a+b");
        var result = parser.Parse();
        
        var binaryOp = Assert.IsType<BinaryOpNode>(result);
        var left = Assert.IsType<NodeReferenceNode>(binaryOp.Left);
        var right = Assert.IsType<NodeReferenceNode>(binaryOp.Right);
        
        Assert.Equal("a", left.NodeId);
        Assert.Equal("b", right.NodeId);
    }
    
    [Fact]
    public void DebugBinaryOpWithSpaces()
    {
        var parser = new ExpressionParser("a + b");
        var result = parser.Parse();
        
        var binaryOp = Assert.IsType<BinaryOpNode>(result);
        var left = Assert.IsType<NodeReferenceNode>(binaryOp.Left);
        var right = Assert.IsType<NodeReferenceNode>(binaryOp.Right);
        
        // Debug what we actually get
        System.Console.WriteLine($"Left NodeId: '{left.NodeId}' (length: {left.NodeId.Length})");
        System.Console.WriteLine($"Right NodeId: '{right.NodeId}' (length: {right.NodeId.Length})");
        
        Assert.Equal("a", left.NodeId);
        Assert.Equal("b", right.NodeId);
    }
}
