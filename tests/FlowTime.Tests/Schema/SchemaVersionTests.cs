using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for schemaVersion validation (target schema requires version 1)
/// Tests validate that ModelValidator properly enforces schemaVersion: 1 requirement.
/// Last 2 tests are skipped because they test non-existent serialization APIs.
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
    public void ValidateModel_SchemaVersionAsString_AcceptsIfParseable()
    {
        // Arrange - YAML parsers may accept '1' as valid integer
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
        
        // Assert - Lenient: accept string '1' since it's parseable as integer 1
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
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
    
    // FUTURE TESTS: When ModelDefinition gets SchemaVersion property and ModelSerializer exists
    // 
    // [Fact]
    // public void ParseModel_WithSchemaVersion1_SetsProperty()
    // {
    //     // Arrange
    //     var yaml = @"
    // schemaVersion: 1
    // grid:
    //   bins: 24
    //   binSize: 1
    //   binUnit: hours
    // nodes:
    //   result:
    //     const: 42.5
    // ";
    //     
    //     // Act
    //     var model = Core.Models.ModelParser.ParseModel(yaml);
    //     
    //     // Assert
    //     Assert.Equal(1, model.SchemaVersion);
    // }
    // 
    // [Fact]
    // public void SerializeModel_IncludesSchemaVersion()
    // {
    //     // Arrange - Would need Model class and ModelSerializer
    //     var grid = new Core.TimeGrid(24, 1, Core.TimeUnit.Hours);
    //     var nodes = new Dictionary<string, Core.INode>
    //     {
    //         ["result"] = new Core.ConstSeriesNode(new NodeId("result"), ...)
    //     };
    //     var model = new Core.Model(grid, nodes) { SchemaVersion = 1 };
    //     
    //     // Act
    //     var yaml = Core.ModelSerializer.Serialize(model);
    //     
    //     // Assert
    //     Assert.Contains("schemaVersion: 1", yaml);
    // }
}
