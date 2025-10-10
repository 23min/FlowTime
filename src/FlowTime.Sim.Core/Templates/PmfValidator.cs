using System.Security.Cryptography;
using System.Text;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Validation result for PMF specifications.
/// </summary>
public class PmfValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Validates PMF (Probability Mass Function) specifications.
/// </summary>
public static class PmfValidator
{
    /// <summary>
    /// Validate a PMF specification for correctness.
    /// </summary>
    /// <param name="pmfSpec">PMF specification to validate</param>
    /// <returns>Validation result with any errors</returns>
    public static PmfValidationResult Validate(PmfSpec pmfSpec)
    {
        var result = new PmfValidationResult { IsValid = true };

        if (pmfSpec == null)
        {
            result.IsValid = false;
            result.Errors.Add("PMF specification cannot be null");
            return result;
        }

        // Check that values and probabilities exist
        if (pmfSpec.Values == null || pmfSpec.Values.Length == 0)
        {
            result.IsValid = false;
            result.Errors.Add("PMF values cannot be null or empty");
        }

        if (pmfSpec.Probabilities == null || pmfSpec.Probabilities.Length == 0)
        {
            result.IsValid = false;
            result.Errors.Add("PMF probabilities cannot be null or empty");
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        // Check length matching
        if (pmfSpec.Values!.Length != pmfSpec.Probabilities!.Length)
        {
            result.IsValid = false;
            result.Errors.Add("Values and probabilities arrays must have the same length");
            return result;
        }

        // Check non-negative probabilities
        if (pmfSpec.Probabilities.Any(p => p < 0))
        {
            result.IsValid = false;
            result.Errors.Add("All probabilities must be non-negative");
        }

        // Check probabilities sum to 1.0 (within tolerance)
        var sum = pmfSpec.Probabilities.Sum();
        if (Math.Abs(sum - 1.0) > 1e-10)
        {
            result.IsValid = false;
            result.Errors.Add("Probabilities must sum to 1.0");
        }

        return result;
    }
}

/// <summary>
/// Computes hashes for PMF specifications for provenance tracking.
/// </summary>
public static class PmfHasher
{
    /// <summary>
    /// Compute a deterministic hash for a PMF specification.
    /// </summary>
    /// <param name="pmfSpec">PMF specification to hash</param>
    /// <returns>Hash string for provenance tracking</returns>
    public static string ComputeHash(PmfSpec pmfSpec)
    {
        if (pmfSpec == null)
        {
            throw new ArgumentNullException(nameof(pmfSpec));
        }

        // Create a deterministic string representation
        var builder = new StringBuilder();
        builder.Append("values:");
        foreach (var value in pmfSpec.Values)
        {
            builder.Append(value.ToString("G17")); // High precision
            builder.Append(",");
        }
        builder.Append("|probabilities:");
        foreach (var prob in pmfSpec.Probabilities)
        {
            builder.Append(prob.ToString("G17")); // High precision
            builder.Append(",");
        }

        // Compute SHA256 hash
        var data = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}