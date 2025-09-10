using System.Globalization;

namespace FlowTime.Core.Pmf;

/// <summary>
/// Represents a Probability Mass Function (PMF) with discrete values and their probabilities.
/// Provides validation, normalization, and expected value calculation.
/// </summary>
public class Pmf
{
    private const double NORMALIZATION_TOLERANCE = 1e-6;
    private const double MIN_PROBABILITY_SUM = 0.5;
    private const double MAX_PROBABILITY_SUM = 2.0;

    /// <summary>
    /// The probability distribution as value -> probability mappings.
    /// Guaranteed to be normalized (probabilities sum to 1.0).
    /// </summary>
    public IReadOnlyDictionary<double, double> Distribution { get; }

    /// <summary>
    /// The expected value of this PMF: E[X] = Σ (value × probability).
    /// </summary>
    public double ExpectedValue { get; }

    /// <summary>
    /// Creates a new PMF with the given distribution.
    /// The distribution will be validated and normalized if necessary.
    /// </summary>
    /// <param name="distribution">A dictionary mapping values to their probabilities</param>
    /// <exception cref="ArgumentException">Thrown when the distribution is invalid</exception>
    public Pmf(Dictionary<double, double> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        
        if (distribution.Count == 0)
            throw new ArgumentException("PMF must have at least one value-probability pair");
        
        ValidateDistribution(distribution);
        Distribution = NormalizeDistribution(distribution);
        ExpectedValue = CalculateExpectedValue();
    }

    /// <summary>
    /// Create a PMF from parallel arrays of values and probabilities.
    /// </summary>
    /// <param name="values">Array of discrete values</param>
    /// <param name="probabilities">Array of corresponding probabilities</param>
    public Pmf(double[] values, double[] probabilities)
    {
        if (values == null || probabilities == null)
            throw new ArgumentException("Values and probabilities arrays cannot be null");
        
        if (values.Length != probabilities.Length)
            throw new ArgumentException("Values and probabilities arrays must have the same length");
        
        if (values.Length == 0)
            throw new ArgumentException("PMF must have at least one value");

        var distribution = new Dictionary<double, double>();
        for (int i = 0; i < values.Length; i++)
        {
            if (distribution.ContainsKey(values[i]))
                throw new ArgumentException($"Duplicate PMF value: {values[i]}");
            distribution[values[i]] = probabilities[i];
        }

        ValidateDistribution(distribution);
        Distribution = NormalizeDistribution(distribution);
        ExpectedValue = CalculateExpectedValue();
    }

    /// <summary>
    /// Validate that the distribution meets PMF requirements.
    /// </summary>
    private static void ValidateDistribution(Dictionary<double, double> distribution)
    {
        // Check for negative probabilities
        foreach (var kvp in distribution)
        {
            if (kvp.Value < 0)
                throw new ArgumentException($"PMF probabilities must be non-negative, got {kvp.Value} for value {kvp.Key}");
        }

        // Check probability sum
        var sum = distribution.Values.Sum();
        if (sum <= 0)
            throw new ArgumentException("PMF probabilities sum to zero or negative value");

        // Check for NaN or infinite values
        foreach (var kvp in distribution)
        {
            if (double.IsNaN(kvp.Key) || double.IsInfinity(kvp.Key))
                throw new ArgumentException($"PMF value cannot be NaN or infinite: {kvp.Key}");
            
            if (double.IsNaN(kvp.Value) || double.IsInfinity(kvp.Value))
                throw new ArgumentException($"PMF probability cannot be NaN or infinite: {kvp.Value}");
        }
    }

    /// <summary>
    /// Normalize the distribution so probabilities sum to exactly 1.0.
    /// </summary>
    private static Dictionary<double, double> NormalizeDistribution(Dictionary<double, double> distribution)
    {
        var sum = distribution.Values.Sum();
        
        // If already normalized within tolerance, return as-is
        if (Math.Abs(sum - 1.0) <= NORMALIZATION_TOLERANCE)
            return new Dictionary<double, double>(distribution);

        // Normalize by dividing each probability by the sum
        var normalized = new Dictionary<double, double>();
        foreach (var kvp in distribution)
        {
            normalized[kvp.Key] = kvp.Value / sum;
        }

        return normalized;
    }

    /// <summary>
    /// Calculate the expected value: E[X] = Σ (value × probability).
    /// </summary>
    private double CalculateExpectedValue()
    {
        return Distribution.Sum(kvp => kvp.Key * kvp.Value);
    }

    /// <summary>
    /// Get a string representation of this PMF for debugging.
    /// </summary>
    public override string ToString()
    {
        var pairs = Distribution
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key:G}: {kvp.Value:F4}")
            .ToList();
        
        return $"Pmf({string.Join(", ", pairs)}) E[X]={ExpectedValue:F4}";
    }

    /// <summary>
    /// Check if two PMFs are approximately equal within tolerance.
    /// </summary>
    public bool Equals(Pmf other, double tolerance = 1e-10)
    {
        if (other == null) return false;
        
        if (Distribution.Count != other.Distribution.Count)
            return false;

        foreach (var kvp in Distribution)
        {
            if (!other.Distribution.TryGetValue(kvp.Key, out var otherProb))
                return false;
            
            if (Math.Abs(kvp.Value - otherProb) > tolerance)
                return false;
        }

        return Math.Abs(ExpectedValue - other.ExpectedValue) <= tolerance;
    }

    /// <summary>
    /// Check if two PMFs are equal (using default tolerance).
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Pmf other && Equals(other);
    }

    /// <summary>
    /// Get a hash code for this PMF. Uses the distribution values in a deterministic way.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        
        // Add each key-value pair in a consistent order
        foreach (var kvp in Distribution.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(Math.Round(kvp.Value, 10)); // Round to avoid floating point precision issues
        }
        
        return hash.ToHashCode();
    }
}
