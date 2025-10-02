namespace FlowTime.Core;

/// <summary>
/// Options for PMF compilation pipeline.
/// </summary>
public class PmfCompilerOptions
{
    /// <summary>
    /// Grid alignment policy (default: Error).
    /// </summary>
    public RepeatPolicy RepeatPolicy { get; set; } = RepeatPolicy.Error;

    /// <summary>
    /// Random seed for deterministic sampling (default: 42).
    /// </summary>
    public int Seed { get; set; } = 42;

    /// <summary>
    /// Number of bins in the time grid.
    /// If specified, will validate PMF length against this.
    /// </summary>
    public int? GridBins { get; set; }

    /// <summary>
    /// Tolerance for normalization check (default: 0.001).
    /// Probabilities summing within this tolerance of 1.0 will be renormalized.
    /// </summary>
    public double NormalizationTolerance { get; set; } = 0.001;
}
