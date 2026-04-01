using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Expressions;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;

namespace FlowTime.Expressions.Tests;

public class ExpressionIntegrationTests
{
    private readonly TimeGrid grid = new(bins: 4, binSize: 60, binUnit: TimeUnit.Minutes);
    
    [Fact]
    public void ParseAndEvaluate_SimpleLiteral_ReturnsConstantSeries()
    {
        // Arrange
        var parser = new ExpressionParser("42.5");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");
        
        // Act
        var result = exprNode.Evaluate(grid, _ => throw new ArgumentException("No inputs expected"));
        
        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(42.5, result[0]);
        Assert.Equal(42.5, result[1]);
        Assert.Equal(42.5, result[2]);
        Assert.Equal(42.5, result[3]);
    }
    
    [Fact]
    public void ParseAndEvaluate_NodeReference_ReturnsSourceSeries()
    {
        // Arrange
        var sourceData = new double[] { 10.0, 20.0, 30.0, 40.0 };
        var parser = new ExpressionParser("demand");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "demand")
                return new Series(sourceData);
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = exprNode.Evaluate(grid, getInput);
        
        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(10.0, result[0]);
        Assert.Equal(20.0, result[1]);
        Assert.Equal(30.0, result[2]);
        Assert.Equal(40.0, result[3]);
    }
    
    [Fact]
    public void ParseAndEvaluate_Addition_ReturnsSumSeries()
    {
        // Arrange
        var sourceA = new double[] { 1.0, 2.0, 3.0, 4.0 };
        var sourceB = new double[] { 10.0, 20.0, 30.0, 40.0 };
        var parser = new ExpressionParser("a + b");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "a") return new Series(sourceA);
            if (id.Value == "b") return new Series(sourceB);
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = exprNode.Evaluate(grid, getInput);
        
        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(11.0, result[0]); // 1 + 10
        Assert.Equal(22.0, result[1]); // 2 + 20
        Assert.Equal(33.0, result[2]); // 3 + 30
        Assert.Equal(44.0, result[3]); // 4 + 40
    }
    
    [Fact]
    public void ParseAndEvaluate_ShiftFunction_ReturnsLaggedSeries()
    {
        // Arrange
        var sourceData = new double[] { 10.0, 20.0, 30.0, 40.0 };
        var parser = new ExpressionParser("SHIFT(demand, 1)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "demand")
                return new Series(sourceData);
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = exprNode.Evaluate(grid, getInput);
        
        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(0.0, result[0]);    // No previous value, defaults to 0
        Assert.Equal(10.0, result[1]);   // Previous index 0 value
        Assert.Equal(20.0, result[2]);   // Previous index 1 value
        Assert.Equal(30.0, result[3]);   // Previous index 2 value
    }

    [Fact]
    public void ParseAndEvaluate_ComplexExpression_CombinesAllFeatures()
    {
        // Arrange: (a + b) * 2 + SHIFT(c, 1)
        var sourceA = new double[] { 1.0, 2.0, 3.0, 4.0 };
        var sourceB = new double[] { 2.0, 3.0, 4.0, 5.0 };
        var sourceC = new double[] { 100.0, 200.0, 300.0, 400.0 };
        var parser = new ExpressionParser("(a + b) * 2 + SHIFT(c, 1)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "a") return new Series(sourceA);
            if (id.Value == "b") return new Series(sourceB);
            if (id.Value == "c") return new Series(sourceC);
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = exprNode.Evaluate(grid, getInput);
        
        // Assert
        Assert.Equal(4, result.Length);
        // Index 0: (1+2)*2 + 0 = 6 + 0 = 6
        Assert.Equal(6.0, result[0]);
        // Index 1: (2+3)*2 + 100 = 10 + 100 = 110
        Assert.Equal(110.0, result[1]);
        // Index 2: (3+4)*2 + 200 = 14 + 200 = 214
        Assert.Equal(214.0, result[2]);
        // Index 3: (4+5)*2 + 300 = 18 + 300 = 318
        Assert.Equal(318.0, result[3]);
    }

    [Fact]
    public void ParseAndEvaluate_ModFunction_ReturnsRemainder()
    {
        var sourceA = new double[] { 5.5, 6.0, 7.75, 9.1 };
        var parser = new ExpressionParser("MOD(a, 2)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        var getInput = (NodeId id) =>
        {
            if (id.Value == "a") return new Series(sourceA);
            throw new ArgumentException($"Unknown node: {id}");
        };

        var result = exprNode.Evaluate(grid, getInput);

        var expected = new[] { 1.5, 0.0, 1.75, 1.1 };
        var actual = result.ToArray();
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 6);
        }
    }

    [Fact]
    public void ParseAndEvaluate_FloorCeilRoundFunctions_WorkPerBin()
    {
        var series = new double[] { 1.2, 3.8, -2.4, -3.5 };

        Series Eval(string expr)
        {
            var parser = new ExpressionParser(expr);
            var ast = parser.Parse();
            var node = ExpressionCompiler.Compile(ast, "result");
            return node.Evaluate(grid, id =>
            {
                if (id.Value == "x") return new Series(series);
                throw new ArgumentException();
            });
        }

        Assert.Equal(new[] { 1.0, 3.0, -3.0, -4.0 }, Eval("FLOOR(x)").ToArray());
        Assert.Equal(new[] { 2.0, 4.0, -2.0, -3.0 }, Eval("CEIL(x)").ToArray());
        Assert.Equal(new[] { 1.0, 4.0, -2.0, -4.0 }, Eval("ROUND(x)").ToArray());
    }

    [Fact]
    public void ParseAndEvaluate_StepFunction_GatesSeries()
    {
        var source = new double[] { 0.2, 0.5, 0.75, 1.1 };
        var parser = new ExpressionParser("STEP(utilization, 0.75)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        var result = exprNode.Evaluate(grid, id =>
        {
            if (id.Value == "utilization") return new Series(source);
            throw new ArgumentException();
        });

        Assert.Equal(new[] { 0.0, 0.0, 1.0, 1.0 }, result.ToArray());
    }

    [Fact]
    public void ParseAndEvaluate_PulseFunction_WithLiteralAmplitude()
    {
        var parser = new ExpressionParser("PULSE(2, 1, 5)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        var result = exprNode.Evaluate(grid, _ => throw new ArgumentException());

        Assert.Equal(new[] { 0.0, 5.0, 0.0, 5.0 }, result.ToArray());
    }

    [Fact]
    public void ParseAndEvaluate_PulseFunction_WithSeriesAmplitude()
    {
        var source = new double[] { 10, 20, 30, 40 };
        var parser = new ExpressionParser("PULSE(2, 0, demand)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        var result = exprNode.Evaluate(grid, id =>
        {
            if (id.Value == "demand") return new Series(source);
            throw new ArgumentException();
        });

        Assert.Equal(new[] { 10.0, 0.0, 30.0, 0.0 }, result.ToArray());
    }

    // ──────────────────────────────────────────────────
    // Edge-case tests for expression functions
    // ──────────────────────────────────────────────────

    [Fact]
    public void ParseAndEvaluate_ModFunction_NegativeDivisor()
    {
        var result = Eval("MOD(a, b)",
            ("a", new double[] { 7, -7, 7, -7 }),
            ("b", new double[] { -3, -3, 3, 3 }));

        // MOD uses floored modulo (result has same sign as divisor)
        Assert.Equal(-2.0, result[0], 6); // 7 mod -3 = -2
        Assert.Equal(-1.0, result[1], 6); // -7 mod -3 = -1
        Assert.Equal(1.0, result[2], 6);  // 7 mod 3 = 1
        Assert.Equal(2.0, result[3], 6);  // -7 mod 3 = 2
    }

    [Fact]
    public void ParseAndEvaluate_ModFunction_SeriesDivisor()
    {
        var result = Eval("MOD(a, b)",
            ("a", new double[] { 10, 15, 20, 25 }),
            ("b", new double[] { 3, 4, 7, 6 }));

        Assert.Equal(1.0, result[0], 6);  // 10 mod 3
        Assert.Equal(3.0, result[1], 6);  // 15 mod 4
        Assert.Equal(6.0, result[2], 6);  // 20 mod 7
        Assert.Equal(1.0, result[3], 6);  // 25 mod 6
    }

    [Fact]
    public void ParseAndEvaluate_StepFunction_SeriesThreshold()
    {
        var result = Eval("STEP(x, threshold)",
            ("x", new double[] { 0.5, 0.8, 0.3, 1.0 }),
            ("threshold", new double[] { 0.6, 0.6, 0.6, 0.6 }));

        Assert.Equal(new[] { 0.0, 1.0, 0.0, 1.0 }, result);
    }

    [Fact]
    public void ParseAndEvaluate_StepFunction_EqualToThreshold_ReturnsOne()
    {
        var result = Eval("STEP(x, 0.5)",
            ("x", new double[] { 0.5, 0.5, 0.5, 0.5 }));

        // value >= threshold, so exactly equal should return 1.0
        Assert.Equal(new[] { 1.0, 1.0, 1.0, 1.0 }, result);
    }

    [Fact]
    public void ParseAndEvaluate_PulseFunction_Period1_AlwaysOn()
    {
        var parser = new ExpressionParser("PULSE(1, 0, 5)");
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        var result = exprNode.Evaluate(grid, _ => throw new ArgumentException());

        // Period 1 means every bin is a pulse bin
        Assert.Equal(new[] { 5.0, 5.0, 5.0, 5.0 }, result.ToArray());
    }

    [Fact]
    public void ParseAndEvaluate_CombinedExpressions_FloorOfMod()
    {
        var result = Eval("FLOOR(MOD(x, 3))",
            ("x", new double[] { 7.5, 8.2, 3.0, 11.9 }));

        // MOD(7.5, 3) = 1.5 → FLOOR = 1.0
        // MOD(8.2, 3) = 2.2 → FLOOR = 2.0
        // MOD(3.0, 3) = 0.0 → FLOOR = 0.0
        // MOD(11.9, 3) = 2.9 → FLOOR = 2.0
        Assert.Equal(1.0, result[0], 6);
        Assert.Equal(2.0, result[1], 6);
        Assert.Equal(0.0, result[2], 6);
        Assert.Equal(2.0, result[3], 6);
    }

    [Fact]
    public void ParseAndEvaluate_CombinedExpressions_CeilOfDivision()
    {
        var result = Eval("CEIL(a / b)",
            ("a", new double[] { 7, 10, 1, 0 }),
            ("b", new double[] { 3, 4, 3, 5 }));

        // 7/3 = 2.333 → CEIL = 3.0
        // 10/4 = 2.5 → CEIL = 3.0
        // 1/3 = 0.333 → CEIL = 1.0
        // 0/5 = 0.0 → CEIL = 0.0
        Assert.Equal(3.0, result[0]);
        Assert.Equal(3.0, result[1]);
        Assert.Equal(1.0, result[2]);
        Assert.Equal(0.0, result[3]);
    }

    // ──────────────────────────────────────────────────
    // Helper
    // ──────────────────────────────────────────────────

    private double[] Eval(string expr, params (string name, double[] values)[] inputs)
    {
        var testGrid = new TimeGrid(inputs[0].values.Length, 60, TimeUnit.Minutes);
        var parser = new ExpressionParser(expr);
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        Series GetInput(NodeId id)
        {
            foreach (var (name, values) in inputs)
            {
                if (id.Value == name) return new Series(values);
            }
            throw new KeyNotFoundException($"Unknown input: {id.Value}");
        }

        return exprNode.Evaluate(testGrid, GetInput).ToArray();
    }
}
