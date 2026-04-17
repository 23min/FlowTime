namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Result of a single parameter value evaluation within a sweep.
/// </summary>
public sealed class SweepPoint
{
    /// <summary>The parameter value used for this evaluation.</summary>
    public required double ParamValue { get; init; }

    /// <summary>
    /// Series outputs for this evaluation.
    /// Keys use <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// If <see cref="SweepSpec.CaptureSeriesIds"/> was set, only those series are included.
    /// </summary>
    public required IReadOnlyDictionary<string, double[]> Series { get; init; }
}

/// <summary>
/// Full result of a parameter sweep — one <see cref="SweepPoint"/> per value in
/// <see cref="SweepSpec.Values"/>, in order.
/// </summary>
public sealed class SweepResult
{
    /// <summary>The parameter ID that was swept (from the originating <see cref="SweepSpec"/>).</summary>
    public required string ParamId { get; init; }

    /// <summary>Evaluation results, one per sweep value, in input order.</summary>
    public required SweepPoint[] Points { get; init; }
}
