using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class OptimizerTests
{
    // ── YAML fixtures ─────────────────────────────────────────────────────

    private const string Yaml1D = """
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

    private const string Yaml2D = """
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
            values: [50, 50, 50, 50]
        """;

    // ── Fake evaluators ───────────────────────────────────────────────────

    /// <summary>metric = (arrivals - 50)^2 — minimum at arrivals = 50.</summary>
    private sealed class Bowl1DEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            var v = (arrivals - 50.0) * (arrivals - 50.0);
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [v, v, v, v],
                });
        }
    }

    /// <summary>metric = (arrivals-50)^2 + (capacity-100)^2 — minimum at (50, 100).</summary>
    private sealed class Bowl2DEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            var capacity = ConstNodeReader.ReadValue(modelYaml, "capacity") ?? 0.0;
            var v = (arrivals - 50.0) * (arrivals - 50.0)
                  + (capacity - 100.0) * (capacity - 100.0);
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [v, v, v, v],
                });
        }
    }

    /// <summary>
    /// metric = |arrivals - target| — V-shaped, minimum at target.
    /// When the reflection xr lands past the minimum and expansion xe goes even further,
    /// fe > fr, so expansion is rejected and the reflection is accepted instead.
    /// </summary>
    private sealed class AbsEvaluator : IModelEvaluator
    {
        private readonly double target;
        public AbsEvaluator(double target) => this.target = target;

        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            var v = Math.Abs(arrivals - target);
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [v, v, v, v],
                });
        }
    }

    /// <summary>
    /// f(peak) = 0, f(valley) = 3, f(everywhere else) = 10.
    /// With range [0,100]: midpoint=50 (peak, f=0), perturbation=55 (f=10).
    /// Iter 1: xr=45 (valley, fr=3); outside contraction at xoc=47.5 (foc=10 > fr=3) → shrink.
    /// Iter 2: xr=47.5 (fr=10); inside contraction at xic=51.25 (fic=10 ≥ worst=10) → shrink.
    /// </summary>
    private sealed class StepEvaluator : IModelEvaluator
    {
        private readonly double peak;
        private readonly double valley;

        public StepEvaluator(double peak, double valley)
        {
            this.peak = peak;
            this.valley = valley;
        }

        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            double v = Math.Abs(arrivals - peak)   < 1e-9 ? 0.0
                     : Math.Abs(arrivals - valley) < 1e-9 ? 3.0
                     : 10.0;
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [v, v, v, v],
                });
        }
    }

    /// <summary>metric = arrivals — linear, maximized at the upper search bound.</summary>
    private sealed class LinearEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
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

    private static Optimizer MakeOptimizer(IModelEvaluator evaluator) => new(evaluator);

    // ── Constructor guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullEvaluator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Optimizer(null!));
    }

    // ── OptimizeAsync guards ──────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_NullSpec_Throws()
    {
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        await Assert.ThrowsAsync<ArgumentNullException>(() => optimizer.OptimizeAsync(null!));
    }

    [Fact]
    public async Task OptimizeAsync_CancelledToken_Throws()
    {
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => optimizer.OptimizeAsync(spec, cts.Token));
    }

    // ── 1D bowl — minimize ────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_1DBowl_Minimize_ConvergesToMinimum()
    {
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) });

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(50.0, result.ParamValues["arrivals"], precision: 1);
        Assert.True(result.AchievedMetricMean < 1.0);
    }

    [Fact]
    public async Task OptimizeAsync_1DBowl_IterationCountPopulated()
    {
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) });

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Iterations >= 0);
    }

    // ── 2D bowl — minimize ────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_2DBowl_Minimize_ConvergesToMinimum()
    {
        var optimizer = MakeOptimizer(new Bowl2DEvaluator());
        var spec = new OptimizeSpec(Yaml2D, ["arrivals", "capacity"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange>
            {
                ["arrivals"] = new(0.0, 100.0),
                ["capacity"] = new(50.0, 200.0),
            });

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(50.0, result.ParamValues["arrivals"], precision: 1);
        Assert.Equal(100.0, result.ParamValues["capacity"], precision: 1);
        Assert.True(result.AchievedMetricMean < 1.0);
    }

    // ── Maximize ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_LinearMetric_Maximize_ConvergesToUpperBound()
    {
        var optimizer = MakeOptimizer(new LinearEvaluator());
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Maximize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) });

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Converged);
        Assert.True(result.ParamValues["arrivals"] > 95.0);
        Assert.True(result.AchievedMetricMean > 95.0);
    }

    // ── Pre-loop convergence (0 iterations) ──────────────────────────────

    [Fact]
    public async Task OptimizeAsync_InitialSimplexAlreadyConverged_ReturnsZeroIterations()
    {
        // Tiny search range [49, 51]: midpoint=50 (bowl minimum, f=0), perturbation
        // at 50.1 (f=0.01). f-spread = 0.01 < tolerance=0.1 → converged before loop.
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(49.0, 51.0) },
            tolerance: 0.1);

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(0, result.Iterations);
    }

    // ── Expansion rejected → accept reflection ────────────────────────────

    [Fact]
    public async Task OptimizeAsync_AbsMetric_ExpansionRejected_AcceptsReflection()
    {
        // target=90, range=[0,200]: midpoint=100 (right of minimum), perturbation at 110.
        // Iter 1: xr=90 hits the minimum exactly (fr=0) — better than best (f=10).
        //         Expansion xe=80 gives fe=10 >= fr=0, so expansion is rejected
        //         and the reflection xr=90 is accepted instead.
        var optimizer = MakeOptimizer(new AbsEvaluator(90.0));
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 200.0) });

        var result = await optimizer.OptimizeAsync(spec);

        Assert.True(result.Converged);
        Assert.Equal(90.0, result.ParamValues["arrivals"], precision: 1);
        Assert.True(result.AchievedMetricMean < 1.0);
    }

    // ── Shrink path ───────────────────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_ShrinkPath_BothContractionVariantsCovered()
    {
        // StepEvaluator: f(50)=0, f(45)=3, f(everywhere else)=10.
        // range=[0,100]: midpoint=50(f=0), perturbation=55(f=10).
        //
        // Iter 1: xr=45(fr=3); outside contraction xoc=47.5(foc=10 > fr=3) → shrink. [line 116]
        // Iter 2: xr=47.5(fr=10); inside contraction xic=51.25(fic=10 ≥ worst=10) → shrink. [line 128]
        //
        // maxIterations=2 exhausts; best vertex stays at arrivals=50 (f=0).
        var optimizer = MakeOptimizer(new StepEvaluator(peak: 50.0, valley: 45.0));
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) },
            tolerance: 1e-15,
            maxIterations: 2);

        var result = await optimizer.OptimizeAsync(spec);

        Assert.False(result.Converged);
        Assert.Equal(2, result.Iterations);
        Assert.Equal(50.0, result.ParamValues["arrivals"], precision: 1);
        Assert.Equal(0.0, result.AchievedMetricMean);
    }

    // ── Max iterations exhausted ──────────────────────────────────────────

    [Fact]
    public async Task OptimizeAsync_MaxIterationsExhausted_ReturnsNotConverged()
    {
        // Use off-centre target (37.5) so initial simplex midpoint (50) != minimum,
        // ensuring the algorithm can't accidentally converge in 1 step.
        var optimizer = MakeOptimizer(new Bowl1DEvaluator());
        // Bowl minimum is at arrivals=50 (metric=0), but we set maxIterations=1 and
        // tolerance=1e-15 so the f-spread never falls below tolerance in just 1 step.
        var spec = new OptimizeSpec(Yaml1D, ["arrivals"], "metric",
            OptimizeObjective.Minimize,
            new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 75.0) },
            tolerance: 1e-15,
            maxIterations: 1);

        var result = await optimizer.OptimizeAsync(spec);

        Assert.False(result.Converged);
        Assert.Equal(1, result.Iterations);
    }
}
