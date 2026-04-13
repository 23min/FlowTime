using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class GoalSeekerTests
{
    // ── Fixtures ───────────────────────────────────────────────────────────

    /// <summary>Model where metric = arrivals (identity, perfectly linear).</summary>
    private const string LinearYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
        """;

    /// <summary>
    /// Evaluator where metric = arrivals value (linear, monotone increasing).
    /// Reads the patched arrivals value from the YAML and echoes it as the metric.
    /// </summary>
    private sealed class LinearEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [arrivals, arrivals, arrivals, arrivals],
                });
        }
    }

    /// <summary>
    /// Evaluator where metric is constant regardless of arrivals.
    /// Used to test the non-bracketed (non-monotone) case.
    /// </summary>
    private sealed class ConstantMetricEvaluator : IModelEvaluator
    {
        private readonly double constant;
        public ConstantMetricEvaluator(double constant) => this.constant = constant;

        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [constant, constant, constant, constant],
                });
        }
    }

    private static GoalSeeker MakeSeeker(IModelEvaluator evaluator) =>
        new(new SweepRunner(evaluator));

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSweepRunner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GoalSeeker(null!));
    }

    // ── SeekAsync guards ───────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_NullSpec_Throws()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        await Assert.ThrowsAsync<ArgumentNullException>(() => seeker.SeekAsync(null!));
    }

    [Fact]
    public async Task SeekAsync_CancelledToken_Throws()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => seeker.SeekAsync(spec, cts.Token));
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_LinearModel_ConvergesToTarget()
    {
        // metric = arrivals → target arrivals = 50 when target metric = 50
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(50.0, result.ParamValue, precision: 4);
        Assert.Equal(50.0, result.AchievedMetricMean, precision: 4);
    }

    [Fact]
    public async Task SeekAsync_TargetAtLoBoundary_ConvergesQuickly()
    {
        // target = 0 at lo = 0 → already bracketed and converges
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 0.0, searchLo: 0.0, searchHi: 100.0, tolerance: 1e-6);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(0.0, result.AchievedMetricMean, precision: 4);
    }

    [Fact]
    public async Task SeekAsync_TargetAtHiBoundary_ConvergesQuickly()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 100.0, searchLo: 0.0, searchHi: 100.0, tolerance: 1e-6);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(100.0, result.AchievedMetricMean, precision: 4);
    }

    [Fact]
    public async Task SeekAsync_IterationCount_IsPopulated()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Iterations > 0);
    }

    // ── Non-bracketed (target outside metric range) ────────────────────────

    [Fact]
    public async Task SeekAsync_TargetAboveMetricRange_ReturnsNotConverged()
    {
        // metric is always in [0,100], target=200 can't be reached
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 200.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
    }

    [Fact]
    public async Task SeekAsync_TargetBelowMetricRange_ReturnsNotConverged()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: -10.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
    }

    [Fact]
    public async Task SeekAsync_ConstantMetric_ReturnsNotConverged()
    {
        // metric = 42 regardless of param → target 50 not achievable
        var seeker = MakeSeeker(new ConstantMetricEvaluator(42.0));
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
    }

    // ── Max iterations ─────────────────────────────────────────────────────

    [Fact]
    public async Task SeekAsync_MaxIterationsExhausted_ReturnsNotConverged()
    {
        // target=25 is bracketed (metric range [0,100]) but is NOT the first midpoint (50),
        // so maxIterations=1 runs out before converging — forces the exhaustion code path.
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 25.0, searchLo: 0.0, searchHi: 100.0,
            tolerance: 1e-15,   // essentially unreachable in 1 step
            maxIterations: 1);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
        Assert.Equal(1, result.Iterations);
    }
}
