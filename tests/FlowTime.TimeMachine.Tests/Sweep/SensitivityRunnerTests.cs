using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class SensitivityRunnerTests
{
    // ── Fixtures ───────────────────────────────────────────────────────────

    /// <summary>
    /// Model with two const nodes: arrivals=10, capacity=20.
    /// </summary>
    private const string TwoParamYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
          - id: capacity
            kind: const
            values: [20, 20, 20, 20]
        """;

    /// <summary>
    /// Fake evaluator: returns a fixed "metric" series where the value tracks the
    /// arrivals param value (parsed from the patched YAML), simulating a linear
    /// model where metric = arrivals.
    /// </summary>
    private sealed class LinearFakeEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read the arrivals value from the patched YAML
            var arrivalsValue = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 10.0;
            var capacityValue = ConstNodeReader.ReadValue(modelYaml, "capacity") ?? 20.0;

            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    // metric tracks arrivals linearly: metric(t) = arrivals
                    ["metric"] = [arrivalsValue, arrivalsValue, arrivalsValue, arrivalsValue],
                    ["arrivals"] = [arrivalsValue, arrivalsValue, arrivalsValue, arrivalsValue],
                    ["capacity"] = [capacityValue, capacityValue, capacityValue, capacityValue],
                });
        }
    }

    /// <summary>
    /// Fake that returns series without the requested metric (for error-path test).
    /// </summary>
    private sealed class NoMetricEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["other"] = [1.0, 1.0, 1.0, 1.0],
                });
        }
    }

    private static SensitivityRunner MakeRunner(IModelEvaluator evaluator) =>
        new(new SweepRunner(evaluator));

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSweepRunner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SensitivityRunner(null!));
    }

    // ── RunAsync guards ────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NullSpec_Throws()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null!));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_Throws()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync(spec, cts.Token));
    }

    [Fact]
    public async Task RunAsync_MetricSeriesNotInResult_Throws()
    {
        var runner = MakeRunner(new NoMetricEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric");

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(spec));
    }

    // ── RunAsync happy path ────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleParam_ReturnsOnePoint()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Single(result.Points);
        Assert.Equal("arrivals", result.Points[0].ParamId);
    }

    [Fact]
    public async Task RunAsync_MultipleParams_ReturnsOnePointEach()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals", "capacity"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Equal(2, result.Points.Length);
    }

    [Fact]
    public async Task RunAsync_BaseValuePropagated()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Equal(10.0, result.Points[0].BaseValue);
    }

    [Fact]
    public async Task RunAsync_MetricSeriesIdPropagated()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Equal("metric", result.MetricSeriesId);
    }

    [Fact]
    public async Task RunAsync_LinearModel_GradientIsOne()
    {
        // metric = arrivals, so ∂metric/∂arrivals = 1.0
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals"], "metric", perturbation: 0.05);

        var result = await runner.RunAsync(spec);

        Assert.Equal(1.0, result.Points[0].Gradient, precision: 6);
    }

    [Fact]
    public async Task RunAsync_MetricIndependentOfCapacity_GradientIsZero()
    {
        // metric tracks arrivals only, so ∂metric/∂capacity = 0
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["capacity"], "metric", perturbation: 0.05);

        var result = await runner.RunAsync(spec);

        Assert.Equal(0.0, result.Points[0].Gradient, precision: 6);
    }

    [Fact]
    public async Task RunAsync_SortedByAbsGradientDescending()
    {
        // arrivals has gradient 1.0, capacity has gradient 0.0
        // arrivals should come first
        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(TwoParamYaml, ["capacity", "arrivals"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Equal("arrivals", result.Points[0].ParamId);
        Assert.Equal("capacity", result.Points[1].ParamId);
    }

    [Fact]
    public async Task RunAsync_UnknownParamId_SkippedSilently()
    {
        var runner = MakeRunner(new LinearFakeEvaluator());
        // "nonexistent" is not a const node in the model
        var spec = new SensitivitySpec(TwoParamYaml, ["arrivals", "nonexistent"], "metric");

        var result = await runner.RunAsync(spec);

        // Only "arrivals" should appear — "nonexistent" skipped
        Assert.Single(result.Points);
        Assert.Equal("arrivals", result.Points[0].ParamId);
    }

    [Fact]
    public async Task RunAsync_ZeroBaseValue_GradientIsZero()
    {
        var yaml = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            nodes:
              - id: arrivals
                kind: const
                values: [0, 0, 0, 0]
            """;

        var runner = MakeRunner(new LinearFakeEvaluator());
        var spec = new SensitivitySpec(yaml, ["arrivals"], "metric");

        var result = await runner.RunAsync(spec);

        Assert.Equal(0.0, result.Points[0].Gradient);
        Assert.Equal(0.0, result.Points[0].BaseValue);
    }
}
