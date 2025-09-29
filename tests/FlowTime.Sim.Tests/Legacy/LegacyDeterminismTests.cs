using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests.Legacy;

/// <summary>
/// Tests to verify deterministic behavior of FlowTime-Sim.
/// Determinism is critical for reproducible results and debugging.
/// </summary>
[Trait("Category", "Legacy")]
[Obsolete("Legacy determinism tests - will be replaced by node-based template tests")]
public class LegacyDeterminismTests
{
    private SimulationSpec BaseConstSpec()
    {
        return new SimulationSpec
        {
            schemaVersion = 1,
            grid = new GridSpec { bins = 4, binMinutes = 60, start = "2025-01-01T00:00:00Z" },
            seed = 12345,
            arrivals = new ArrivalsSpec { kind = "const", values = new List<double> { 5, 5, 5, 5 } },
            route = new RouteSpec { id = "nodeA" }
        };
    }

    private SimulationSpec BasePoissonSpec()
    {
        return new SimulationSpec
        {
            schemaVersion = 1,
            grid = new GridSpec { bins = 4, binMinutes = 60, start = "2025-01-01T00:00:00Z" },
            seed = 999,
            arrivals = new ArrivalsSpec { kind = "poisson", rate = 3.5 },
            route = new RouteSpec { id = "nodeA" }
        };
    }

    [Fact]
    public void ConstantArrivals_SameSeed_ProducesIdenticalResults()
    {
        var spec1 = BaseConstSpec();
        var spec2 = BaseConstSpec();
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        Assert.Equal(result1.BinCounts, result2.BinCounts);
        Assert.Equal(result1.Total, result2.Total);
    }

    [Fact]
    public void PoissonArrivals_SameSeed_ProducesIdenticalResults()
    {
        var spec1 = BasePoissonSpec();
        var spec2 = BasePoissonSpec();
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        Assert.Equal(result1.BinCounts, result2.BinCounts);
        Assert.Equal(result1.Total, result2.Total);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentResults()
    {
        var spec1 = BasePoissonSpec();
        spec1.seed = 12345;
        
        var spec2 = BasePoissonSpec();
        spec2.seed = 54321;
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        // Different seeds should produce different results for Poisson arrivals
        // (allowing for rare case where they might be identical by chance)
        var identical = result1.BinCounts.SequenceEqual(result2.BinCounts) &&
                       result1.Total == result2.Total;
        
        Assert.False(identical, "Different seeds should produce different results (very rare to be identical by chance)");
    }

    [Fact]
    public void PcgRng_SameSeed_ProducesIdenticalResults()
    {
        var spec1 = BasePoissonSpec();
        spec1.rng = "pcg"; // explicit PCG
        
        var spec2 = BasePoissonSpec();
        spec2.rng = "pcg"; // explicit PCG
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        Assert.Equal(result1.BinCounts, result2.BinCounts);
        Assert.Equal(result1.Total, result2.Total);
    }

    [Fact]
    public void LegacyRng_SameSeed_ProducesIdenticalResults()
    {
        var spec1 = BasePoissonSpec();
        spec1.rng = "legacy";
        
        var spec2 = BasePoissonSpec();
        spec2.rng = "legacy";
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        Assert.Equal(result1.BinCounts, result2.BinCounts);
        Assert.Equal(result1.Total, result2.Total);
    }

    [Fact]
    public void DefaultRng_EquivalentToPcg()
    {
        var specDefault = BasePoissonSpec();
        // no explicit rng = default behavior
        
        var specPcg = BasePoissonSpec();
        specPcg.rng = "pcg";
        
        var resultDefault = ArrivalGenerators.Generate(specDefault);
        var resultPcg = ArrivalGenerators.Generate(specPcg);
        
        Assert.Equal(resultDefault.BinCounts, resultPcg.BinCounts);
        Assert.Equal(resultDefault.Total, resultPcg.Total);
    }

    [Fact]
    public void PcgVsLegacy_SameSeed_ProducesDifferentResults()
    {
        var specPcg = BasePoissonSpec();
        specPcg.rng = "pcg";
        
        var specLegacy = BasePoissonSpec();
        specLegacy.rng = "legacy";
        
        var resultPcg = ArrivalGenerators.Generate(specPcg);
        var resultLegacy = ArrivalGenerators.Generate(specLegacy);
        
        // Different RNG algorithms should produce different results with same seed
        var identical = resultPcg.BinCounts.SequenceEqual(resultLegacy.BinCounts);
        
        Assert.False(identical, "PCG and Legacy RNG should produce different results with same seed");
    }

    [Fact]
    public void MultipleRuns_ConsistentDeterminism()
    {
        var spec = BasePoissonSpec();
        var results = new List<ArrivalGenerationResult>();
        
        // Run simulation 5 times
        for (int i = 0; i < 5; i++)
        {
            results.Add(ArrivalGenerators.Generate(spec));
        }
        
        // All results should be identical
        var firstResult = results[0];
        foreach (var result in results.Skip(1))
        {
            Assert.Equal(firstResult.BinCounts, result.BinCounts);
            Assert.Equal(firstResult.Total, result.Total);
        }
    }

    [Fact]
    public void SeedAndRng_BothSpecified_StillDeterministic()
    {
        var spec1 = BasePoissonSpec();
        spec1.seed = 42;
        spec1.rng = "pcg";
        
        var spec2 = BasePoissonSpec();
        spec2.seed = 42;
        spec2.rng = "pcg";
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        Assert.Equal(result1.BinCounts, result2.BinCounts);
        Assert.Equal(result1.Total, result2.Total);
    }

    [Fact]
    public void ConstantArrivals_AlwaysDeterministic()
    {
        var spec = BaseConstSpec();
        
        // Constant arrivals should be completely deterministic regardless of RNG
        var result1 = ArrivalGenerators.Generate(spec);
        var result2 = ArrivalGenerators.Generate(spec);
        
        Assert.Equal(new[] { 5, 5, 5, 5 }, result1.BinCounts);
        Assert.Equal(new[] { 5, 5, 5, 5 }, result2.BinCounts);
        Assert.Equal(20, result1.Total);
        Assert.Equal(20, result2.Total);
    }

    [Fact]
    public void PoissonWithDifferentRates_DifferentResults()
    {
        var spec1 = BasePoissonSpec();
        spec1.arrivals!.rate = 2.0;
        
        var spec2 = BasePoissonSpec();
        spec2.arrivals!.rate = 5.0;
        
        var result1 = ArrivalGenerators.Generate(spec1);
        var result2 = ArrivalGenerators.Generate(spec2);
        
        // Different rates should generally produce different totals
        // (though theoretically could be same by chance)
        Assert.NotEqual(result1.Total, result2.Total);
    }
}
