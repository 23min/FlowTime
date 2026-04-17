namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Specification for a 1D goal-seek operation: find the const-node parameter value
/// that drives a metric series mean to <see cref="Target"/> via bisection.
/// </summary>
public sealed class GoalSeekSpec
{
    /// <param name="modelYaml">YAML of the model.</param>
    /// <param name="paramId">ID of the const node to vary.</param>
    /// <param name="metricSeriesId">Series ID of the metric to drive to <paramref name="target"/>.</param>
    /// <param name="target">Target mean value for the metric series.</param>
    /// <param name="searchLo">Lower bound of the parameter search range. Must be less than <paramref name="searchHi"/>.</param>
    /// <param name="searchHi">Upper bound of the parameter search range.</param>
    /// <param name="tolerance">Convergence tolerance on |achievedMean − target|. Default 1e-6.</param>
    /// <param name="maxIterations">Maximum number of bisection steps. Default 50.</param>
    public GoalSeekSpec(
        string modelYaml,
        string paramId,
        string metricSeriesId,
        double target,
        double searchLo,
        double searchHi,
        double tolerance = 1e-6,
        int maxIterations = 50)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
            throw new ArgumentException("Model YAML must not be null or whitespace.", nameof(modelYaml));
        if (string.IsNullOrWhiteSpace(paramId))
            throw new ArgumentException("Parameter ID must not be null or whitespace.", nameof(paramId));
        if (string.IsNullOrWhiteSpace(metricSeriesId))
            throw new ArgumentException("Metric series ID must not be null or whitespace.", nameof(metricSeriesId));
        if (searchLo >= searchHi)
            throw new ArgumentException(
                $"SearchLo ({searchLo}) must be less than SearchHi ({searchHi}).",
                nameof(searchLo));
        if (tolerance <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be positive.");
        if (maxIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "MaxIterations must be at least 1.");

        ModelYaml = modelYaml;
        ParamId = paramId;
        MetricSeriesId = metricSeriesId;
        Target = target;
        SearchLo = searchLo;
        SearchHi = searchHi;
        Tolerance = tolerance;
        MaxIterations = maxIterations;
    }

    public string ModelYaml { get; }
    public string ParamId { get; }
    public string MetricSeriesId { get; }
    public double Target { get; }
    public double SearchLo { get; }
    public double SearchHi { get; }
    public double Tolerance { get; }
    public int MaxIterations { get; }
}
