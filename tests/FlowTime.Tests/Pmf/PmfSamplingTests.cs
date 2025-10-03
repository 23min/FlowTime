namespace FlowTime.Tests.Pmf;

/// <summary>
/// Tests for Phase 3: PMF Sampling/Compilation
/// Validates that PMF compiler generates deterministic series from distributions.
/// </summary>
public class PmfSamplingTests
{
    [Fact]
    public void Compile_WithGridBins_GeneratesSeries()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.5),
            new Core.PmfEntry(200.0, 0.5)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 10,
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (2) doesn't match grid (10)
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledSeries);
        Assert.Equal(10, result.CompiledSeries!.Length);
        
        // All values should be from PMF distribution
        Assert.All(result.CompiledSeries, v => 
            Assert.True(v == 100.0 || v == 200.0, $"Value {v} not in PMF"));
    }

    [Fact]
    public void Compile_SameSeed_ProducesSameSeries()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.3),
            new Core.PmfEntry(200.0, 0.5),
            new Core.PmfEntry(300.0, 0.2)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 99,  // Divisible by 3 (PMF length)
            Seed = 12345,
            RepeatPolicy = Core.RepeatPolicy.Repeat
        };

        // Act
        var result1 = Core.PmfCompiler.Compile(entries, "test1", options);
        var result2 = Core.PmfCompiler.Compile(entries, "test2", options);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.CompiledSeries, result2.CompiledSeries);
    }

    [Fact]
    public void Compile_DifferentSeeds_ProduceDifferentSeries()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.5),
            new Core.PmfEntry(200.0, 0.5)
        };
        
        var options1 = new Core.PmfCompilerOptions
        {
            GridBins = 50,
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (2) doesn't match grid (50)
        };
        
        var options2 = new Core.PmfCompilerOptions
        {
            GridBins = 50,
            Seed = 43,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (2) doesn't match grid (50)
        };

        // Act
        var result1 = Core.PmfCompiler.Compile(entries, "test", options1);
        var result2 = Core.PmfCompiler.Compile(entries, "test", options2);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.CompiledSeries, result2.CompiledSeries);
    }

    [Fact]
    public void Compile_WithoutGridBins_NoSeries()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.5),
            new Core.PmfEntry(200.0, 0.5)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            // No GridBins specified
            Seed = 42
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.CompiledSeries);
        Assert.NotNull(result.CompiledPmf);
    }

    [Fact]
    public void Compile_UniformDistribution_MatchesExpectedFrequencies()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(1.0, 0.25),
            new Core.PmfEntry(2.0, 0.25),
            new Core.PmfEntry(3.0, 0.25),
            new Core.PmfEntry(4.0, 0.25)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 1000,
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (4) doesn't match grid (1000)
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Count frequencies
        var freq = result.CompiledSeries!
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        // Each value should appear roughly 250 times (±50 for statistical variation)
        Assert.All(new[] { 1.0, 2.0, 3.0, 4.0 }, value =>
        {
            Assert.True(freq.ContainsKey(value));
            var count = freq[value];
            Assert.InRange(count, 200, 300);
        });
    }

    [Fact]
    public void Compile_SkewedDistribution_MatchesExpectedFrequencies()
    {
        // Arrange - heavily skewed toward 100.0
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.8),
            new Core.PmfEntry(200.0, 0.15),
            new Core.PmfEntry(300.0, 0.05)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 999,  // Divisible by 3 (PMF length)
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Count frequencies
        var freq = result.CompiledSeries!
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        // 100.0 should appear ~799 times (999 * 0.8)
        Assert.InRange(freq[100.0], 750, 850);
        
        // 200.0 should appear ~150 times (999 * 0.15)
        Assert.InRange(freq[200.0], 100, 200);
        
        // 300.0 should appear ~50 times (999 * 0.05)
        Assert.InRange(freq[300.0], 20, 80);
    }

    [Fact]
    public void Compile_SingleValue_AllSame()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(42.0, 1.0)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 20,
            Seed = 12345,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (1) doesn't match grid (20)
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(result.CompiledSeries!, v => Assert.Equal(42.0, v));
    }

    [Fact]
    public void Compile_LargeSeries_Deterministic()
    {
        // Arrange
        var entries = new[]
        {
            new Core.PmfEntry(10.0, 0.1),
            new Core.PmfEntry(20.0, 0.2),
            new Core.PmfEntry(30.0, 0.3),
            new Core.PmfEntry(40.0, 0.25),
            new Core.PmfEntry(50.0, 0.15)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 10000,
            Seed = 999,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (5) doesn't match grid (10000)
        };

        // Act - compile twice with same seed
        var result1 = Core.PmfCompiler.Compile(entries, "test", options);
        var result2 = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert - must be exactly identical
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(10000, result1.CompiledSeries!.Length);
        
        for (int i = 0; i < 10000; i++)
        {
            Assert.Equal(result1.CompiledSeries[i], result2.CompiledSeries![i]);
        }
    }

    [Fact]
    public void Compile_WithRepeatWarning_StillSamples()
    {
        // Arrange - PMF length doesn't match grid, but repeat policy allows it
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.5),
            new Core.PmfEntry(200.0, 0.5)
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 10, // PMF has 2 values, grid has 10 bins
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledSeries);
        Assert.Equal(10, result.CompiledSeries!.Length);
        Assert.Single(result.Warnings); // Should have tiling warning
        Assert.Contains("tile", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_WithNormalizationWarning_StillSamples()
    {
        // Arrange - probabilities don't sum to 1.0
        var entries = new[]
        {
            new Core.PmfEntry(100.0, 0.4),
            new Core.PmfEntry(200.0, 0.4)
            // Sum = 0.8, not 1.0
        };
        
        var options = new Core.PmfCompilerOptions
        {
            GridBins = 20,
            Seed = 42,
            RepeatPolicy = Core.RepeatPolicy.Repeat // PMF length (2) doesn't match grid (20)
        };

        // Act
        var result = Core.PmfCompiler.Compile(entries, "test", options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledSeries);
        Assert.Equal(20, result.CompiledSeries!.Length);
        Assert.Equal(2, result.Warnings.Count); // Should have normalization + tiling warnings
        Assert.Contains("renormalizing", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tile", result.Warnings[1], StringComparison.OrdinalIgnoreCase);
        
        // After renormalization, each value should have 0.5 probability
        var freq = result.CompiledSeries
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());
        
        // Should be roughly equal frequencies
        Assert.InRange(freq[100.0], 7, 13);
        Assert.InRange(freq[200.0], 7, 13);
    }
}
