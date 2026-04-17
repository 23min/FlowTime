namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Validated input for a multi-parameter optimization run.
/// </summary>
public sealed class OptimizeSpec
{
    /// <summary>Raw model YAML to optimize.</summary>
    public string ModelYaml { get; }

    /// <summary>Const-node parameter IDs to vary.</summary>
    public IReadOnlyList<string> ParamIds { get; }

    /// <summary>Metric series ID whose mean is the optimization objective.</summary>
    public string MetricSeriesId { get; }

    /// <summary>Whether to minimize or maximize the metric mean.</summary>
    public OptimizeObjective Objective { get; }

    /// <summary>Search range for each parameter. Must contain an entry for every ParamId.</summary>
    public IReadOnlyDictionary<string, SearchRange> SearchRanges { get; }

    /// <summary>Convergence tolerance on the f-value spread across simplex vertices. Default 1e-4.</summary>
    public double Tolerance { get; }

    /// <summary>Maximum number of Nelder-Mead iterations. Default 200.</summary>
    public int MaxIterations { get; }

    public OptimizeSpec(
        string modelYaml,
        IReadOnlyList<string> paramIds,
        string metricSeriesId,
        OptimizeObjective objective,
        IReadOnlyDictionary<string, SearchRange> searchRanges,
        double tolerance = 1e-4,
        int maxIterations = 200)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
            throw new ArgumentException("ModelYaml is required.", nameof(modelYaml));

        if (paramIds is null || paramIds.Count == 0)
            throw new ArgumentException("ParamIds must contain at least one entry.", nameof(paramIds));

        foreach (var id in paramIds)
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ParamIds must not contain null or whitespace entries.", nameof(paramIds));

        if (string.IsNullOrWhiteSpace(metricSeriesId))
            throw new ArgumentException("MetricSeriesId is required.", nameof(metricSeriesId));

        if (searchRanges is null)
            throw new ArgumentException("SearchRanges is required.", nameof(searchRanges));

        foreach (var id in paramIds)
        {
            if (!searchRanges.TryGetValue(id, out var range))
                throw new ArgumentException($"SearchRanges must contain an entry for every ParamId (missing '{id}').", nameof(searchRanges));

            if (range.Lo >= range.Hi)
                throw new ArgumentException($"SearchRanges['{id}'].Lo must be less than Hi.", nameof(searchRanges));
        }

        if (tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be greater than zero.");

        if (maxIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "MaxIterations must be at least 1.");

        ModelYaml = modelYaml;
        ParamIds = paramIds;
        MetricSeriesId = metricSeriesId;
        Objective = objective;
        SearchRanges = searchRanges;
        Tolerance = tolerance;
        MaxIterations = maxIterations;
    }
}
