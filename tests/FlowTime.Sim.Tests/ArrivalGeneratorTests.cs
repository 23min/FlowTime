using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ArrivalGeneratorTests
{
    private SimulationSpec BaseGrid(int bins = 5) => new()
    {
        grid = new GridSpec { bins = bins, binMinutes = 60, start = "2025-01-01T00:00:00Z" },
        route = new RouteSpec { id = "n1" }
    };

    [Fact]
    public void Const_Generation_Returns_Specified_Counts()
    {
        var spec = BaseGrid(3);
        spec.arrivals = new ArrivalsSpec { kind = "const", values = new List<double> { 1, 2, 3 } };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));
        var res = ArrivalGenerators.Generate(spec);
        Assert.Equal(new[] { 1, 2, 3 }, res.BinCounts);
        Assert.Equal(6, res.Total);
    }

    [Fact]
    public void Poisson_Generation_Deterministic_With_Same_Seed()
    {
        var spec = BaseGrid(4);
        spec.seed = 42;
        spec.arrivals = new ArrivalsSpec { kind = "poisson", rate = 3.5 };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));

        var res1 = ArrivalGenerators.Generate(spec, new DeterministicRng(spec.seed!.Value));
        var res2 = ArrivalGenerators.Generate(spec, new DeterministicRng(spec.seed!.Value));

        Assert.Equal(res1.BinCounts, res2.BinCounts);
    }

    [Fact]
    public void Poisson_Generation_Varies_With_Different_Seeds()
    {
        var spec = BaseGrid(6);
        spec.seed = 100;
        spec.arrivals = new ArrivalsSpec { kind = "poisson", rate = 5.0 };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));

        var res1 = ArrivalGenerators.Generate(spec, new DeterministicRng(100));
        var res2 = ArrivalGenerators.Generate(spec, new DeterministicRng(101));

        // It's possible (though unlikely) they are identical; allow rare fluke by checking at least one difference else retry once.
        if (res1.BinCounts.SequenceEqual(res2.BinCounts))
        {
            var res3 = ArrivalGenerators.Generate(spec, new DeterministicRng(102));
            Assert.False(res1.BinCounts.SequenceEqual(res3.BinCounts), "Poisson samples identical across three different seeds — extremely unlikely; investigate RNG");
        }
    }
}
