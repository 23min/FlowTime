using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for complete target schema validation (binSize/binUnit format)
/// Status: FAILING (RED) - Target schema validation doesn't exist yet
/// </summary>
public class TargetSchemaValidationTests
{
    [Fact]
    public void ValidateModel_CompleteTargetSchema_Succeeds()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  demand:
    const: 100.0
  result:
    expr: demand * 1.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void ValidateModel_LegacyBinMinutes_ReturnsError()
    {
        // Arrange - Legacy format should be rejected
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binMinutes: 60
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("binMinutes") && e.Contains("binSize") && e.Contains("binUnit"));
    }
    
    [Fact]
    public void ValidateModel_MissingBinSize_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binUnit: hours
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("binSize"));
    }
    
    [Fact]
    public void ValidateModel_MissingBinUnit_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("binUnit"));
    }
    
    [Theory]
    [InlineData("minutes")]
    [InlineData("hours")]
    [InlineData("days")]
    [InlineData("weeks")]
    public void ValidateModel_ValidTimeUnits_Succeeds(string unit)
    {
        // Arrange
        var yaml = $@"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: {unit}
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Theory]
    [InlineData("seconds")]
    [InlineData("months")]
    [InlineData("years")]
    [InlineData("invalid")]
    public void ValidateModel_InvalidTimeUnit_ReturnsError(string unit)
    {
        // Arrange
        var yaml = $@"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: {unit}
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("binUnit") && 
            (e.Contains("minutes") || e.Contains("hours") || 
             e.Contains("days") || e.Contains("weeks")));
    }
    
    [Fact]
    public void ValidateModel_ExprNode_UsesExprField()
    {
        // Arrange - Target schema uses "expr" not "expression"
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  result:
    expr: 42.5 * 2
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateModel_LegacyExpressionField_ReturnsError()
    {
        // Arrange - Legacy "expression" field should be rejected
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  result:
    expression: 42.5 * 2
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("expression") && e.Contains("expr"));
    }
    
    [Fact]
    public void ValidateModel_PmfNode_WithTargetSchema_Succeeds()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  demand:
    pmf:
      - value: 100
        probability: 0.3
      - value: 200
        probability: 0.5
      - value: 300
        probability: 0.2
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateModel_MixedNodeTypes_Succeeds()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 168
  binSize: 1
  binUnit: hours
nodes:
  baseLoad:
    const: 100.0
  variability:
    pmf:
      - value: 0.9
        probability: 0.3
      - value: 1.0
        probability: 0.4
      - value: 1.1
        probability: 0.3
  demand:
    expr: baseLoad * variability
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
    }
}
