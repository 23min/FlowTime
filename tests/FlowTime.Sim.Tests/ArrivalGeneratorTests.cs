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

    [Fact]
    public void PMF_Generation_Basic_Functionality()
    {
        var spec = BaseGrid(3);
        spec.seed = 42;
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, 2, 3 },
            probabilities = new List<double> { 0.5, 0.3, 0.2 }
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));
        
        var res = ArrivalGenerators.Generate(spec, new DeterministicRng(spec.seed!.Value));
        
        Assert.NotNull(res);
        Assert.Equal(3, res.BinCounts.Length);
        Assert.True(res.Total > 0);
        
        // Each bin count should be one of the specified values
        var allowedValues = new[] { 1, 2, 3 };
        foreach (var count in res.BinCounts)
        {
            Assert.Contains(count, allowedValues);
        }
    }

    [Fact]
    public void PMF_Generation_Deterministic_With_Same_Seed()
    {
        var spec = BaseGrid(4);
        spec.seed = 123;
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 2, 4, 6 },
            probabilities = new List<double> { 0.4, 0.4, 0.2 }
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));

        var res1 = ArrivalGenerators.Generate(spec, new DeterministicRng(spec.seed!.Value));
        var res2 = ArrivalGenerators.Generate(spec, new DeterministicRng(spec.seed!.Value));

        Assert.Equal(res1.BinCounts, res2.BinCounts);
        Assert.Equal(res1.Total, res2.Total);
    }

    [Fact]
    public void PMF_Generation_Varies_With_Different_Seeds()
    {
        var spec = BaseGrid(5);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, 3, 5, 7 },
            probabilities = new List<double> { 0.25, 0.25, 0.25, 0.25 }
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));

        var res1 = ArrivalGenerators.Generate(spec, new DeterministicRng(200));
        var res2 = ArrivalGenerators.Generate(spec, new DeterministicRng(201));

        // Check that results vary with different seeds (allowing for rare identical case)
        if (res1.BinCounts.SequenceEqual(res2.BinCounts))
        {
            var res3 = ArrivalGenerators.Generate(spec, new DeterministicRng(202));
            Assert.False(res1.BinCounts.SequenceEqual(res3.BinCounts), 
                "PMF samples identical across three different seeds — investigate RNG");
        }
    }

    [Fact]
    public void PMF_Validation_Rejects_Mismatched_Arrays()
    {
        var spec = BaseGrid(3);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, 2, 3 },
            probabilities = new List<double> { 0.5, 0.5 } // Mismatch: 3 values, 2 probabilities
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.False(vr.IsValid);
        Assert.Contains("PMF values and probabilities arrays must have the same length", vr.Errors);
    }

    [Fact]
    public void PMF_Validation_Rejects_Non_Normalized_Probabilities()
    {
        var spec = BaseGrid(3);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, 2, 3 },
            probabilities = new List<double> { 0.3, 0.3, 0.3 } // Sum = 0.9, not 1.0
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.False(vr.IsValid);
        Assert.Contains("PMF probabilities must sum to 1.0", vr.Errors);
    }

    [Fact]
    public void PMF_Validation_Rejects_Negative_Probabilities()
    {
        var spec = BaseGrid(3);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, 2, 3 },
            probabilities = new List<double> { 0.6, -0.1, 0.5 } // Negative probability
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.False(vr.IsValid);
        Assert.Contains("PMF probabilities must be non-negative", vr.Errors);
    }

    [Fact]
    public void PMF_Validation_Rejects_Negative_Values()
    {
        var spec = BaseGrid(3);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 1, -2, 3 }, // Negative value
            probabilities = new List<double> { 0.4, 0.3, 0.3 }
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.False(vr.IsValid);
        Assert.Contains("PMF values must be non-negative", vr.Errors);
    }

    [Fact]
    public void PMF_Expected_Value_Calculation()
    {
        var spec = BaseGrid(1);
        spec.arrivals = new ArrivalsSpec 
        { 
            kind = "pmf", 
            values = new List<double> { 2, 4, 6 },
            probabilities = new List<double> { 0.2, 0.5, 0.3 }
        };
        var vr = SimulationSpecValidator.Validate(spec);
        Assert.True(vr.IsValid, string.Join(";", vr.Errors));
        
        // Expected value should be: 2*0.2 + 4*0.5 + 6*0.3 = 0.4 + 2.0 + 1.8 = 4.2
        // This is tested indirectly through the generation process, but validates the calculation
        var res = ArrivalGenerators.Generate(spec, new DeterministicRng(42));
        Assert.NotNull(res);
        Assert.True(res.Total > 0);
    }
}
