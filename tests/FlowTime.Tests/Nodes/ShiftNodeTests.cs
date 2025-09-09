using FlowTime.Core;
using FlowTime.Core.Nodes;

namespace FlowTime.Tests.Nodes;

public class ShiftNodeTests
{
    private readonly TimeGrid _grid = new(Bins: 5, BinMinutes: 60);
    
    [Fact]
    public void ShiftNode_Lag0_ReturnsIdentity()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [1.0, 2.0, 3.0, 4.0, 5.0]);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 0);
        
        // Act
        var result = shiftNode.Evaluate(_grid, _ => sourceNode.Evaluate(_grid, _ => throw new InvalidOperationException()));
        
        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal(1.0, result[0]);
        Assert.Equal(2.0, result[1]);
        Assert.Equal(3.0, result[2]);
        Assert.Equal(4.0, result[3]);
        Assert.Equal(5.0, result[4]);
    }
    
    [Fact]
    public void ShiftNode_Lag1_ShiftsOneBin()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [10.0, 20.0, 30.0, 40.0, 50.0]);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 1);
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "source")
                return sourceNode.Evaluate(_grid, _ => throw new InvalidOperationException());
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = shiftNode.Evaluate(_grid, getInput);
        
        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal(0.0, result[0]);    // First bin is zero
        Assert.Equal(10.0, result[1]);   // Second bin gets first source value
        Assert.Equal(20.0, result[2]);   // Third bin gets second source value
        Assert.Equal(30.0, result[3]);   // Fourth bin gets third source value
        Assert.Equal(40.0, result[4]);   // Fifth bin gets fourth source value
    }
    
    [Fact]
    public void ShiftNode_Lag2_ShiftsTwoBins()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [100.0, 200.0, 300.0, 400.0, 500.0]);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 2);
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "source")
                return sourceNode.Evaluate(_grid, _ => throw new InvalidOperationException());
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = shiftNode.Evaluate(_grid, getInput);
        
        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal(0.0, result[0]);     // First bin is zero
        Assert.Equal(0.0, result[1]);     // Second bin is zero
        Assert.Equal(100.0, result[2]);   // Third bin gets first source value
        Assert.Equal(200.0, result[3]);   // Fourth bin gets second source value
        Assert.Equal(300.0, result[4]);   // Fifth bin gets third source value
    }
    
    [Fact]
    public void ShiftNode_LagLargerThanSeries_ReturnsAllZeros()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [1.0, 2.0, 3.0]);
        var grid = new TimeGrid(Bins: 3, BinMinutes: 60);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 5);
        
        // Act
        var getInput = (NodeId id) => {
            if (id.Value == "source")
                return sourceNode.Evaluate(grid, _ => throw new InvalidOperationException());
            throw new ArgumentException($"Unknown node: {id}");
        };
        var result = shiftNode.Evaluate(grid, getInput);
        
        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal(0.0, result[0]);
        Assert.Equal(0.0, result[1]);
        Assert.Equal(0.0, result[2]);
    }
    
    [Fact]
    public void ShiftNode_NegativeLag_ThrowsException()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [1.0, 2.0, 3.0]);
        
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new ShiftNode("shifted", sourceNode, lag: -1));
        Assert.Contains("non-negative", ex.Message);
    }
    
    [Fact]
    public void ShiftNode_Inputs_ReturnsSourceNodeId()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [1.0, 2.0, 3.0]);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 1);
        
        // Act
        var inputs = shiftNode.Inputs.ToList();
        
        // Assert
        Assert.Single(inputs);
        Assert.Equal("source", inputs[0].Value);
    }
    
    [Fact]
    public void ShiftNode_InitializeState_ClearsHistory()
    {
        // Arrange
        var sourceNode = new ConstSeriesNode("source", [1.0, 2.0, 3.0, 4.0, 5.0]);
        var shiftNode = new ShiftNode("shifted", sourceNode, lag: 2);
        
        // Act - call InitializeState (should not throw)
        shiftNode.InitializeState(_grid);
        
        // Assert - verify it doesn't crash and can still evaluate
        var getInput = (NodeId id) => sourceNode.Evaluate(_grid, _ => throw new InvalidOperationException());
        var result = shiftNode.Evaluate(_grid, getInput);
        Assert.Equal(5, result.Length);
    }
}
