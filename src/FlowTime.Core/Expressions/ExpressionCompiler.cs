using FlowTime.Core.Nodes;
using FlowTime.Expressions;
using BinaryOpNode = FlowTime.Expressions.BinaryOpNode;
using FunctionCallNode = FlowTime.Expressions.FunctionCallNode;
using NodeReferenceNode = FlowTime.Expressions.NodeReferenceNode;
using LiteralNode = FlowTime.Expressions.LiteralNode;
using ArrayLiteralNode = FlowTime.Expressions.ArrayLiteralNode;

namespace FlowTime.Core.Expressions;

/// <summary>
/// Compiles expression ASTs into executable node graphs.
/// </summary>
public class ExpressionCompiler
{
    /// <summary>
    /// Compile an expression AST into a node that can be evaluated.
    /// </summary>
    public static INode Compile(ExpressionNode ast, string nodeId)
    {
        var (sameBin, lagged) = FindClassifiedReferences(ast);
        // Only same-bin references are graph edges (for topological sort).
        // Lagged references (inside SHIFT with lag >= 1) are resolved at
        // evaluation time from previous bins — they don't create dependencies.
        var inputs = sameBin.Select(name => new NodeId(name)).ToList();
        var hasLaggedRefs = lagged.Count > 0;
        return new ExprNode(nodeId, ast, inputs, hasLaggedRefs);
    }

    /// <summary>
    /// Classify node references as same-bin (direct dependencies) or lagged
    /// (inside SHIFT with lag >= 1). If a reference appears in both contexts,
    /// same-bin wins — the direct dependency must be satisfied.
    /// </summary>
    public static (HashSet<string> SameBin, HashSet<string> Lagged) FindClassifiedReferences(ExpressionNode ast)
    {
        var visitor = new ClassifiedRefFinder();
        ast.Accept(visitor);

        // If a ref appears in both sets, same-bin wins
        visitor.LaggedReferences.ExceptWith(visitor.SameBinReferences);

        return (visitor.SameBinReferences, visitor.LaggedReferences);
    }

    /// <summary>
    /// Find all node references in an expression AST (legacy — returns all refs).
    /// </summary>
    private static HashSet<string> FindNodeReferences(ExpressionNode ast)
    {
        var visitor = new NodeRefFinder();
        ast.Accept(visitor);
        return visitor.NodeReferences;
    }

    private class ClassifiedRefFinder : IExpressionVisitor<object?>
    {
        public HashSet<string> SameBinReferences { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LaggedReferences { get; } = new(StringComparer.OrdinalIgnoreCase);
        private bool insideLaggedShift;

        public object? VisitBinaryOp(BinaryOpNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            if (string.Equals(node.FunctionName, "SHIFT", StringComparison.OrdinalIgnoreCase)
                && node.Arguments.Count == 2
                && node.Arguments[1] is LiteralNode lagLiteral
                && lagLiteral.Value >= 1)
            {
                // First argument is evaluated at a lagged time — refs are lagged
                var prev = insideLaggedShift;
                insideLaggedShift = true;
                node.Arguments[0].Accept(this);
                insideLaggedShift = prev;
                // Don't visit the lag literal — it's not a reference
                return null;
            }

            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            return null;
        }

        public object? VisitNodeReference(NodeReferenceNode node)
        {
            if (insideLaggedShift)
                LaggedReferences.Add(node.NodeId);
            else
                SameBinReferences.Add(node.NodeId);
            return null;
        }

        public object? VisitLiteral(LiteralNode node) => null;
        public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
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

        public object? VisitLiteral(LiteralNode node) => null;
        public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
    }
}
