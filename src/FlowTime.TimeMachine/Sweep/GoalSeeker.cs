namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Finds the const-node parameter value that drives a metric series mean to a target
/// using bisection over <see cref="SweepRunner"/>.
/// </summary>
public sealed class GoalSeeker
{
    private readonly SweepRunner sweepRunner;

    public GoalSeeker(SweepRunner sweepRunner)
    {
        this.sweepRunner = sweepRunner ?? throw new ArgumentNullException(nameof(sweepRunner));
    }

    /// <summary>
    /// Seek the parameter value that achieves <see cref="GoalSeekSpec.Target"/>.
    /// </summary>
    public async Task<GoalSeekResult> SeekAsync(
        GoalSeekSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Trace buffer — bounded by 2 boundary entries + MaxIterations bisection steps
        // (per D-2026-04-21-034). Pre-size to the upper bound to avoid List growth.
        var trace = new List<GoalSeekTracePoint>(capacity: spec.MaxIterations + 2);

        // Evaluate at both boundaries — both iteration:0 entries carry the spec's
        // original (searchLo, searchHi) bracket.
        var meanLo = await EvaluateMeanAsync(spec, spec.SearchLo, cancellationToken).ConfigureAwait(false);
        trace.Add(new GoalSeekTracePoint(0, spec.SearchLo, meanLo, spec.SearchLo, spec.SearchHi));

        var meanHi = await EvaluateMeanAsync(spec, spec.SearchHi, cancellationToken).ConfigureAwait(false);
        trace.Add(new GoalSeekTracePoint(0, spec.SearchHi, meanHi, spec.SearchLo, spec.SearchHi));

        // Check if target is already hit at a boundary
        if (Math.Abs(meanLo - spec.Target) < spec.Tolerance)
            return Converged(spec.SearchLo, meanLo, 0, trace);
        if (Math.Abs(meanHi - spec.Target) < spec.Tolerance)
            return Converged(spec.SearchHi, meanHi, 0, trace);

        // Check if target is bracketed (one side above, one side below)
        var loResidual = meanLo - spec.Target;
        var hiResidual = meanHi - spec.Target;

        if (Math.Sign(loResidual) == Math.Sign(hiResidual))
        {
            // Target not bracketed — return the closer endpoint
            var closerParam = Math.Abs(loResidual) <= Math.Abs(hiResidual) ? spec.SearchLo : spec.SearchHi;
            var closerMean = Math.Abs(loResidual) <= Math.Abs(hiResidual) ? meanLo : meanHi;
            return NotConverged(closerParam, closerMean, 0, trace);
        }

        // Bisection
        var lo = spec.SearchLo;
        var hi = spec.SearchHi;
        var currentMeanLo = meanLo;

        for (var iteration = 1; iteration <= spec.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mid = (lo + hi) / 2.0;
            var midMean = await EvaluateMeanAsync(spec, mid, cancellationToken).ConfigureAwait(false);

            if (Math.Abs(midMean - spec.Target) < spec.Tolerance)
            {
                // Post-step bracket: target hit at mid — no narrowing happens past this point,
                // but the recorded bracket is the pre-narrow bracket (still correct:
                // mid ∈ [lo, hi]).
                trace.Add(new GoalSeekTracePoint(iteration, mid, midMean, lo, hi));
                return Converged(mid, midMean, iteration, trace);
            }

            // Narrow the bracket
            var midResidual = midMean - spec.Target;
            if (Math.Sign(midResidual) == Math.Sign(currentMeanLo - spec.Target))
            {
                lo = mid;
                currentMeanLo = midMean;
            }
            else
            {
                hi = mid;
            }

            // Record AFTER narrowing — per-spec semantics (post-step bracket).
            trace.Add(new GoalSeekTracePoint(iteration, mid, midMean, lo, hi));

            if (iteration == spec.MaxIterations)
                return NotConverged(mid, midMean, iteration, trace);
        }

        // Unreachable — MaxIterations >= 1 is enforced by spec
        return NotConverged((lo + hi) / 2.0, double.NaN, spec.MaxIterations, trace);
    }

    private async Task<double> EvaluateMeanAsync(
        GoalSeekSpec spec,
        double paramValue,
        CancellationToken cancellationToken)
    {
        var sweepSpec = new SweepSpec(
            spec.ModelYaml,
            spec.ParamId,
            [paramValue],
            captureSeriesIds: [spec.MetricSeriesId]);

        var sweepResult = await sweepRunner.RunAsync(sweepSpec, cancellationToken).ConfigureAwait(false);
        var series = sweepResult.Points[0].Series;

        if (!series.TryGetValue(spec.MetricSeriesId, out var values) || values.Length == 0)
            return 0.0;

        return values.Average();
    }

    private static GoalSeekResult Converged(
        double param, double mean, int iterations, List<GoalSeekTracePoint> trace) =>
        new()
        {
            ParamValue = param,
            AchievedMetricMean = mean,
            Converged = true,
            Iterations = iterations,
            Trace = trace,
        };

    private static GoalSeekResult NotConverged(
        double param, double mean, int iterations, List<GoalSeekTracePoint> trace) =>
        new()
        {
            ParamValue = param,
            AchievedMetricMean = mean,
            Converged = false,
            Iterations = iterations,
            Trace = trace,
        };
}
