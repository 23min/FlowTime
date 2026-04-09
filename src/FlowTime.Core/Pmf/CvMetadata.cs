namespace FlowTime.Core;

/// <summary>
/// Source of the coefficient of variation data.
/// </summary>
public enum CvSource
{
    /// <summary>Cv computed from the PMF distribution shape (exact, per-bin from distribution).</summary>
    Pmf,

    /// <summary>Cv computed from observed series values (sample statistic, approximate).</summary>
    Observed,

    /// <summary>Cv is zero by definition (constant/deterministic series).</summary>
    Constant
}

/// <summary>
/// Per-node coefficient of variation metadata produced during evaluation.
/// Wraps a per-bin Cv array with a source tag that distinguishes
/// PMF-derived (exact) from observed (approximate) and constant (zero).
/// </summary>
public sealed record CvMetadata(double[] CoefficientOfVariation, CvSource Source);
