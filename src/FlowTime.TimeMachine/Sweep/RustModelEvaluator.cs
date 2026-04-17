using FlowTime.Core.Execution;

namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// <see cref="IModelEvaluator"/> implementation backed by the Rust engine subprocess.
/// Wraps <see cref="RustEngineRunner"/> and maps the series list to a dictionary.
/// </summary>
public sealed class RustModelEvaluator : IModelEvaluator
{
    private readonly RustEngineRunner runner;

    public RustModelEvaluator(RustEngineRunner runner)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
        string modelYaml,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.EvaluateAsync(modelYaml, cancellationToken).ConfigureAwait(false);

        return result.Series.ToDictionary(
            s => s.Id,
            s => s.Values,
            StringComparer.OrdinalIgnoreCase);
    }
}
