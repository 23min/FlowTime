using FlowTime.TimeMachine.Validation;

namespace FlowTime.TimeMachine.Tests.Validation;

public sealed class TimeMachineValidatorTests
{
    // Minimal valid model YAML used across multiple test cases.
    // Uses only const nodes (no serviceWithBuffer/queue that need inflow/outflow).
    private const string MinimalValidYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: Source
            kind: const
            values: [10, 10, 10, 10]
        """;

    // YAML that fails JSON schema validation (missing required fields).
    private const string InvalidSchemaYaml = """
        schemaVersion: 999
        nodes: []
        """;

    // YAML with a reference to an undeclared class (tier 1 failure).
    private const string BadClassRefYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        classes:
          - id: regular
        nodes:
          - id: Source
            kind: const
            values: [10, 10, 10, 10]
        traffic:
          arrivals:
            - nodeId: Source
              classId: premium
              pattern:
                kind: constant
                ratePerBin: 10
        """;


    #region Null / Empty

    [Fact]
    public void Validate_NullYaml_ReturnsInvalid_ForAllTiers()
    {
        foreach (var tier in Enum.GetValues<ValidationTier>())
        {
            var result = TimeMachineValidator.Validate(null!, tier);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void Validate_EmptyYaml_ReturnsInvalid_ForAllTiers()
    {
        foreach (var tier in Enum.GetValues<ValidationTier>())
        {
            var result = TimeMachineValidator.Validate("   ", tier);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }
    }

    #endregion

    #region Tier 1 — Schema

    [Fact]
    public void Validate_ValidModel_Schema_IsValid()
    {
        var result = TimeMachineValidator.Validate(MinimalValidYaml, ValidationTier.Schema);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(ValidationTier.Schema, result.Tier);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidSchema_ReturnsErrors()
    {
        var result = TimeMachineValidator.Validate(InvalidSchemaYaml, ValidationTier.Schema);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_BadClassRef_Schema_ReturnsErrors()
    {
        var result = TimeMachineValidator.Validate(BadClassRefYaml, ValidationTier.Schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("premium", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("class", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Tier 2 — Compile

    [Fact]
    public void Validate_ValidModel_Compile_IsValid()
    {
        var result = TimeMachineValidator.Validate(MinimalValidYaml, ValidationTier.Compile);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(ValidationTier.Compile, result.Tier);
    }

    [Fact]
    public void Validate_InvalidSchema_Compile_PropagatesToTier2()
    {
        // A schema error reported at tier 2 (tier 1 runs first)
        var result = TimeMachineValidator.Validate(InvalidSchemaYaml, ValidationTier.Compile);
        Assert.False(result.IsValid);
        Assert.Equal(ValidationTier.Compile, result.Tier);
    }

    [Fact]
    public void Validate_Compile_IncludesTier1Checks()
    {
        // Tier 2 (compile) is a superset of tier 1 — schema errors surface at tier 2 too.
        var tier1 = TimeMachineValidator.Validate(BadClassRefYaml, ValidationTier.Schema);
        var tier2 = TimeMachineValidator.Validate(BadClassRefYaml, ValidationTier.Compile);
        Assert.False(tier1.IsValid);
        Assert.False(tier2.IsValid);
        // Both tiers should report an error for the undeclared class ref
        Assert.NotEmpty(tier2.Errors);
    }

    #endregion

    #region Tier 3 — Analyse

    [Fact]
    public void Validate_ValidModel_Analyse_IsValid()
    {
        var result = TimeMachineValidator.Validate(MinimalValidYaml, ValidationTier.Analyse);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(ValidationTier.Analyse, result.Tier);
    }

    [Fact]
    public void Validate_InvalidSchema_Analyse_PropagatesToTier3()
    {
        var result = TimeMachineValidator.Validate(InvalidSchemaYaml, ValidationTier.Analyse);
        Assert.False(result.IsValid);
        Assert.Equal(ValidationTier.Analyse, result.Tier);
    }

    [Fact]
    public void Validate_ValidModel_Analyse_ReturnsWarningsNotErrors()
    {
        // Warnings from invariant analysis should be in Warnings, not Errors
        var result = TimeMachineValidator.Validate(MinimalValidYaml, ValidationTier.Analyse);
        Assert.True(result.IsValid);   // warnings don't make it invalid
        Assert.Empty(result.Errors);
        // Warnings may or may not be present depending on the model
    }

    #endregion

    #region ValidationResult shape

    [Fact]
    public void ValidationResult_Valid_HasCorrectShape()
    {
        var result = ValidationResult.Valid(ValidationTier.Schema);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal(ValidationTier.Schema, result.Tier);
    }

    [Fact]
    public void ValidationResult_Invalid_HasCorrectShape()
    {
        var result = ValidationResult.Invalid(ValidationTier.Compile, ["one", "two"]);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("one", result.Errors[0].Message);
    }

    #endregion
}
