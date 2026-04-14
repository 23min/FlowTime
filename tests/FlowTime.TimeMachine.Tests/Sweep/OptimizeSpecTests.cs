using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class OptimizeSpecTests
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

    private static readonly IReadOnlyDictionary<string, SearchRange> ValidRanges =
        new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) };

    // ── ModelYaml guards ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(null!, ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges));
    }

    [Fact]
    public void Constructor_WhitespaceYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec("  ", ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges));
    }

    // ── ParamIds guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullParamIds_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, null!, "metric",
                OptimizeObjective.Minimize, ValidRanges));
    }

    [Fact]
    public void Constructor_EmptyParamIds_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, [], "metric",
                OptimizeObjective.Minimize, ValidRanges));
    }

    [Fact]
    public void Constructor_ParamIdContainsWhitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals", "  "], "metric",
                OptimizeObjective.Minimize,
                new Dictionary<string, SearchRange>
                {
                    ["arrivals"] = new(0.0, 100.0),
                    ["  "] = new(0.0, 100.0),
                }));
    }

    // ── MetricSeriesId guards ─────────────────────────────────────────────

    [Fact]
    public void Constructor_NullMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], null!,
                OptimizeObjective.Minimize, ValidRanges));
    }

    [Fact]
    public void Constructor_WhitespaceMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "  ",
                OptimizeObjective.Minimize, ValidRanges));
    }

    // ── SearchRanges guards ───────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSearchRanges_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize, null!));
    }

    [Fact]
    public void Constructor_MissingRangeForParamId_Throws()
    {
        // paramIds has "arrivals" but searchRanges doesn't
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize,
                new Dictionary<string, SearchRange>()));
    }

    [Fact]
    public void Constructor_RangeLoEqualsHi_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize,
                new Dictionary<string, SearchRange> { ["arrivals"] = new(50.0, 50.0) }));
    }

    [Fact]
    public void Constructor_RangeLoGreaterThanHi_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize,
                new Dictionary<string, SearchRange> { ["arrivals"] = new(100.0, 0.0) }));
    }

    // ── Tolerance / MaxIterations guards ──────────────────────────────────

    [Fact]
    public void Constructor_ZeroTolerance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges, tolerance: 0.0));
    }

    [Fact]
    public void Constructor_NegativeTolerance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges, tolerance: -1.0));
    }

    [Fact]
    public void Constructor_ZeroMaxIterations_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges, maxIterations: 0));
    }

    [Fact]
    public void Constructor_NegativeMaxIterations_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OptimizeSpec(Yaml, ["arrivals"], "metric",
                OptimizeObjective.Minimize, ValidRanges, maxIterations: -1));
    }

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_PropertiesSet()
    {
        var ranges = new Dictionary<string, SearchRange> { ["arrivals"] = new(0.0, 100.0) };
        var spec = new OptimizeSpec(Yaml, ["arrivals"], "metric",
            OptimizeObjective.Maximize, ranges, tolerance: 0.01, maxIterations: 50);

        Assert.Equal(Yaml, spec.ModelYaml);
        Assert.Equal(["arrivals"], spec.ParamIds);
        Assert.Equal("metric", spec.MetricSeriesId);
        Assert.Equal(OptimizeObjective.Maximize, spec.Objective);
        Assert.Equal(0.0, spec.SearchRanges["arrivals"].Lo);
        Assert.Equal(100.0, spec.SearchRanges["arrivals"].Hi);
        Assert.Equal(0.01, spec.Tolerance);
        Assert.Equal(50, spec.MaxIterations);
    }

    [Fact]
    public void Constructor_DefaultTolerance_Is1e4()
    {
        var spec = new OptimizeSpec(Yaml, ["arrivals"], "metric",
            OptimizeObjective.Minimize, ValidRanges);
        Assert.Equal(1e-4, spec.Tolerance);
    }

    [Fact]
    public void Constructor_DefaultMaxIterations_Is200()
    {
        var spec = new OptimizeSpec(Yaml, ["arrivals"], "metric",
            OptimizeObjective.Minimize, ValidRanges);
        Assert.Equal(200, spec.MaxIterations);
    }
}
