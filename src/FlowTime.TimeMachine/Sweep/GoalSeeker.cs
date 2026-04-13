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

        // Evaluate at both boundaries
        var meanLo = await EvaluateMeanAsync(spec, spec.SearchLo, cancellationToken).ConfigureAwait(false);
        var meanHi = await EvaluateMeanAsync(spec, spec.SearchHi, cancellationToken).ConfigureAwait(false);

        // Check if target is already hit at a boundary
        if (Math.Abs(meanLo - spec.Target) < spec.Tolerance)
            return Converged(spec.SearchLo, meanLo, 0);
        if (Math.Abs(meanHi - spec.Target) < spec.Tolerance)
            return Converged(spec.SearchHi, meanHi, 0);

        // Check if target is bracketed (one side above, one side below)
        var loResidual = meanLo - spec.Target;
        var hiResidual = meanHi - spec.Target;

        if (Math.Sign(loResidual) == Math.Sign(hiResidual))
        {
            // Target not bracketed — return the closer endpoint
            var closerParam = Math.Abs(loResidual) <= Math.Abs(hiResidual) ? spec.SearchLo : spec.SearchHi;
            var closerMean = Math.Abs(loResidual) <= Math.Abs(hiResidual) ? meanLo : meanHi;
            return NotConverged(closerParam, closerMean, 0);
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
                return Converged(mid, midMean, iteration);

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

            if (iteration == spec.MaxIterations)
                return NotConverged(mid, midMean, iteration);
        }

        // Unreachable — MaxIterations >= 1 is enforced by spec
        return NotConverged((lo + hi) / 2.0, double.NaN, spec.MaxIterations);
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

    private static GoalSeekResult Converged(double param, double mean, int iterations) =>
        new() { ParamValue = param, AchievedMetricMean = mean, Converged = true, Iterations = iterations };

    private static GoalSeekResult NotConverged(double param, double mean, int iterations) =>
        new() { ParamValue = param, AchievedMetricMean = mean, Converged = false, Iterations = iterations };
}
