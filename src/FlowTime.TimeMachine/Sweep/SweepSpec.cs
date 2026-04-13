namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Specification for a one-dimensional parameter sweep over a const node.
/// </summary>
public sealed class SweepSpec
{
    /// <param name="modelYaml">YAML of the model to sweep.</param>
    /// <param name="paramId">ID of the const node to vary (e.g., "arrivals").</param>
    /// <param name="values">Values to evaluate at. Must be non-empty.</param>
    /// <param name="captureSeriesIds">
    /// Series IDs to include in each <see cref="SweepPoint"/>.
    /// <c>null</c> means return all series.
    /// </param>
    public SweepSpec(string modelYaml, string paramId, double[] values, string[]? captureSeriesIds = null)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
            throw new ArgumentException("Model YAML must not be null or whitespace.", nameof(modelYaml));
        if (string.IsNullOrWhiteSpace(paramId))
            throw new ArgumentException("Parameter ID must not be null or whitespace.", nameof(paramId));
        if (values is null || values.Length == 0)
            throw new ArgumentException("Values must not be null or empty.", nameof(values));

        ModelYaml = modelYaml;
        ParamId = paramId;
        Values = values;
        CaptureSeriesIds = captureSeriesIds;
    }

    /// <summary>YAML of the model to sweep.</summary>
    public string ModelYaml { get; }

    /// <summary>ID of the const node to vary.</summary>
    public string ParamId { get; }

    /// <summary>Values to evaluate at (one engine call per value).</summary>
    public double[] Values { get; }

    /// <summary>Series IDs to capture in results; <c>null</c> captures all series.</summary>
    public string[]? CaptureSeriesIds { get; }
}
