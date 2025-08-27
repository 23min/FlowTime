using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class RngSelectionTests
{
    private SimulationSpec BasePoisson()
    {
        return new SimulationSpec
        {
            schemaVersion = 1,
            grid = new GridSpec { bins = 5, binMinutes = 60, start = "2025-01-01T00:00:00Z" },
            seed = 123,
            arrivals = new ArrivalsSpec { kind = "poisson", rate = 2.5 },
            route = new RouteSpec { id = "n" }
        };
    }

    [Fact]
    public void Default_Uses_Pcg_Deterministically()
    {
        var spec = BasePoisson();
        var v = SimulationSpecValidator.Validate(spec);
        Assert.True(v.IsValid, string.Join(";", v.Errors));
        var a1 = ArrivalGenerators.Generate(spec);
        var a2 = ArrivalGenerators.Generate(spec);
        Assert.Equal(a1.BinCounts, a2.BinCounts);
    }

    [Fact]
    public void Legacy_Rng_Differs_From_Pcg_For_Same_Seed()
    {
        var specPcg = BasePoisson();
        var specLegacy = BasePoisson();
        specLegacy.rng = "legacy";
        var v1 = SimulationSpecValidator.Validate(specPcg);
        var v2 = SimulationSpecValidator.Validate(specLegacy);
        Assert.True(v1.IsValid && v2.IsValid);
        var aPcg = ArrivalGenerators.Generate(specPcg);
        var aLegacy = ArrivalGenerators.Generate(specLegacy);
        // Allow rare identical sample; if identical, retry with different seed to show divergence
        if (aPcg.BinCounts.SequenceEqual(aLegacy.BinCounts))
        {
            specPcg.seed = 124;
            specLegacy.seed = 124;
            aPcg = ArrivalGenerators.Generate(specPcg);
            aLegacy = ArrivalGenerators.Generate(specLegacy);
        }
        Assert.False(aPcg.BinCounts.SequenceEqual(aLegacy.BinCounts), "Expected differing sequences between pcg and legacy RNG");
    }

}
