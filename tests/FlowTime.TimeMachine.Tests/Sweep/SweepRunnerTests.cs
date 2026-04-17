using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class SweepRunnerTests
{
    // ── Fixture ────────────────────────────────────────────────────────────

    private const string MinimalYaml = """
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
    /// Fake evaluator that returns fixed series regardless of the YAML.
    /// Captures the last YAML it was called with for assertion.
    /// </summary>
    private sealed class FakeEvaluator : IModelEvaluator
    {
        private readonly IReadOnlyDictionary<string, double[]> result;
        public string? LastYaml { get; private set; }

        public FakeEvaluator(IReadOnlyDictionary<string, double[]>? result = null)
        {
            this.result = result ?? new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arrivals"] = [10.0, 10.0, 10.0, 10.0],
                ["served"] = [8.0, 8.0, 8.0, 8.0],
            };
        }

        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastYaml = modelYaml;
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Fake that returns different results per call count (for value-tracking tests).
    /// </summary>
    private sealed class CountingEvaluator : IModelEvaluator
    {
        private int callCount;
        private readonly Func<int, IReadOnlyDictionary<string, double[]>> factory;

        public CountingEvaluator(Func<int, IReadOnlyDictionary<string, double[]>> factory)
        {
            this.factory = factory;
        }

        public int CallCount => callCount;

        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(factory(Interlocked.Increment(ref callCount)));
        }
    }

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullEvaluator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SweepRunner(null!));
    }

    // ── RunAsync guards ────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NullSpec_Throws()
    {
        var runner = new SweepRunner(new FakeEvaluator());
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null!));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_Throws()
    {
        var runner = new SweepRunner(new FakeEvaluator());
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0, 20.0, 30.0]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync(spec, cts.Token));
    }

    // ── RunAsync happy path ────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleValue_ReturnsSinglePoint()
    {
        var runner = new SweepRunner(new FakeEvaluator());
        var spec = new SweepSpec(MinimalYaml, "arrivals", [15.0]);

        var result = await runner.RunAsync(spec);

        Assert.Single(result.Points);
        Assert.Equal(15.0, result.Points[0].ParamValue);
    }

    [Fact]
    public async Task RunAsync_MultipleValues_ReturnsOnePointPerValue()
    {
        var runner = new SweepRunner(new FakeEvaluator());
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0, 20.0, 30.0]);

        var result = await runner.RunAsync(spec);

        Assert.Equal(3, result.Points.Length);
        Assert.Equal([10.0, 20.0, 30.0], result.Points.Select(p => p.ParamValue).ToArray());
    }

    [Fact]
    public async Task RunAsync_ParamIdPropagatedToResult()
    {
        var runner = new SweepRunner(new FakeEvaluator());
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0]);

        var result = await runner.RunAsync(spec);

        Assert.Equal("arrivals", result.ParamId);
    }

    [Fact]
    public async Task RunAsync_NullCaptureSeriesIds_ReturnsAllSeries()
    {
        var evaluator = new FakeEvaluator(new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = [5.0, 5.0],
            ["served"] = [4.0, 4.0],
            ["queue"] = [1.0, 1.0],
        });
        var runner = new SweepRunner(evaluator);
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0], captureSeriesIds: null);

        var result = await runner.RunAsync(spec);

        Assert.Equal(3, result.Points[0].Series.Count);
        Assert.True(result.Points[0].Series.ContainsKey("arrivals"));
        Assert.True(result.Points[0].Series.ContainsKey("served"));
        Assert.True(result.Points[0].Series.ContainsKey("queue"));
    }

    [Fact]
    public async Task RunAsync_WithCaptureSeriesIds_FiltersToSpecified()
    {
        var evaluator = new FakeEvaluator(new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = [5.0, 5.0],
            ["served"] = [4.0, 4.0],
            ["queue"] = [1.0, 1.0],
        });
        var runner = new SweepRunner(evaluator);
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0],
            captureSeriesIds: ["arrivals", "served"]);

        var result = await runner.RunAsync(spec);

        Assert.Equal(2, result.Points[0].Series.Count);
        Assert.True(result.Points[0].Series.ContainsKey("arrivals"));
        Assert.True(result.Points[0].Series.ContainsKey("served"));
        Assert.False(result.Points[0].Series.ContainsKey("queue"));
    }

    [Fact]
    public async Task RunAsync_CaptureSeriesIds_IsCaseInsensitive()
    {
        var evaluator = new FakeEvaluator(new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arrivals"] = [5.0, 5.0],
        });
        var runner = new SweepRunner(evaluator);
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0],
            captureSeriesIds: ["arrivals"]);  // lowercase, key is "Arrivals"

        var result = await runner.RunAsync(spec);

        Assert.True(result.Points[0].Series.ContainsKey("Arrivals"));
    }

    [Fact]
    public async Task RunAsync_EvaluatorCalledOncePerValue()
    {
        int callCount = 0;
        var evaluator = new CountingEvaluator(_ =>
        {
            callCount++;
            return new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arrivals"] = [1.0],
            };
        });
        var runner = new SweepRunner(evaluator);
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0, 20.0, 30.0]);

        await runner.RunAsync(spec);

        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task RunAsync_EachPointHasDifferentParamValue()
    {
        // Evaluator echoes back a different series value each call to prove values differ
        var evaluator = new CountingEvaluator(n => new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = [n * 10.0],
        });
        var runner = new SweepRunner(evaluator);
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0, 20.0, 30.0]);

        var result = await runner.RunAsync(spec);

        // Each point should capture a different series snapshot
        Assert.Equal(10.0, result.Points[0].ParamValue);
        Assert.Equal(20.0, result.Points[1].ParamValue);
        Assert.Equal(30.0, result.Points[2].ParamValue);
    }

    [Fact]
    public async Task RunAsync_YamlPatchedBeforeEval_ConstNodeValueSubstituted()
    {
        // Verify ConstNodePatcher is applied: the YAML passed to the evaluator
        // should contain the sweep value, not the original.
        var fake = new FakeEvaluator();
        var runner = new SweepRunner(fake);
        // Original YAML has arrivals=10; sweep value is 25
        var spec = new SweepSpec(MinimalYaml, "arrivals", [25.0]);

        await runner.RunAsync(spec);

        // The YAML passed to the evaluator should have "25" in it, not "10"
        Assert.NotNull(fake.LastYaml);
        Assert.Contains("25", fake.LastYaml);
    }
}
