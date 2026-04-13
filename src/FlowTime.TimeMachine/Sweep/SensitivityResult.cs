namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Sensitivity of the target metric to a single const-node parameter.
/// </summary>
public sealed class SensitivityPoint
{
    /// <summary>The const-node parameter ID.</summary>
    public required string ParamId { get; init; }

    /// <summary>The parameter's current value (first bin of the const node).</summary>
    public required double BaseValue { get; init; }

    /// <summary>
    /// Numerical gradient: ∂(mean metric) / ∂param, computed via central difference.
    /// <c>0.0</c> when the base value is zero (gradient indeterminate).
    /// </summary>
    public required double Gradient { get; init; }
}

/// <summary>
/// Full sensitivity analysis result — one <see cref="SensitivityPoint"/> per found parameter,
/// sorted by <c>|Gradient|</c> descending (most impactful parameter first).
/// </summary>
public sealed class SensitivityResult
{
    /// <summary>The metric series ID that was measured (from the originating spec).</summary>
    public required string MetricSeriesId { get; init; }

    /// <summary>
    /// Sensitivity points, sorted by <c>|Gradient|</c> descending.
    /// Parameters not found in the model (non-const or absent nodes) are omitted.
    /// </summary>
    public required SensitivityPoint[] Points { get; init; }
}
