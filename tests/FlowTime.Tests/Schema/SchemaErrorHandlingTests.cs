using FlowTime.Core;

namespace FlowTime.Tests.Schema;

/// <summary>
/// Tests for schema validation error messages and edge cases.
/// m-E23-02: migrated from <c>ModelValidator</c> to <c>ModelSchemaValidator</c>.
/// Fixtures use canonical array-form <c>nodes:</c>; assertions are semantic
/// (substring on the load-bearing token) rather than pinned to legacy phrasing,
/// since the JSON-schema-shaped error format is the new contract.
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
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - each missing required field surfaces as a distinct error message.
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
  - id: result
    kind: const
    values: [invalid yaml structure
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("YAML", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("syntax", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("Validation error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateModel_EmptyModel_ReturnsError()
    {
        // Arrange
        var yaml = "";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateModel_NullModel_ReturnsError()
    {
        // Act
        var result = ModelSchemaValidator.Validate(null!);

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
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

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
        // Arrange - Model is valid (multiple unrelated nodes are allowed at the schema layer).
        // Fixture is updated to match grid.bins so the m-E23-01 const-values cross-array
        // adjunct does not trip; preserves the original test intent (extra unused node is
        // structurally allowed).
        var yaml = @"
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
nodes:
  - id: unused
    kind: const
    values: [100.0]
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert
        Assert.True(result.IsValid, $"Model should be valid; errors: {string.Join("; ", result.Errors)}");
        // Warnings are different from errors
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateModel_CaseSensitivity_BinUnit()
    {
        // Arrange - binUnit must be lowercase under the canonical schema (enum: minutes/hours/days/weeks).
        // Note: This was previously asserted as "still valid" against ModelValidator's lenient
        // case-insensitive parsing. The canonical schema is strict — bucket (d) reframe.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: Hours
nodes:
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - Strict schema enum rejects 'Hours' (case-sensitive).
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("binUnit"));
    }

    [Fact]
    public void ValidateModel_ExtraFields_Ignored()
    {
        // Arrange - Note: under the canonical schema, top-level `additionalProperties: false`
        // rejects unknown root fields. ModelValidator was lenient (silent ignore for forward
        // compatibility); ModelSchemaValidator is strict. The schema-shape contract change
        // is intentional. Test reframed: extra fields under a path that DOES allow them
        // (e.g., grid.binSize is not the test target — we use a known-strict location).
        // The original lenient-extra-fields contract is gone; this reframed test asserts
        // that the validator surfaces extra-field errors against the strict schema.
        var yaml = @"
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
  futureField: someValue
nodes:
  - id: result
    kind: const
    values: [42.5]
metadata:
  author: test
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - strict schema rejects unknown fields at both grid and root.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("futureField") || e.Contains("metadata"));
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
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - schema enforces bins minimum: 1, maximum: 10000.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("bins"));
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
  - id: result
    kind: const
    values: [42.5]
";

        // Act
        var result = ModelSchemaValidator.Validate(yaml);

        // Assert - schema enforces binSize minimum: 1, maximum: 1000.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("binSize"));
    }
}
