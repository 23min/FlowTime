using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for complete target schema validation (binSize/binUnit format).
/// m-E23-02: migrated from <c>ModelValidator</c> to <c>ModelSchemaValidator</c>.
/// Fixtures use canonical array-form <c>nodes:</c>; assertions are semantic
/// (substring on the load-bearing token) rather than pinned to legacy phrasing.
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
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [100.0]
  - id: result
    kind: expr
    expr: demand * 1.5
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateModel_LegacyBinMinutes_ReturnsError()
    {
        // Arrange - Legacy format must be rejected.
        // Schema's grid block declares additionalProperties: false and requires binSize+binUnit,
        // so a binMinutes-only grid trips multiple errors that together name all three fields.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binMinutes: 60
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - errors collectively name all three fields. Each token appears in at
        // least one error string.
        Assert.False(result.IsValid);
        var joined = string.Join("\n", result.Errors);
        Assert.Contains("binMinutes", joined);
        Assert.Contains("binSize", joined);
        Assert.Contains("binUnit", joined);
    }

    [Fact]
    public void ValidateModel_MissingBinSize_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
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
        Assert.Contains(result.Errors, e => e.Contains("binSize"));
    }

    [Fact]
    public void ValidateModel_MissingBinUnit_ReturnsError()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

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
  bins: 1
  binSize: 1
  binUnit: {unit}
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
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
  bins: 1
  binSize: 1
  binUnit: {unit}
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - schema enum keyword ensures binUnit is one of the four valid units.
        // Error text from JsonEverything is "Value should match one of the values specified by the enum"
        // — we assert the load-bearing token (binUnit) appears in the error rather than the
        // exact valid-unit list (which the new shape does not enumerate verbatim).
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("binUnit"));
    }

    [Fact]
    public void ValidateModel_ExprNode_UsesExprField()
    {
        // Arrange - Target schema uses "expr" not "expression"
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: expr
    expr: 42.5 * 2
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void ValidateModel_LegacyExpressionField_ReturnsError()
    {
        // Arrange - Legacy "expression" field is rejected by the schema's expr-arm
        // additionalProperties: false. With kind: expr, the schema's 5-arm oneOf at
        // nodes[].items requires `expr` and forbids `expression`.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: result
    kind: expr
    expression: 42.5 * 2
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - errors collectively name both the legacy field and the canonical replacement.
        Assert.False(result.IsValid);
        var joined = string.Join("\n", result.Errors);
        Assert.Contains("expression", joined);
        Assert.Contains("expr", joined);
    }

    [Fact]
    public void ValidateModel_LegacyArrivalsRouteSchema_ReturnsError()
    {
        // Arrange - old schema format with top-level arrivals and route.
        // The canonical schema's root additionalProperties: false rejects both fields.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 3
  binSize: 1
  binUnit: hours
arrivals:
  kind: const
  values: [10, 20, 30]
route:
  id: TEST
nodes:
  - id: result
    kind: const
    values: [42.5, 42.5, 42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - the JSON-schema error reads "/arrivals: All values fail against the false schema"
        // and "/route: All values fail against the false schema". The field names appear; the
        // precise "not supported" phrasing is gone (legacy ModelValidator wording) — bucket (c)
        // semantic relax. We assert the field names surface as errors.
        Assert.False(result.IsValid, "Legacy arrivals/route schema should be rejected");
        Assert.Contains(result.Errors, e => e.Contains("arrivals"));
        Assert.Contains(result.Errors, e => e.Contains("route"));
    }

    [Fact]
    public void ValidateModel_PmfNode_WithTargetSchema_Succeeds()
    {
        // Arrange - PMF node uses a single sample (bins=1) so the values length adjunct is N/A
        // and the PMF probability/uniqueness adjuncts are satisfied (3 distinct values).
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: pmf
    pmf:
      values: [100, 200, 300]
      probabilities: [0.3, 0.5, 0.2]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void ValidateModel_MixedNodeTypes_Succeeds()
    {
        // Arrange
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: baseLoad
    kind: const
    values: [100.0]
  - id: variability
    kind: pmf
    pmf:
      values: [0.9, 1.0, 1.1]
      probabilities: [0.3, 0.4, 0.3]
  - id: demand
    kind: expr
    expr: baseLoad * variability
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
    }
}
