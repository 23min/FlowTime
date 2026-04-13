namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Evaluates a model YAML and returns the resulting series dictionary.
/// Injected into <see cref="SweepRunner"/> so the sweep domain can be tested
/// without spawning the Rust engine subprocess.
/// </summary>
public interface IModelEvaluator
{
    /// <summary>
    /// Evaluate <paramref name="modelYaml"/> and return all output series.
    /// </summary>
    /// <returns>Map of series ID → per-bin double values.</returns>
    Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
        string modelYaml,
        CancellationToken cancellationToken = default);
}
