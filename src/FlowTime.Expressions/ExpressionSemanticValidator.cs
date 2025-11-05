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

    /// <summary>
    /// Validate semantic rules that must hold for the supplied expression AST.
    /// </summary>
    public static ExpressionValidationResult Validate(ExpressionNode ast, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(ast);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id must be provided.", nameof(nodeId));
        }

        if (!HasSelfReferencingShift(ast, nodeId))
        {
            return ExpressionValidationResult.Success;
        }

        var error = new ExpressionValidationError(
            ExpressionValidationErrorCodes.SelfShiftRequiresInitialCondition,
            $"Expression node '{nodeId}' uses SHIFT on itself and requires an initial condition (topology.nodes[].initialCondition.queueDepth).",
            nodeId);

        return ExpressionValidationResult.FromErrors(new[] { error });
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

        public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
    }
}

/// <summary>
/// Represents the outcome of evaluating semantic validation rules.
/// </summary>
public sealed class ExpressionValidationResult
{
    private static readonly ExpressionValidationResult success = new(Array.Empty<ExpressionValidationError>());

    private ExpressionValidationResult(IReadOnlyList<ExpressionValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Indicates whether the expression passed all validation checks.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Collection of validation errors (empty when <see cref="IsValid"/> is true).
    /// </summary>
    public IReadOnlyList<ExpressionValidationError> Errors { get; }

    /// <summary>
    /// Reusable success instance.
    /// </summary>
    public static ExpressionValidationResult Success => success;

    /// <summary>
    /// Create a result from the provided error set.
    /// </summary>
    public static ExpressionValidationResult FromErrors(IReadOnlyList<ExpressionValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Count == 0 ? success : new ExpressionValidationResult(errors);
    }
}

/// <summary>
/// Represents a specific semantic validation failure.
/// </summary>
public sealed record ExpressionValidationError(string Code, string Message, string NodeId);

/// <summary>
/// Constants describing validation error codes.
/// </summary>
public static class ExpressionValidationErrorCodes
{
    /// <summary>
    /// Indicates an expression uses SHIFT on itself without guaranteeing initialization.
    /// </summary>
    public const string SelfShiftRequiresInitialCondition = "SELF_SHIFT_REQUIRES_INITIAL_CONDITION";
}
