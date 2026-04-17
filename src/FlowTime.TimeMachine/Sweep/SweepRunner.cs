namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Executes a parameter sweep by evaluating the model once per value in
/// <see cref="SweepSpec.Values"/>, patching the named const node before each evaluation.
/// </summary>
public sealed class SweepRunner
{
    private readonly IModelEvaluator evaluator;

    public SweepRunner(IModelEvaluator evaluator)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    /// <summary>
    /// Run the sweep described by <paramref name="spec"/>.
    /// </summary>
    public async Task<SweepResult> RunAsync(SweepSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var points = new List<SweepPoint>(spec.Values.Length);

        foreach (var value in spec.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var patchedYaml = ConstNodePatcher.Patch(spec.ModelYaml, spec.ParamId, value);
            var allSeries = await evaluator.EvaluateAsync(patchedYaml, cancellationToken)
                .ConfigureAwait(false);

            var series = FilterSeries(allSeries, spec.CaptureSeriesIds);
            points.Add(new SweepPoint { ParamValue = value, Series = series });
        }

        return new SweepResult { ParamId = spec.ParamId, Points = points.ToArray() };
    }

    private static IReadOnlyDictionary<string, double[]> FilterSeries(
        IReadOnlyDictionary<string, double[]> all,
        string[]? captureSeriesIds)
    {
        if (captureSeriesIds is null || captureSeriesIds.Length == 0)
            return all;

        return all
            .Where(kvp => captureSeriesIds.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }
}
