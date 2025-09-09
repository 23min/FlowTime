namespace FlowTime.Core.Expressions;

/// <summary>
/// A node that represents a compiled expression.
/// This serves as a bridge between the expression system and the node graph system.
/// </summary>
public class ExprNode : INode
{
    private readonly ExpressionNode _ast;
    
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }
    
    public ExprNode(string id, ExpressionNode ast, IEnumerable<NodeId> inputs)
    {
        Id = new NodeId(id ?? throw new ArgumentNullException(nameof(id)));
        _ast = ast ?? throw new ArgumentNullException(nameof(ast));
        Inputs = inputs?.ToList() ?? throw new ArgumentNullException(nameof(inputs));
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        // For M1.5, we'll implement a simple evaluator that handles basic expressions
        return EvaluateExpression(_ast, grid, getInput);
    }
    
    private Series EvaluateExpression(ExpressionNode expr, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        return expr switch
        {
            LiteralNode literal => CreateLiteralSeries(literal.Value, grid),
            NodeReferenceNode nodeRef => getInput(new NodeId(nodeRef.NodeId)),
            BinaryOpNode binOp => EvaluateBinaryOp(binOp, grid, getInput),
            FunctionCallNode funcCall => EvaluateFunctionCall(funcCall, grid, getInput),
            _ => throw new ArgumentException($"Unsupported expression node type: {expr.GetType()}")
        };
    }
    
    private Series CreateLiteralSeries(double value, TimeGrid grid)
    {
        var values = new double[grid.Bins];
        Array.Fill(values, value);
        return new Series(values);
    }
    
    private Series EvaluateBinaryOp(BinaryOpNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var left = EvaluateExpression(node.Left, grid, getInput);
        var right = EvaluateExpression(node.Right, grid, getInput);
        
        var result = new double[grid.Bins];
        
        for (int i = 0; i < grid.Bins; i++)
        {
            result[i] = node.Operator switch
            {
                BinaryOperator.Add => left[i] + right[i],
                BinaryOperator.Subtract => left[i] - right[i],
                BinaryOperator.Multiply => left[i] * right[i],
                BinaryOperator.Divide => right[i] != 0.0 ? left[i] / right[i] : 0.0, // Handle division by zero
                _ => throw new ArgumentException($"Unsupported operator: {node.Operator}")
            };
        }
        
        return new Series(result);
    }
    
    private Series EvaluateFunctionCall(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        return node.FunctionName.ToUpperInvariant() switch
        {
            "SHIFT" => EvaluateShiftFunction(node, grid, getInput),
            "MIN" => EvaluateMinFunction(node, grid, getInput),
            "MAX" => EvaluateMaxFunction(node, grid, getInput),
            "CLAMP" => EvaluateClampFunction(node, grid, getInput),
            _ => throw new ArgumentException($"Unknown function: {node.FunctionName}")
        };
    }
    
    private Series EvaluateShiftFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("SHIFT function requires exactly 2 arguments: SHIFT(series, lag)");
        }
        
        var sourceSeries = EvaluateExpression(node.Arguments[0], grid, getInput);
        
        if (node.Arguments[1] is not LiteralNode lagLiteral)
        {
            throw new ArgumentException("SHIFT lag parameter must be a literal integer");
        }
        
        var lag = (int)lagLiteral.Value;
        if (lag != lagLiteral.Value || lag < 0)
        {
            throw new ArgumentException("SHIFT lag parameter must be a non-negative integer");
        }
        
        var result = new double[grid.Bins];
        
        for (int i = 0; i < grid.Bins; i++)
        {
            if (i < lag)
            {
                result[i] = 0.0; // Leading zeros
            }
            else
            {
                result[i] = sourceSeries[i - lag]; // Shifted values
            }
        }
        
        return new Series(result);
    }
    
    private Series EvaluateMinFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("MIN function requires exactly 2 arguments");
        }
        
        var left = EvaluateExpression(node.Arguments[0], grid, getInput);
        var right = EvaluateExpression(node.Arguments[1], grid, getInput);
        
        var result = new double[grid.Bins];
        for (int i = 0; i < grid.Bins; i++)
        {
            result[i] = Math.Min(left[i], right[i]);
        }
        
        return new Series(result);
    }
    
    private Series EvaluateMaxFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("MAX function requires exactly 2 arguments");
        }
        
        var left = EvaluateExpression(node.Arguments[0], grid, getInput);
        var right = EvaluateExpression(node.Arguments[1], grid, getInput);
        
        var result = new double[grid.Bins];
        for (int i = 0; i < grid.Bins; i++)
        {
            result[i] = Math.Max(left[i], right[i]);
        }
        
        return new Series(result);
    }
    
    private Series EvaluateClampFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 3)
        {
            throw new ArgumentException("CLAMP function requires exactly 3 arguments: CLAMP(value, min, max)");
        }
        
        var value = EvaluateExpression(node.Arguments[0], grid, getInput);
        var min = EvaluateExpression(node.Arguments[1], grid, getInput);
        var max = EvaluateExpression(node.Arguments[2], grid, getInput);
        
        var result = new double[grid.Bins];
        for (int i = 0; i < grid.Bins; i++)
        {
            result[i] = Math.Max(min[i], Math.Min(max[i], value[i]));
        }
        
        return new Series(result);
    }
}
