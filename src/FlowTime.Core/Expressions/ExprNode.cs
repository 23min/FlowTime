using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using System.Linq;
using FlowTime.Expressions;
using BinaryOpNode = FlowTime.Expressions.BinaryOpNode;
using FunctionCallNode = FlowTime.Expressions.FunctionCallNode;
using LiteralNode = FlowTime.Expressions.LiteralNode;
using ArrayLiteralNode = FlowTime.Expressions.ArrayLiteralNode;

namespace FlowTime.Core.Expressions;

/// <summary>
/// A node that represents a compiled expression.
/// This serves as a bridge between the expression system and the node graph system.
/// </summary>
public class ExprNode : INode
{
    private readonly ExpressionNode ast;
    
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }
    
    public ExprNode(string id, ExpressionNode ast, IEnumerable<NodeId> inputs)
    {
        Id = new NodeId(id);
        this.ast = ast;
        Inputs = inputs;
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        // For M1.5, we'll implement a simple evaluator that handles basic expressions
        return EvaluateExpression(ast, grid, getInput);
    }
    
    private Series EvaluateExpression(ExpressionNode expr, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        return expr switch
        {
            LiteralNode literal => CreateLiteralSeries(literal.Value, grid),
            NodeReferenceNode nodeRef => CloneSeries(getInput(new NodeId(nodeRef.NodeId))),
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
            "CONV" => EvaluateConvolutionFunction(node, grid, getInput),
            "MIN" => EvaluateMinFunction(node, grid, getInput),
            "MAX" => EvaluateMaxFunction(node, grid, getInput),
            "CLAMP" => EvaluateClampFunction(node, grid, getInput),
            "MOD" => EvaluateModFunction(node, grid, getInput),
            "FLOOR" => EvaluateUnaryMathFunction(node, grid, getInput, Math.Floor, "FLOOR"),
            "CEIL" => EvaluateUnaryMathFunction(node, grid, getInput, Math.Ceiling, "CEIL"),
            "ROUND" => EvaluateUnaryMathFunction(node, grid, getInput, v => Math.Round(v, MidpointRounding.AwayFromZero), "ROUND"),
            "STEP" => EvaluateStepFunction(node, grid, getInput),
            "PULSE" => EvaluatePulseFunction(node, grid, getInput),
            _ => throw new ArgumentException($"Unknown function: {node.FunctionName}")
        };
    }

    private static Series CloneSeries(Series source)
    {
        return new Series(source.ToArray());
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

    private Series EvaluateConvolutionFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("CONV function requires exactly 2 arguments: CONV(series, [kernel...])");
        }

        var sourceSeries = EvaluateExpression(node.Arguments[0], grid, getInput);
        var kernel = ExtractKernel(node.Arguments[1]);
        var result = new double[grid.Bins];

        if (kernel.Length == 0)
        {
            return new Series(result);
        }

        for (var t = 0; t < grid.Bins; t++)
        {
            double sum = 0;
            for (var k = 0; k < kernel.Length; k++)
            {
                var sourceIndex = t - k;
                if (sourceIndex < 0)
                {
                    break; // kernel is causal
                }

                if (sourceIndex >= sourceSeries.Length)
                {
                    continue;
                }

                var sample = sourceSeries[sourceIndex];
                if (!double.IsFinite(sample))
                {
                    continue;
                }

                sum += sample * kernel[k];
            }

            result[t] = sum;
        }

        return new Series(result);
    }

    private static double[] ExtractKernel(ExpressionNode kernelNode)
    {
        return kernelNode switch
        {
            ArrayLiteralNode array => array.Values.ToArray(),
            LiteralNode literal => new[] { literal.Value },
            _ => throw new ArgumentException("CONV kernel argument must be an array literal (e.g., [0.0, 0.6, 0.3, 0.1]).")
        };
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

    private Series EvaluateModFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("MOD function requires exactly 2 arguments: MOD(value, divisor)");
        }

        var value = EvaluateExpression(node.Arguments[0], grid, getInput);
        var divisor = EvaluateExpression(node.Arguments[1], grid, getInput);
        var result = new double[grid.Bins];

        for (var i = 0; i < grid.Bins; i++)
        {
            var d = divisor[i];
            result[i] = Math.Abs(d) <= double.Epsilon ? 0d : Modulo(value[i], d);
        }

        return new Series(result);
    }

    private Series EvaluateUnaryMathFunction(
        FunctionCallNode node,
        TimeGrid grid,
        Func<NodeId, Series> getInput,
        Func<double, double> op,
        string functionName)
    {
        if (node.Arguments.Count != 1)
        {
            throw new ArgumentException($"{functionName} function requires exactly 1 argument");
        }

        var operand = EvaluateExpression(node.Arguments[0], grid, getInput);
        var result = new double[grid.Bins];
        for (var i = 0; i < grid.Bins; i++)
        {
            result[i] = op(operand[i]);
        }

        return new Series(result);
    }

    private Series EvaluateStepFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count != 2)
        {
            throw new ArgumentException("STEP function requires 2 arguments: STEP(value, threshold)");
        }

        var value = EvaluateExpression(node.Arguments[0], grid, getInput);
        var threshold = EvaluateExpression(node.Arguments[1], grid, getInput);
        var result = new double[grid.Bins];

        for (var i = 0; i < grid.Bins; i++)
        {
            result[i] = value[i] >= threshold[i] ? 1d : 0d;
        }

        return new Series(result);
    }

    private Series EvaluatePulseFunction(FunctionCallNode node, TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (node.Arguments.Count is < 1 or > 3)
        {
            throw new ArgumentException("PULSE requires 1-3 arguments: PULSE(periodBins, phaseOffset?, amplitude?)");
        }

        if (node.Arguments[0] is not LiteralNode periodLiteral)
        {
            throw new ArgumentException("PULSE period must be a literal");
        }

        var period = (int)periodLiteral.Value;
        if (period <= 0 || Math.Abs(period - periodLiteral.Value) > double.Epsilon)
        {
            throw new ArgumentException("PULSE period must be a positive integer");
        }

        var phase = 0;
        if (node.Arguments.Count >= 2)
        {
            if (node.Arguments[1] is not LiteralNode phaseLiteral)
            {
                throw new ArgumentException("PULSE phaseOffset must be a literal");
            }

            phase = (int)phaseLiteral.Value;
            if (phase < 0 || Math.Abs(phase - phaseLiteral.Value) > double.Epsilon)
            {
                throw new ArgumentException("PULSE phaseOffset must be a non-negative integer");
            }
        }

        Series amplitudeSeries;
        if (node.Arguments.Count == 3)
        {
            amplitudeSeries = EvaluateExpression(node.Arguments[2], grid, getInput);
        }
        else
        {
            amplitudeSeries = CreateLiteralSeries(1d, grid);
        }

        var result = new double[grid.Bins];
        for (var i = 0; i < grid.Bins; i++)
        {
            var delta = i - phase;
            if (delta >= 0 && delta % period == 0)
            {
                result[i] = amplitudeSeries[i];
            }
            else
            {
                result[i] = 0d;
            }
        }

        return new Series(result);
    }

    private static double Modulo(double dividend, double divisor)
    {
        var remainder = dividend % divisor;
        if (remainder == 0 || Math.Sign(remainder) == Math.Sign(divisor))
        {
            return remainder;
        }

        return remainder + divisor;
    }
}
