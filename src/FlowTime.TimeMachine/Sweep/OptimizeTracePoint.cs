namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// One entry in an <see cref="OptimizeResult.Trace"/> — the per-iteration best vertex
/// of a Nelder-Mead optimization.
///
/// <c>iteration: 0</c> is the initial simplex's best vertex after the pre-loop sort
/// (recorded before the main loop begins). <c>iteration: 1..N</c> are the post-sort
/// best vertices at the end of each main-loop iteration.
/// </summary>
/// <param name="Iteration">0 for the initial simplex best; 1..N for per-iteration bests.</param>
/// <param name="ParamValues">
/// The best vertex's parameter values at this iteration, keyed by param id
/// in the original <c>ParamIds</c> order.
/// </param>
/// <param name="MetricMean">
/// <b>Unsigned</b> mean of the metric series at <see cref="ParamValues"/>. The internal
/// Nelder-Mead minimize sign-flip (negating the metric for <c>maximize</c> runs) is
/// reversed at record time so the trace is always in user-space semantics.
/// </param>
public sealed record OptimizeTracePoint(
    int Iteration,
    IReadOnlyDictionary<string, double> ParamValues,
    double MetricMean);
