namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// One entry in a <see cref="GoalSeekResult.Trace"/>.
///
/// Two <c>iteration: 0</c> entries are emitted for the initial boundary evaluations
/// at <c>searchLo</c> and <c>searchHi</c> (in that order). Each bisection step then
/// emits one entry with <c>iteration: 1..N</c>, where <see cref="ParamValue"/> is the
/// midpoint evaluated at that step and (<see cref="SearchLo"/>, <see cref="SearchHi"/>)
/// is the <b>post-step</b> bracket (after narrowing).
/// </summary>
/// <param name="Iteration">0 for boundary evaluations; 1..N for bisection steps.</param>
/// <param name="ParamValue">
/// The parameter value evaluated at this entry (<c>searchLo</c> / <c>searchHi</c> /
/// midpoint depending on <see cref="Iteration"/>).
/// </param>
/// <param name="MetricMean">
/// Unsigned mean of the metric series at <see cref="ParamValue"/> — the same value
/// that drives the bisection decision.
/// </param>
/// <param name="SearchLo">
/// For <c>iteration: 0</c> entries, the original <c>searchLo</c> from the spec.
/// For bisection steps, the lower bound of the bracket <b>after</b> this step's narrowing.
/// </param>
/// <param name="SearchHi">
/// For <c>iteration: 0</c> entries, the original <c>searchHi</c> from the spec.
/// For bisection steps, the upper bound of the bracket <b>after</b> this step's narrowing.
/// </param>
public sealed record GoalSeekTracePoint(
    int Iteration,
    double ParamValue,
    double MetricMean,
    double SearchLo,
    double SearchHi);
