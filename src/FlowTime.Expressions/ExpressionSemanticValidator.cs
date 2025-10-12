namespace FlowTime.Expressions;

/// <summary>
/// Provides semantic validation helpers for FlowTime expressions.
/// </summary>
public static class ExpressionSemanticValidator
{
    /// <summary>
    /// Determines whether the supplied AST contains a SHIFT call that references the same node id with a positive lag.
    /// </summary>
    public static bool HasSelfReferencingShift(ExpressionNode ast, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(ast);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id must be provided.", nameof(nodeId));
        }

        var detector = new SelfShiftDetector(nodeId);
        ast.Accept(detector);
        return detector.HasSelfShift;
    }

    private sealed class SelfShiftDetector : IExpressionVisitor<object?>
    {
        private readonly string nodeId;

        public SelfShiftDetector(string nodeId)
        {
            this.nodeId = nodeId;
        }

        public bool HasSelfShift { get; private set; }

        public object? VisitBinaryOp(BinaryOpNode node)
        {
            if (HasSelfShift) return null;

            node.Left.Accept(this);
            if (HasSelfShift) return null;

            node.Right.Accept(this);
            return null;
        }

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            if (HasSelfShift) return null;

            if (string.Equals(node.FunctionName, "SHIFT", StringComparison.OrdinalIgnoreCase) &&
                node.Arguments.Count == 2 &&
                node.Arguments[0] is NodeReferenceNode referenceNode &&
                string.Equals(referenceNode.NodeId, nodeId, StringComparison.Ordinal))
            {
                if (node.Arguments[1] is LiteralNode literal && literal.Value > 0)
                {
                    HasSelfShift = true;
                    return null;
                }
            }

            foreach (var argument in node.Arguments)
            {
                argument.Accept(this);
                if (HasSelfShift) break;
            }

            return null;
        }

        public object? VisitNodeReference(NodeReferenceNode node) => null;

        public object? VisitLiteral(LiteralNode node) => null;
    }
}
