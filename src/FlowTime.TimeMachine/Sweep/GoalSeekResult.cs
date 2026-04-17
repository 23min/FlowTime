namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Result of a goal-seek operation.
/// </summary>
public sealed class GoalSeekResult
{
    /// <summary>
    /// The parameter value that best achieves the target.
    /// If <see cref="Converged"/> is <c>false</c>, this is the best approximation found.
    /// </summary>
    public required double ParamValue { get; init; }

    /// <summary>Mean of the metric series at <see cref="ParamValue"/>.</summary>
    public required double AchievedMetricMean { get; init; }

    /// <summary>
    /// <c>true</c> when |<see cref="AchievedMetricMean"/> − target| &lt; tolerance.
    /// <c>false</c> when the target was not bracketed by the search range, or when
    /// <see cref="MaxIterations"/> was exhausted before convergence.
    /// </summary>
    public required bool Converged { get; init; }

    /// <summary>Number of bisection steps taken (not counting the two boundary evaluations).</summary>
    public required int Iterations { get; init; }
}
