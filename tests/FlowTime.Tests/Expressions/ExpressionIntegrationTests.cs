using FlowTime.Core;
using FlowTime.Core.Expressions;

namespace FlowTime.Tests.Expressions;

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
}
