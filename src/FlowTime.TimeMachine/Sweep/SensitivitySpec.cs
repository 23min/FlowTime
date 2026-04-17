namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Specification for a numerical sensitivity analysis over a set of const-node parameters.
/// </summary>
public sealed class SensitivitySpec
{
    /// <param name="modelYaml">YAML of the model to analyse.</param>
    /// <param name="paramIds">IDs of const nodes to compute sensitivity for. Must be non-empty.</param>
    /// <param name="metricSeriesId">Series ID of the output metric to measure (e.g., "queue.queueTimeMs").</param>
    /// <param name="perturbation">
    /// Fractional perturbation for central difference, in the open interval (0, 1).
    /// Default 0.05 = ±5%.
    /// </param>
    public SensitivitySpec(
        string modelYaml,
        string[] paramIds,
        string metricSeriesId,
        double perturbation = 0.05)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
            throw new ArgumentException("Model YAML must not be null or whitespace.", nameof(modelYaml));
        if (paramIds is null || paramIds.Length == 0)
            throw new ArgumentException("ParamIds must not be null or empty.", nameof(paramIds));
        if (string.IsNullOrWhiteSpace(metricSeriesId))
            throw new ArgumentException("Metric series ID must not be null or whitespace.", nameof(metricSeriesId));
        if (perturbation <= 0.0 || perturbation >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(perturbation),
                perturbation, "Perturbation must be in the open interval (0, 1).");

        ModelYaml = modelYaml;
        ParamIds = paramIds;
        MetricSeriesId = metricSeriesId;
        Perturbation = perturbation;
    }

    /// <summary>YAML of the model to analyse.</summary>
    public string ModelYaml { get; }

    /// <summary>Const-node IDs to compute sensitivity for.</summary>
    public string[] ParamIds { get; }

    /// <summary>Series ID of the output metric to measure.</summary>
    public string MetricSeriesId { get; }

    /// <summary>
    /// Fractional perturbation for central difference (default 0.05 = ±5%).
    /// Must be in (0, 1) exclusive.
    /// </summary>
    public double Perturbation { get; }
}
