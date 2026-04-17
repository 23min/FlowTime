namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Computes numerical sensitivity (∂metric_mean/∂param) for a set of const-node parameters
/// using central-difference approximation over two <see cref="SweepRunner"/> evaluations per param.
/// </summary>
public sealed class SensitivityRunner
{
    private readonly SweepRunner sweepRunner;

    public SensitivityRunner(SweepRunner sweepRunner)
    {
        this.sweepRunner = sweepRunner ?? throw new ArgumentNullException(nameof(sweepRunner));
    }

    /// <summary>
    /// Run sensitivity analysis for the parameters described in <paramref name="spec"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the evaluator does not return the metric series specified in the spec.
    /// </exception>
    public async Task<SensitivityResult> RunAsync(
        SensitivitySpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var points = new List<SensitivityPoint>(spec.ParamIds.Length);

        foreach (var paramId in spec.ParamIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baseValue = ConstNodeReader.ReadValue(spec.ModelYaml, paramId);
            if (baseValue is null)
                continue; // unknown or non-const node — skip silently

            var point = await ComputePointAsync(spec, paramId, baseValue.Value, cancellationToken)
                .ConfigureAwait(false);
            points.Add(point);
        }

        // Sort by |gradient| descending — most impactful param first
        points.Sort((a, b) => Math.Abs(b.Gradient).CompareTo(Math.Abs(a.Gradient)));

        return new SensitivityResult
        {
            MetricSeriesId = spec.MetricSeriesId,
            Points = points.ToArray(),
        };
    }

    private async Task<SensitivityPoint> ComputePointAsync(
        SensitivitySpec spec,
        string paramId,
        double baseValue,
        CancellationToken cancellationToken)
    {
        // Zero-base: gradient is indeterminate — return 0.0
        if (baseValue == 0.0)
        {
            return new SensitivityPoint
            {
                ParamId = paramId,
                BaseValue = 0.0,
                Gradient = 0.0,
            };
        }

        var hi = baseValue * (1.0 + spec.Perturbation);
        var lo = baseValue * (1.0 - spec.Perturbation);

        var sweepSpec = new SweepSpec(
            spec.ModelYaml,
            paramId,
            [lo, hi],
            captureSeriesIds: [spec.MetricSeriesId]);

        var sweepResult = await sweepRunner.RunAsync(sweepSpec, cancellationToken).ConfigureAwait(false);

        var seriesLo = sweepResult.Points[0].Series;
        var seriesHi = sweepResult.Points[1].Series;

        if (!seriesLo.TryGetValue(spec.MetricSeriesId, out var metricLo) ||
            !seriesHi.TryGetValue(spec.MetricSeriesId, out var metricHi))
        {
            throw new InvalidOperationException(
                $"Metric series '{spec.MetricSeriesId}' was not returned by the evaluator. " +
                $"Check that the series ID is correct and that the model produces it.");
        }

        var meanLo = metricLo.Length > 0 ? metricLo.Average() : 0.0;
        var meanHi = metricHi.Length > 0 ? metricHi.Average() : 0.0;
        var denominator = hi - lo; // == 2 * baseValue * perturbation

        var gradient = (meanHi - meanLo) / denominator;

        return new SensitivityPoint
        {
            ParamId = paramId,
            BaseValue = baseValue,
            Gradient = gradient,
        };
    }
}
