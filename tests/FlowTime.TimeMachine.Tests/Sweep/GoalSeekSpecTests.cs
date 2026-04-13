using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class GoalSeekSpecTests
{
    private const string Yaml = """
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

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(null!, "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_WhitespaceYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec("  ", "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_NullParamId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, null!, "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_WhitespaceParamId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, "  ", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_NullMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", null!, target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_WhitespaceMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "  ", target: 50.0, searchLo: 0.0, searchHi: 100.0));
    }

    [Fact]
    public void Constructor_SearchLoEqualSearchHi_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 10.0, searchHi: 10.0));
    }

    [Fact]
    public void Constructor_SearchLoGreaterThanSearchHi_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 100.0, searchHi: 10.0));
    }

    [Fact]
    public void Constructor_ZeroTolerance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0, tolerance: 0.0));
    }

    [Fact]
    public void Constructor_NegativeTolerance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0, tolerance: -1.0));
    }

    [Fact]
    public void Constructor_ZeroMaxIterations_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0, maxIterations: 0));
    }

    [Fact]
    public void Constructor_NegativeMaxIterations_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GoalSeekSpec(Yaml, "arrivals", "metric", target: 50.0, searchLo: 0.0, searchHi: 100.0, maxIterations: -1));
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_PropertiesSet()
    {
        var spec = new GoalSeekSpec(Yaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0,
            tolerance: 0.001, maxIterations: 20);

        Assert.Equal(Yaml, spec.ModelYaml);
        Assert.Equal("arrivals", spec.ParamId);
        Assert.Equal("metric", spec.MetricSeriesId);
        Assert.Equal(50.0, spec.Target);
        Assert.Equal(0.0, spec.SearchLo);
        Assert.Equal(100.0, spec.SearchHi);
        Assert.Equal(0.001, spec.Tolerance);
        Assert.Equal(20, spec.MaxIterations);
    }

    [Fact]
    public void Constructor_DefaultTolerance_Is1e6()
    {
        var spec = new GoalSeekSpec(Yaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);
        Assert.Equal(1e-6, spec.Tolerance);
    }

    [Fact]
    public void Constructor_DefaultMaxIterations_Is50()
    {
        var spec = new GoalSeekSpec(Yaml, "arrivals", "metric",
            target: 50.0, searchLo: 0.0, searchHi: 100.0);
        Assert.Equal(50, spec.MaxIterations);
    }
}
