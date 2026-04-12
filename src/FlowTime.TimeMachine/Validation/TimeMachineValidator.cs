using FlowTime.Contracts.Services;
using FlowTime.Core;
using FlowTime.Core.Compiler;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Analysis;

namespace FlowTime.TimeMachine.Validation;

/// <summary>
/// Client-agnostic tiered model validation.
///
/// Callers: Sim UI, Blazor UI, Svelte UI, MCP servers, AI agents, tests, CI.
/// No caller is privileged — all get the same three-tier surface.
///
/// Tiers are cumulative: each tier performs all checks from the tier(s) before it.
/// </summary>
public static class TimeMachineValidator
{
    /// <summary>
    /// Validate a model YAML at the specified tier.
    /// Returns 200 (always) — errors and warnings are in the result, not in HTTP status.
    /// </summary>
    /// <param name="yaml">Raw model YAML string.</param>
    /// <param name="tier">The validation depth.</param>
    public static ValidationResult Validate(string yaml, ValidationTier tier)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return ValidationResult.Invalid(tier, ["Model YAML cannot be null or empty."]);
        }

        return tier switch
        {
            ValidationTier.Schema => ValidateSchema(yaml),
            ValidationTier.Compile => ValidateCompile(yaml),
            ValidationTier.Analyse => ValidateAnalyse(yaml),
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };
    }

    private static ValidationResult ValidateSchema(string yaml)
    {
        var errors = new List<string>();

        // Schema + class refs
        var schemaResult = ModelSchemaValidator.Validate(yaml);
        errors.AddRange(schemaResult.Errors);

        // Structure + legacy field detection
        var structResult = ModelValidator.Validate(yaml);
        errors.AddRange(structResult.Errors);

        if (errors.Count > 0)
        {
            return ValidationResult.Invalid(ValidationTier.Schema, errors);
        }

        return ValidationResult.Valid(ValidationTier.Schema);
    }

    private static ValidationResult ValidateCompile(string yaml)
    {
        // Tier 1 first
        var schemaResult = ValidateSchema(yaml);
        if (!schemaResult.IsValid)
        {
            return new ValidationResult(ValidationTier.Compile, schemaResult.Errors, []);
        }

        // Tier 2: compile into a graph
        try
        {
            var model = ModelService.ParseAndConvert(yaml);
            ModelCompiler.Compile(model);
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ValidationTier.Compile, [ex.Message]);
        }

        return ValidationResult.Valid(ValidationTier.Compile);
    }

    private static ValidationResult ValidateAnalyse(string yaml)
    {
        // Tier 1 + 2 first
        var compileResult = ValidateCompile(yaml);
        if (!compileResult.IsValid)
        {
            return new ValidationResult(ValidationTier.Analyse, compileResult.Errors, []);
        }

        // Tier 3: evaluate + invariant analysis
        try
        {
            var analysis = TemplateInvariantAnalyzer.Analyze(yaml);
            var warnings = analysis.Warnings
                .Select(w => new ValidationWarning(w.NodeId, w.Code, w.Message))
                .ToList();
            return ValidationResult.Valid(ValidationTier.Analyse, warnings);
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ValidationTier.Analyse, [ex.Message]);
        }
    }
}
