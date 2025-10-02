namespace FlowTime.Core;

/// <summary>
/// Represents a single entry in a PMF specification.
/// Used as input to the PMF compilation pipeline.
/// </summary>
public class PmfEntry
{
    /// <summary>
    /// The discrete value this entry represents.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The probability of this value occurring.
    /// Must be non-negative. The sum of all probabilities should be ~1.0.
    /// </summary>
    public double Probability { get; set; }

    /// <summary>
    /// Create a new PMF entry.
    /// </summary>
    public PmfEntry() { }

    /// <summary>
    /// Create a new PMF entry with specified value and probability.
    /// </summary>
    public PmfEntry(double value, double probability)
    {
        Value = value;
        Probability = probability;
    }

    /// <summary>
    /// Get string representation for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"PmfEntry({Value}, p={Probability:F4})";
    }
}
