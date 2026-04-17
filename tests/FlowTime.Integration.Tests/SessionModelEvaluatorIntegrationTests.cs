using FlowTime.Core.Configuration;
using FlowTime.Core.Execution;
using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Integration.Tests;

/// <summary>
/// Integration tests for <see cref="SessionModelEvaluator"/> that exercise the real
/// Rust engine session protocol. Tests are skipped (return early) if the engine binary
/// is not present — same pattern as <see cref="EngineSessionWebSocketTests"/>.
/// </summary>
public class SessionModelEvaluatorIntegrationTests
{
    private const string SimpleModel = """
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
          - id: served
            kind: expr
            expr: "arrivals * 0.5"
        """;

    private static string ModelWithArrivals(double value) => $"""
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: arrivals
            kind: const
            values: [{value}, {value}, {value}, {value}]
          - id: served
            kind: expr
            expr: "arrivals * 0.5"
        """;

    private static string? TryResolveEnginePath()
    {
        var solutionRoot = DirectoryProvider.FindSolutionRoot();
        if (solutionRoot is null) return null;
        var path = Path.Combine(solutionRoot, "engine", "target", "release", "flowtime-engine");
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public async Task FirstCall_Compiles_ReturnsSeries()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return; // engine binary not available — skip

        await using var evaluator = new SessionModelEvaluator(enginePath);

        var series = await evaluator.EvaluateAsync(SimpleModel);

        Assert.True(series.ContainsKey("arrivals"), "arrivals series present");
        Assert.True(series.ContainsKey("served"), "served series present");
        Assert.Equal(4, series["arrivals"].Length);
        Assert.All(series["arrivals"], v => Assert.Equal(10.0, v, precision: 6));
        Assert.All(series["served"], v => Assert.Equal(5.0, v, precision: 6));
    }

    [Fact]
    public async Task SecondCall_SendsOverrides_NotRecompile()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        await using var evaluator = new SessionModelEvaluator(enginePath);

        // First call with arrivals=10 (from SimpleModel)
        var first = await evaluator.EvaluateAsync(SimpleModel);
        Assert.All(first["arrivals"], v => Assert.Equal(10.0, v, precision: 6));

        // Second call with patched YAML where arrivals=20. The evaluator must read
        // the new value via ConstNodeReader and send it as an eval override.
        var patched = ModelWithArrivals(20.0);
        var second = await evaluator.EvaluateAsync(patched);

