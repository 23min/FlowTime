using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class SweepSpecTests
{
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

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec(null!, "arrivals", [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_WhitespaceYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec("   ", "arrivals", [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_NullParamId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec(MinimalYaml, null!, [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_WhitespaceParamId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec(MinimalYaml, "  ", [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_NullValues_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec(MinimalYaml, "arrivals", null!));
    }

    [Fact]
    public void Constructor_EmptyValues_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepSpec(MinimalYaml, "arrivals", []));
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_PropertiesSet()
    {
        var values = new[] { 10.0, 20.0, 30.0 };
        var spec = new SweepSpec(MinimalYaml, "arrivals", values);

        Assert.Equal(MinimalYaml, spec.ModelYaml);
        Assert.Equal("arrivals", spec.ParamId);
        Assert.Equal(values, spec.Values);
        Assert.Null(spec.CaptureSeriesIds);
    }

    [Fact]
    public void Constructor_WithCaptureSeriesIds_PropertiesSet()
    {
        var captureIds = new[] { "arrivals", "served" };
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0], captureIds);

        Assert.Equal(captureIds, spec.CaptureSeriesIds);
    }

    [Fact]
    public void Constructor_NullCaptureSeriesIds_IsAllowed()
    {
        // CaptureSeriesIds is optional — null means "return all series"
        var spec = new SweepSpec(MinimalYaml, "arrivals", [10.0], captureSeriesIds: null);
        Assert.Null(spec.CaptureSeriesIds);
    }
}
