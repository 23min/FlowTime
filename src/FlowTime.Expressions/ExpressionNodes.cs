namespace FlowTime.Expressions;

/// <summary>
/// Base class for all expression AST nodes.
/// </summary>
public abstract class ExpressionNode
{
    /// <summary>
    /// Position information for error reporting.
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Accept a visitor for traversing the AST.
    /// </summary>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}

/// <summary>
/// Visitor pattern interface for traversing expression ASTs.
/// </summary>
public interface IExpressionVisitor<T>
{
    T VisitBinaryOp(BinaryOpNode node);
    T VisitFunctionCall(FunctionCallNode node);
    T VisitNodeReference(NodeReferenceNode node);
    T VisitLiteral(LiteralNode node);
}

/// <summary>
/// Binary operation node (+, -, *, /).
/// </summary>
public class BinaryOpNode : ExpressionNode
{
    public BinaryOperator Operator { get; set; }
    public ExpressionNode Left { get; set; } = null!;
    public ExpressionNode Right { get; set; } = null!;
    
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitBinaryOp(this);
}

/// <summary>
/// Function call node (e.g., SHIFT(demand, 1), MIN(a, b)).
/// </summary>
public class FunctionCallNode : ExpressionNode
{
    public string FunctionName { get; set; } = string.Empty;
    public List<ExpressionNode> Arguments { get; set; } = new();
    
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitFunctionCall(this);
}

/// <summary>
/// Node reference (e.g., demand, capacity, served).
/// </summary>
public class NodeReferenceNode : ExpressionNode
{
    public string NodeId { get; set; } = string.Empty;
    
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitNodeReference(this);
}

/// <summary>
/// Literal numeric value node.
/// </summary>
public class LiteralNode : ExpressionNode
{
    public double Value { get; set; }
    
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitLiteral(this);
}

/// <summary>
/// Binary operators supported in expressions.
/// </summary>
public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide
}
