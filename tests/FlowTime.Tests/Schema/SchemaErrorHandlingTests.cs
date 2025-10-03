using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for schema validation error messages and edge cases
/// Status: FAILING (RED) - Enhanced error handling doesn't exist yet
/// </summary>
public class SchemaErrorHandlingTests
{
    [Fact]
    public void ValidateModel_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange - Missing schemaVersion, binSize, and binUnit
        var yaml = @"
grid:
  bins: 24
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
        Assert.Contains(result.Errors, e => e.Contains("binSize"));
        Assert.Contains(result.Errors, e => e.Contains("binUnit"));
    }
    
    [Fact]
    public void ValidateModel_InvalidYaml_ReturnsParseError()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  result:
    const: [invalid yaml structure
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("parse") || e.Contains("YAML") || e.Contains("syntax"));
    }
    
    [Fact]
    public void ValidateModel_EmptyModel_ReturnsError()
    {
        // Arrange
        var yaml = "";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
    
    [Fact]
    public void ValidateModel_NullModel_ReturnsError()
    {
        // Act
        var result = ModelValidator.Validate(null!);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null") || e.Contains("empty"));
    }
    
    [Fact]
    public void ValidateModel_ErrorMessages_AreNotEmpty()
    {
        // Arrange - Use intentionally invalid schema to trigger validation errors
        var yaml = @"
grid:
  bins: 24
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        
        // Should have multiple validation errors (missing schemaVersion, binSize, binUnit)
        Assert.True(result.Errors.Count >= 2, "Should have multiple validation errors");
        
        foreach (var error in result.Errors)
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
            // Should have minimum descriptive content
            Assert.True(error.Length > 10, "Error messages should have minimum descriptive content");
        }
    }
    
    [Fact]
    public void ValidateModel_WithWarnings_StillValid()
    {
        // Arrange - Model is valid but might have warnings (e.g., unused nodes)
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  unused:
    const: 100.0
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
        // Warnings are different from errors
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void ValidateModel_CaseSensitivity_BinUnit()
    {
        // Arrange - binUnit should be lowercase
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: Hours
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert - Should still succeed (case-insensitive parsing)
        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void ValidateModel_ExtraFields_Ignored()
    {
        // Arrange - Extra unknown fields should be ignored (forward compatibility)
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
  futureField: someValue
nodes:
  result:
    const: 42.5
metadata:
  author: test
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void ValidateModel_InvalidBins_ReturnsSpecificError(int bins)
    {
        // Arrange
        var yaml = $@"
schemaVersion: 1
grid:
  bins: {bins}
  binSize: 1
  binUnit: hours
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("bins") && (e.Contains("range") || e.Contains("1") || e.Contains("10000")));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ValidateModel_InvalidBinSize_ReturnsSpecificError(int binSize)
    {
        // Arrange
        var yaml = $@"
schemaVersion: 1
grid:
  bins: 24
  binSize: {binSize}
  binUnit: hours
nodes:
  result:
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.Contains("binSize") && (e.Contains("range") || e.Contains("1") || e.Contains("1000")));
    }
}
