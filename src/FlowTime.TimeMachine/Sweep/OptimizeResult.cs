namespace FlowTime.TimeMachine.Sweep;

/// <summary>Result of a multi-parameter optimization run.</summary>
public sealed class OptimizeResult
{
    /// <summary>
    /// The parameter values that best achieve the objective.
    /// If <see cref="Converged"/> is <c>false</c>, this is the best approximation found.
    /// </summary>
    public required IReadOnlyDictionary<string, double> ParamValues { get; init; }

    /// <summary>Mean of the metric series at <see cref="ParamValues"/>.</summary>
    public required double AchievedMetricMean { get; init; }

    /// <summary>
    /// <c>true</c> when the f-value spread across simplex vertices fell below tolerance.
    /// <c>false</c> when <see cref="MaxIterations"/> was exhausted before convergence.
    /// </summary>
    public required bool Converged { get; init; }

    /// <summary>Number of Nelder-Mead iterations taken (not counting initial evaluation).</summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Ordered trace of the per-iteration best vertex, per D-2026-04-21-034.
    /// <c>iteration: 0</c> is the initial simplex best; <c>iteration: 1..N</c> are the
    /// post-iteration bests after each main-loop <c>Sort</c>. <see cref="OptimizeTracePoint.MetricMean"/>
    /// is always unsigned (user-space semantics) for both minimize and maximize runs.
    /// </summary>
    public IReadOnlyList<OptimizeTracePoint> Trace { get; init; } = Array.Empty<OptimizeTracePoint>();
}
