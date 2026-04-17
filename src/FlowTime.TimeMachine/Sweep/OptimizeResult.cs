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
}
