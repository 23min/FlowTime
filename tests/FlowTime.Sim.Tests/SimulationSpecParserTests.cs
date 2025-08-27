using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class SimulationSpecParserTests
{
    [Fact]
    public void Valid_Const_Spec_Parses_And_Validates()
    {
        var yaml = """
                grid:
                    bins: 3
                    binMinutes: 60
                    start: 2025-01-01T00:00:00Z
                seed: 42
                arrivals:
                    kind: const
                    values: [1, 2, 3]
                route:
                    id: testNode
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.True(res.IsValid, string.Join(";", res.Errors));
        Assert.Equal(3, spec.grid!.bins);
        Assert.Equal("const", spec.arrivals!.kind);
    }

    [Fact]
    public void Valid_Poisson_Single_Rate()
    {
        var yaml = """
                grid:
                    bins: 2
                    binMinutes: 15
                arrivals:
                    kind: poisson
                    rate: 5.5
                route:
                    id: n1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.True(res.IsValid, string.Join(";", res.Errors));
    }

    [Fact]
    public void Valid_Poisson_PerBin_Rates()
    {
        var yaml = """
                grid:
                    bins: 2
                    binMinutes: 15
                arrivals:
                    kind: poisson
                    rates: [1.0, 2.0]
                route:
                    id: n1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.True(res.IsValid, string.Join(";", res.Errors));
    }

    [Fact]
    public void Invalid_Missing_Arrivals()
    {
        var yaml = """
                grid:
                    bins: 2
                    binMinutes: 60
                route:
                    id: r1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.StartsWith("arrivals:"));
    }

    [Fact]
    public void Invalid_Rate_And_Rates()
    {
        var yaml = """
                grid:
                    bins: 2
                    binMinutes: 60
                arrivals:
                    kind: poisson
                    rate: 2
                    rates: [1, 2]
                route:
                    id: r1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.Contains("both rate and rates"));
    }

    [Fact]
    public void Invalid_Length_Mismatch()
    {
        var yaml = """
                grid:
                    bins: 3
                    binMinutes: 60
                arrivals:
                    kind: const
                    values: [1, 2]
                route:
                    id: r1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.Contains("arrivals.values: length"));
    }

    [Fact]
    public void Invalid_NonUtc_Start()
    {
        var yaml = """
                grid:
                    bins: 2
                    binMinutes: 60
                    start: 2025-01-01T00:00:00+02:00
                arrivals:
                    kind: const
                    values: [1, 2]
                route:
                    id: r1
                """;
        var spec = SimulationSpecLoader.LoadFromString(yaml);
        var res = SimulationSpecValidator.Validate(spec);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.Contains("grid.start: must be UTC"));
    }
}
