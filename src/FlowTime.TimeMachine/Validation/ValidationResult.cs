namespace FlowTime.TimeMachine.Validation;

/// <summary>
/// Result of a tiered validation operation.
/// </summary>
public sealed class ValidationResult
{
    public ValidationTier Tier { get; init; }
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors { get; init; }
    public IReadOnlyList<ValidationWarning> Warnings { get; init; }

    public ValidationResult(
        ValidationTier tier,
        IReadOnlyList<ValidationError> errors,
        IReadOnlyList<ValidationWarning> warnings)
    {
        Tier = tier;
        Errors = errors;
        Warnings = warnings;
    }

    public static ValidationResult Valid(ValidationTier tier, IReadOnlyList<ValidationWarning>? warnings = null) =>
        new(tier, Array.Empty<ValidationError>(), warnings ?? Array.Empty<ValidationWarning>());

    public static ValidationResult Invalid(ValidationTier tier, IEnumerable<string> messages) =>
        new(tier, messages.Select(m => new ValidationError(m)).ToList(), Array.Empty<ValidationWarning>());
}

/// <summary>
/// A validation error: a definitive problem that makes the model invalid.
/// </summary>
public sealed record ValidationError(string Message);

/// <summary>
/// A validation warning from tier-3 invariant analysis: the model is valid but
/// has a semantic issue that may indicate a modeling problem.
/// </summary>
public sealed record ValidationWarning(string NodeId, string Code, string Message);
