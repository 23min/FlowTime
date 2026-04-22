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

    // ── Trace shape (AC1) ──────────────────────────────────────────────────

    /// <summary>
    /// Return-path "Converged at searchLo" — the initial boundary evaluation at
    /// searchLo hits the target within tolerance. Trace is the two boundary entries only.
    /// </summary>
    [Fact]
    public async Task SeekAsync_ConvergedAtSearchLo_TraceHasOnlyBoundaryEntries()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 0.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(0, result.Iterations);
        Assert.Equal(2, result.Trace.Count);
        Assert.All(result.Trace, tp => Assert.Equal(0, tp.Iteration));
        Assert.Equal(0.0, result.Trace[0].ParamValue);
        Assert.Equal(100.0, result.Trace[1].ParamValue);
        Assert.Equal(0.0, result.Trace[0].MetricMean, precision: 6);
        Assert.Equal(100.0, result.Trace[1].MetricMean, precision: 6);
        // Both boundary entries carry the spec's original bracket.
        Assert.All(result.Trace, tp => Assert.Equal(0.0, tp.SearchLo));
        Assert.All(result.Trace, tp => Assert.Equal(100.0, tp.SearchHi));
    }

    /// <summary>
    /// Return-path "Converged at searchHi" — the initial boundary evaluation at
    /// searchHi hits the target within tolerance. Trace is the two boundary entries only.
    /// Note: this path only fires when searchLo doesn't converge first, which the
    /// existing test ordering in `GoalSeeker.SeekAsync` guarantees.
    /// </summary>
    [Fact]
    public async Task SeekAsync_ConvergedAtSearchHi_TraceHasOnlyBoundaryEntries()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 100.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(0, result.Iterations);
        Assert.Equal(2, result.Trace.Count);
        Assert.All(result.Trace, tp => Assert.Equal(0, tp.Iteration));
        Assert.Equal(0.0, result.Trace[0].ParamValue);
        Assert.Equal(100.0, result.Trace[1].ParamValue);
    }

    /// <summary>
    /// Return-path "Not bracketed" — both boundaries fall on the same side of target.
    /// Trace is the two boundary entries only; response reports converged: false, iterations: 0.
    /// </summary>
    [Fact]
    public async Task SeekAsync_NotBracketed_TraceHasOnlyBoundaryEntries()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        // metric ∈ [0,100] for arrivals ∈ [0,100]; target=200 is above both.
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 200.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
        Assert.Equal(0, result.Iterations);
        Assert.Equal(2, result.Trace.Count);
        Assert.All(result.Trace, tp => Assert.Equal(0, tp.Iteration));
        Assert.Equal(0.0, result.Trace[0].ParamValue);
        Assert.Equal(100.0, result.Trace[1].ParamValue);
    }

    /// <summary>
    /// Return-path "Tolerance hit mid-loop" — bisection converges via a midpoint match.
    /// Trace has 2 boundary entries + N iteration entries, and every bisection entry
    /// is bracketed by its own post-step [searchLo, searchHi].
    /// </summary>
    [Fact]
    public async Task SeekAsync_ToleranceHitMidLoop_TraceIncludesBisectionSteps()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        // target=50 is exactly the first midpoint of [0,100] → converges at iteration 1.
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);

        var result = await seeker.SeekAsync(spec);

        Assert.True(result.Converged);
        Assert.True(result.Iterations >= 1);
        // Expected trace length: 2 boundary entries + iterations
        Assert.Equal(2 + result.Iterations, result.Trace.Count);

        // First two are iteration 0 boundaries in order.
        Assert.Equal(0, result.Trace[0].Iteration);
        Assert.Equal(0, result.Trace[1].Iteration);
        Assert.Equal(0.0, result.Trace[0].ParamValue);
        Assert.Equal(100.0, result.Trace[1].ParamValue);

        // Bisection entries: iteration monotonically increasing from 1,
        // each midpoint sits inside the spec's original bounds, and
        // post-step (searchLo, searchHi) brackets each recorded paramValue.
        for (int i = 2; i < result.Trace.Count; i++)
        {
            var tp = result.Trace[i];
            Assert.Equal(i - 1, tp.Iteration);          // 1, 2, 3...
            Assert.InRange(tp.ParamValue, 0.0, 100.0);
            Assert.True(tp.SearchLo <= tp.ParamValue && tp.ParamValue <= tp.SearchHi,
                $"iteration {tp.Iteration}: paramValue {tp.ParamValue} not in post-step bracket [{tp.SearchLo}, {tp.SearchHi}]");
        }

        // For iteration 1: midpoint = (0 + 100) / 2 = 50, which matches the target
        // exactly. The midpoint writes either to lo or hi depending on the sign of
        // the midpoint residual vs currentMeanLo residual. Because midMean==target,
        // sign(midResidual) == 0, which equals Math.Sign(loResidual) where loResidual
        // is also 0.0 (meanLo was meanLo at lo=0, residual=0-50=-50, sign=-1 or 0?
        // Actually meanLo=0, loResidual=-50, sign=-1. midResidual=0, sign=0. Not equal,
        // so hi is updated to 50. Post-step bracket is [0, 50].
        var iter1 = result.Trace[2];
        Assert.Equal(1, iter1.Iteration);
        Assert.Equal(50.0, iter1.ParamValue, precision: 6);
        Assert.Equal(50.0, iter1.MetricMean, precision: 6);
    }

    /// <summary>
    /// Return-path "Max iterations exhausted" — target bracketed but not reached
    /// within maxIterations. Trace has exactly 2 + maxIterations entries.
    /// </summary>
    [Fact]
    public async Task SeekAsync_MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        // target=33 is bracketed in [0,100] but never lands exactly on a dyadic
        // midpoint in 3 bisections (50, 25, 37.5), so the loop exhausts cleanly.
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 33.0, searchLo: 0.0, searchHi: 100.0,
            tolerance: 1e-15,
            maxIterations: 3);

        var result = await seeker.SeekAsync(spec);

        Assert.False(result.Converged);
        Assert.Equal(3, result.Iterations);
        Assert.Equal(2 + 3, result.Trace.Count);
        Assert.Equal(0, result.Trace[0].Iteration);
        Assert.Equal(0, result.Trace[1].Iteration);
        Assert.Equal(1, result.Trace[2].Iteration);
        Assert.Equal(2, result.Trace[3].Iteration);
        Assert.Equal(3, result.Trace[4].Iteration);

        // Post-step bracket invariant: each bisection entry's paramValue sits in its
        // own (searchLo, searchHi).
        for (int i = 2; i < result.Trace.Count; i++)
        {
            var tp = result.Trace[i];
            Assert.True(tp.SearchLo <= tp.ParamValue && tp.ParamValue <= tp.SearchHi);
        }

        // Post-step bracket for iteration 1 with target=33, loResidual=-33, hiResidual=67:
        // midResidual = meanMid - 33 = 50 - 33 = 17, sign=+1. currentMeanLo residual is
        // negative, sign=-1. Opposite signs → hi := mid (50). Bracket narrows from
        // [0,100] to [0,50].
        var iter1 = result.Trace[2];
        Assert.Equal(1, iter1.Iteration);
        Assert.Equal(50.0, iter1.ParamValue, precision: 6);
        Assert.Equal(0.0, iter1.SearchLo, precision: 6);
        Assert.Equal(50.0, iter1.SearchHi, precision: 6);
    }

    /// <summary>
    /// The two <c>iteration: 0</c> entries must always appear in (lo, hi) order,
    /// regardless of which boundary converges first.
    /// </summary>
    [Fact]
    public async Task SeekAsync_BoundaryEntries_AlwaysLoThenHi()
    {
        var seeker = MakeSeeker(new LinearEvaluator());
        // Generic bisection run.
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 37.0, searchLo: 5.0, searchHi: 95.0);

        var result = await seeker.SeekAsync(spec);

        Assert.Equal(5.0, result.Trace[0].ParamValue, precision: 6);
        Assert.Equal(95.0, result.Trace[1].ParamValue, precision: 6);
    }

    [Fact]
    public async Task SeekAsync_Trace_MetricMeanMatchesEvaluatorOutput()
    {
        // Linear evaluator: metric = arrivals. So each trace entry's metricMean
        // must equal its paramValue.
        var seeker = MakeSeeker(new LinearEvaluator());
        var spec = new GoalSeekSpec(LinearYaml, "arrivals", "metric",
            target: 42.0, searchLo: 0.0, searchHi: 100.0,
            tolerance: 1e-15, maxIterations: 5);

        var result = await seeker.SeekAsync(spec);

        Assert.All(result.Trace, tp => Assert.Equal(tp.ParamValue, tp.MetricMean, precision: 6));
    }
}
