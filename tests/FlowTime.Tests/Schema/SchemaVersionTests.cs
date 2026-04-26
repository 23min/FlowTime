using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for schemaVersion validation (target schema requires version 1).
/// m-E23-02: migrated from <c>ModelValidator</c> to <c>ModelSchemaValidator</c>.
/// Fixtures updated to canonical array-form <c>nodes:</c>; assertions are semantic
/// (substring on the load-bearing token <c>schemaVersion</c>) — phrasing of the
/// JSON-schema-shaped errors differs from the legacy flat strings but the
/// load-bearing token is preserved.
/// </summary>
public class SchemaVersionTests
{
    [Fact]
    public void ValidateModel_WithSchemaVersion1_Succeeds()
    {
        // Arrange - bins = 1 so the const-values length adjunct (m-E23-01) is satisfied.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateModel_MissingSchemaVersion_ReturnsError()
    {
        // Arrange
        var yaml = @"
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - JSON-schema-shaped error reads "Required properties [\"schemaVersion\"] are not present".
        // Both the field name and the "Required" keyword are preserved in the new shape.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
        Assert.Contains(result.Errors, e =>
            e.Contains("Required", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("required", StringComparison.OrdinalIgnoreCase));
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
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - schema enforces `const: 1`. The error text reads:
        //   /schemaVersion: Expected "1"
        // The "1" appears as a string literal from the schema's `const` keyword;
        // assertion stays semantic-on-token.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
        Assert.Contains(result.Errors, e => e.Contains("1"));
    }

    [Fact]
    public void ValidateModel_SchemaVersionAsString_AcceptsIfParseable()
    {
        // Arrange - YAML-stringified '1' is rejected by the strict integer-typed schema.
        // Bucket (d) reframe — the legacy ModelValidator was lenient (TryConvertToInt
        // accepted strings); the canonical schema declares schemaVersion as type:integer
        // const:1, so a stringified '1' fails the type check. The behavior change is
        // intentional and aligned with the schema-as-truth contract.
        var yaml = @"
schemaVersion: '1'
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - strict schema rejects the stringified form.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void ValidateModel_SchemaVersionNull_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: null
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

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