        Assert.All(second["arrivals"], v => Assert.Equal(20.0, v, precision: 6));
        Assert.All(second["served"], v => Assert.Equal(10.0, v, precision: 6));
    }

    [Fact]
    public async Task ManyEvaluations_AllReturnCorrectSeries()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        await using var evaluator = new SessionModelEvaluator(enginePath);

        // First call (compile)
        _ = await evaluator.EvaluateAsync(SimpleModel);

        // 20 subsequent evals with different arrivals values
        for (var i = 1; i <= 20; i++)
        {
            var arrivals = (double)i;
            var series = await evaluator.EvaluateAsync(ModelWithArrivals(arrivals));
            Assert.All(series["arrivals"], v => Assert.Equal(arrivals, v, precision: 6));
            Assert.All(series["served"], v => Assert.Equal(arrivals * 0.5, v, precision: 6));
        }
    }

    [Fact]
    public async Task SessionVsPerEval_NumericValuesAgree()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // Parity check on NUMERIC values only, not key shapes:
        //   RustEngineRunner reads run artifacts → keys are `{nodeId}@{COMPONENT}@{CLASS}`
        //   Session protocol returns column-map keys → bare `nodeId`
        // Both are correct for their context. This test strips the suffix from the
        // per-eval keys before comparing so it validates that the two invocation modes
        // produce the same math.
        await using var session = new SessionModelEvaluator(enginePath);
        var runner = new RustEngineRunner(enginePath);
        var perEval = new RustModelEvaluator(runner);

        var yaml = ModelWithArrivals(7.0);
        var sessionSeries = await session.EvaluateAsync(yaml);
        var perEvalSeries = await perEval.EvaluateAsync(yaml);

        static string BareKey(string full)
        {
            var at = full.IndexOf('@');
            return at < 0 ? full : full[..at];
        }

        var perEvalBare = perEvalSeries
            .ToDictionary(kvp => BareKey(kvp.Key), kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        // Every bare key present in the session must also be present on the per-eval side
        // with identical numeric values (and vice versa).
        Assert.Equal(
            perEvalBare.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            sessionSeries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));

        foreach (var key in sessionSeries.Keys)
        {
            var expected = perEvalBare[key];
            var actual = sessionSeries[key];
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], precision: 6);
            }
        }
    }

    [Fact]
    public async Task SessionEvaluator_WorksWithSweepRunner()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        // SweepRunner should drive SessionModelEvaluator end-to-end without any
        // changes. Five-point sweep over arrivals values.
        await using var evaluator = new SessionModelEvaluator(enginePath);
        var runner = new SweepRunner(evaluator);

        var spec = new SweepSpec(
            modelYaml: SimpleModel,
            paramId: "arrivals",
            values: [5.0, 10.0, 15.0, 20.0, 25.0],
            captureSeriesIds: ["served"]);

        var result = await runner.RunAsync(spec);

        Assert.Equal(5, result.Points.Length);
        for (var i = 0; i < 5; i++)
        {
            // served = arrivals * 0.5; all 4 bins have the same value
            var arrivals = spec.Values[i];
            var expectedServed = arrivals * 0.5;
            Assert.Equal(arrivals, result.Points[i].ParamValue);
            Assert.All(
                result.Points[i].Series["served"],
                v => Assert.Equal(expectedServed, v, precision: 6));
        }
    }

    [Fact]
    public async Task EvaluateAsync_InvalidYaml_RaisesInvalidOperationException()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        await using var evaluator = new SessionModelEvaluator(enginePath);

        // Use a clearly invalid YAML — engine should return an error response.
        var badYaml = "this is not valid flowtime yaml";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluator.EvaluateAsync(badYaml));

        Assert.Contains("compile", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispose_TerminatesSubprocess()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        var before = CountEngineProcesses();

        // Open session, evaluate once (spawns subprocess), dispose.
        {
            await using var evaluator = new SessionModelEvaluator(enginePath);
            _ = await evaluator.EvaluateAsync(SimpleModel);
        }

        // Give OS a moment to reap the exited process.
        await Task.Delay(500);

        var after = CountEngineProcesses();
        Assert.True(after <= before,
            $"Engine process leaked after dispose: before={before}, after={after}");
    }

    [Fact]
    public async Task ConcurrentEvaluateAsyncCalls_AreSerialized()
    {
        var enginePath = TryResolveEnginePath();
        if (enginePath is null) return;

        await using var evaluator = new SessionModelEvaluator(enginePath);

        // Warm up the session first so the compile path doesn't race.
        _ = await evaluator.EvaluateAsync(SimpleModel);

        // Fire 10 concurrent calls with different arrivals values. Each returned
        // series must correspond to its requested value (no cross-talk between calls).
        var values = Enumerable.Range(1, 10).Select(i => (double)i).ToArray();
        var tasks = values
            .Select(v => (value: v, task: evaluator.EvaluateAsync(ModelWithArrivals(v))))
            .ToArray();

        var allResults = await Task.WhenAll(tasks.Select(t => t.task));

        for (var i = 0; i < tasks.Length; i++)
        {
            var value = tasks[i].value;
            var series = allResults[i];
            Assert.All(series["arrivals"], v => Assert.Equal(value, v, precision: 6));
        }
    }

    /// <summary>
    /// Count running flowtime-engine subprocess instances (same helper as
    /// <see cref="EngineSessionWebSocketTests"/>).
    /// </summary>
    private static int CountEngineProcesses()
    {
        try
        {
            var all = System.Diagnostics.Process.GetProcessesByName("flowtime-engine");
            var count = all.Length;
            foreach (var p in all) p.Dispose();
            return count;
        }
        catch
        {
            return 0;
        }
    }
}
