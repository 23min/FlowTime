using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class SensitivitySpecTests
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
            new SensitivitySpec(null!, ["arrivals"], "arrivals"));
    }

    [Fact]
    public void Constructor_WhitespaceYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SensitivitySpec("   ", ["arrivals"], "arrivals"));
    }

    [Fact]
    public void Constructor_NullParamIds_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SensitivitySpec(Yaml, null!, "arrivals"));
    }

    [Fact]
    public void Constructor_EmptyParamIds_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SensitivitySpec(Yaml, [], "arrivals"));
    }

    [Fact]
    public void Constructor_NullMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], null!));
    }

    [Fact]
    public void Constructor_WhitespaceMetricSeriesId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], "  "));
    }

    [Fact]
    public void Constructor_ZeroPerturbation_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], "arrivals", perturbation: 0.0));
    }

    [Fact]
    public void Constructor_NegativePerturbation_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], "arrivals", perturbation: -0.1));
    }

    [Fact]
    public void Constructor_PerturbationAtOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], "arrivals", perturbation: 1.0));
    }

    [Fact]
    public void Constructor_PerturbationAboveOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SensitivitySpec(Yaml, ["arrivals"], "arrivals", perturbation: 1.5));
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_PropertiesSet()
    {
        var spec = new SensitivitySpec(Yaml, ["arrivals", "capacity"], "arrivals", perturbation: 0.1);

        Assert.Equal(Yaml, spec.ModelYaml);
        Assert.Equal(["arrivals", "capacity"], spec.ParamIds);
        Assert.Equal("arrivals", spec.MetricSeriesId);
        Assert.Equal(0.1, spec.Perturbation);
    }

    [Fact]
    public void Constructor_DefaultPerturbation_IsFivePercent()
    {
        var spec = new SensitivitySpec(Yaml, ["arrivals"], "arrivals");
        Assert.Equal(0.05, spec.Perturbation);
    }
}
