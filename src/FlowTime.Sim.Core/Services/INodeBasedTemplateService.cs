using FlowTime.Sim.Core.Templates;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Charter-compliant template service for SIM-M2.6-CORRECTIVE milestone.
/// Supports node-based schema with Engine-compatible model generation.
/// </summary>
public interface INodeBasedTemplateService
{
    /// <summary>
    /// Get all available node-based templates.
    /// </summary>
    Task<IReadOnlyList<Template>> GetAllTemplatesAsync();

    /// <summary>
    /// Get a specific template by ID.
    /// </summary>
    Task<Template?> GetTemplateAsync(string templateId);

    /// <summary>
    /// Generate an Engine-compatible model from a template with parameter substitution.
    /// Substitutes ${parameter} placeholders, removes template-specific metadata,
    /// and preserves PMF nodes for Engine compilation.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="parameters">Parameter values for substitution</param>
    /// <returns>Engine-compatible YAML model ready for FlowTime Engine POST /run</returns>
    Task<string> GenerateEngineModelAsync(string templateId, Dictionary<string, object> parameters);

    /// <summary>
    /// Validate template parameters against the template schema.
    /// </summary>
    Task<ValidationResult> ValidateParametersAsync(string templateId, Dictionary<string, object> parameters);
}

/// <summary>
/// Parameter validation result.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}