using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for schemaVersion validation (target schema requires version 1)
/// Status: FAILING (RED) - Schema version validation doesn't exist yet
/// </summary>
public class SchemaVersionTests
{
    [Fact]
    public void ValidateModel_WithSchemaVersion1_Succeeds()
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
    const: 42.5
";
        
        // Act
        var result = ModelValidator.Validate(yaml);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void ValidateModel_MissingSchemaVersion_ReturnsError()
    {
        // Arrange
        var yaml = @"
grid:
  bins: 24
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
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
        Assert.Contains(result.Errors, e => e.Contains("required"));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(999)]
    public void ValidateModel_InvalidSchemaVersion_ReturnsError(int version)
    {
        // Arrange
        var yaml = $@"
schemaVersion: {version}
grid:
  bins: 24
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
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
        Assert.Contains(result.Errors, e => e.Contains("1"));
    }
    
    [Fact]
    public void ValidateModel_SchemaVersionAsString_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: '1'
grid:
  bins: 24
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
            e.Contains("schemaVersion") && e.Contains("integer"));
    }
    
    [Fact]
    public void ValidateModel_SchemaVersionNull_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: null
grid:
  bins: 24
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
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
    }
    
    [Fact]
    public void ParseModel_WithSchemaVersion1_SetsProperty()
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
    const: 42.5
";
        
        // Act
        var model = Core.ModelParser.Parse(yaml);
        
        // Assert
        Assert.Equal(1, model.SchemaVersion);
    }
    
    [Fact]
    public void SerializeModel_IncludesSchemaVersion()
    {
        // Arrange
        var grid = new Core.TimeGrid(24, 1, Core.TimeUnit.Hours);
        var nodes = new Dictionary<string, Core.INode>
        {
            ["result"] = new Core.ConstNode("result", 42.5)
        };
        var model = new Core.Model(grid, nodes) { SchemaVersion = 1 };
        
        // Act
        var yaml = Core.ModelSerializer.Serialize(model);
        
        // Assert
        Assert.Contains("schemaVersion: 1", yaml);
    }
}
