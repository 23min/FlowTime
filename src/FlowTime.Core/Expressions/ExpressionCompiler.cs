using FlowTime.Core.Nodes;

namespace FlowTime.Core.Expressions;

/// <summary>
/// Compiles expression ASTs into executable node graphs.
/// </summary>
public class ExpressionCompiler
{
    /// <summary>
    /// Compile an expression AST into a node that can be evaluated.
    /// </summary>
    /// <param name="ast">The expression AST to compile</param>
    /// <param name="nodeId">The ID for the resulting node</param>
    /// <returns>A compiled node implementing the expression</returns>
    public static INode Compile(ExpressionNode ast, string nodeId)
    {
        var inputs = FindNodeReferences(ast).Select(name => new NodeId(name)).ToList();
        return new ExprNode(nodeId, ast, inputs);
    }
    
    /// <summary>
    /// Find all node references in an expression AST.
    /// </summary>
    private static HashSet<string> FindNodeReferences(ExpressionNode ast)
    {
        var visitor = new NodeRefFinder();
        ast.Accept(visitor);
        return visitor.NodeReferences;
    }
    
    private class NodeRefFinder : IExpressionVisitor<object?>
    {
        public HashSet<string> NodeReferences { get; } = new();
        
        public object? VisitBinaryOp(BinaryOpNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }
        
        public object? VisitFunctionCall(FunctionCallNode node)
        {
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            return null;
        }
        
        public object? VisitNodeReference(NodeReferenceNode node)
        {
            NodeReferences.Add(node.NodeId);
            return null;
        }
        
        public object? VisitLiteral(LiteralNode node)
        {
            // Literals don't reference nodes
            return null;
        }
    }
}
