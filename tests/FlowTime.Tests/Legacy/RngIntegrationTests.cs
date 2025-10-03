namespace FlowTime.Tests.Legacy;

/// <summary>
/// Tests for RNG integration with PMF sampling
/// Status: FAILING (RED) - RNG integration doesn't exist yet
/// </summary>
public class RngIntegrationTests
{
    private readonly Core.TimeGrid _grid = new(24, 1, Core.TimeUnit.Hours);
    
    [Fact]
    public void RngIntegration_PmfSampling_UsesPcg32()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 }
        };
        var seed = 12345;
        
        // Act
        var result = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        
        // Assert - Should use PCG32 internally
        Assert.NotNull(result);
        Assert.Equal(seed, result.Provenance.Seed);
    }
    
    [Fact]
    public void RngIntegration_SameSeed_ProducesSameSeries()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.3 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 300.0, Probability = 0.2 }
        };
        var seed = 999;
        
        // Act
        var result1 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        var series1 = result1.Evaluate(_grid, _ => throw new Exception());
        
        var result2 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        var series2 = result2.Evaluate(_grid, _ => throw new Exception());
        
        // Assert
        Assert.Equal(series1.Values, series2.Values);
    }
    
    [Fact]
    public void RngIntegration_DifferentSeeds_ProduceDifferentSeries()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 }
        };
        
        // Act
        var result1 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: 111);
        var series1 = result1.Evaluate(_grid, _ => throw new Exception());
        
        var result2 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: 222);
        var series2 = result2.Evaluate(_grid, _ => throw new Exception());
        
        // Assert - Should be different (with very high probability)
        Assert.NotEqual(series1.Values, series2.Values);
    }
    
    [Fact]
    public void RngIntegration_NoSeed_UsesDefaultBehavior()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 1.0 }
        };
        
        // Act - No seed specified
        var result = Core.PmfCompiler.Compile("demand", pmfData, _grid);
        
        // Assert - Should still work (may use default seed or timestamp)
        Assert.NotNull(result);
        var series = result.Evaluate(_grid, _ => throw new Exception());
        Assert.NotNull(series);
    }
    
    [Fact]
    public void RngIntegration_MultipleNodes_IndependentSeeding()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 }
        };
        var baseSeed = 42;
        
        // Act - Compile multiple nodes
        var node1 = Core.PmfCompiler.Compile("demand1", pmfData, _grid, seed: baseSeed);
        var node2 = Core.PmfCompiler.Compile("demand2", pmfData, _grid, seed: baseSeed + 1);
        
        var series1 = node1.Evaluate(_grid, _ => throw new Exception());
        var series2 = node2.Evaluate(_grid, _ => throw new Exception());
        
        // Assert - Different seeds produce different series
        Assert.NotEqual(series1.Values, series2.Values);
    }
    
    [Fact]
    public void RngIntegration_ReproducibleWorkflow_EndToEnd()
    {
        // This test verifies complete reproducibility
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.3 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 300.0, Probability = 0.2 }
        };
        var seed = 12345;
        
        // Act - Run 1
        var run1 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        var series1 = run1.Evaluate(_grid, _ => throw new Exception());
        
        // Act - Run 2 (simulating different process)
        var run2 = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        var series2 = run2.Evaluate(_grid, _ => throw new Exception());
        
        // Assert - Complete reproducibility
        Assert.Equal(series1.Values, series2.Values);
        Assert.Equal(run1.Provenance.Seed, run2.Provenance.Seed);
    }
    
    [Fact]
    public void RngIntegration_StatisticalProperties_MaintainDistribution()
    {
        // Arrange - 50/50 distribution
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 }
        };
        var seed = 999;
        
        // Act
        var result = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        var series = result.Evaluate(_grid, _ => throw new Exception());
        
        // Assert - Check distribution is approximately correct
        var count100 = series.Values.Count(v => Math.Abs(v - 100.0) < 0.001);
        var count200 = series.Values.Count(v => Math.Abs(v - 200.0) < 0.001);
        
        // With 24 bins and 50/50 split, expect roughly 12 each (allow 6-18 range)
        Assert.True(count100 >= 6 && count100 <= 18);
        Assert.True(count200 >= 6 && count200 <= 18);
        Assert.Equal(24, count100 + count200); // All bins accounted for
    }
    
    [Fact]
    public void RngIntegration_LargeGrid_EfficientSampling()
    {
        // Arrange - Large grid
        var largeGrid = new Core.TimeGrid(10000, 1, Core.TimeUnit.Minutes);
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 0.5 },
            new Core.PmfEntry { Value = 200.0, Probability = 0.5 }
        };
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = Core.PmfCompiler.Compile("demand", pmfData, largeGrid, seed: 42);
        var series = result.Evaluate(largeGrid, _ => throw new Exception());
        stopwatch.Stop();
        
        // Assert - Should complete quickly (under 100ms for 10k samples)
        Assert.True(stopwatch.ElapsedMilliseconds < 100);
        Assert.Equal(10000, series.Values.Length);
    }
    
    [Fact]
    public void RngIntegration_SeedTracking_InProvenance()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100.0, Probability = 1.0 }
        };
        var seed = 777;
        
        // Act
        var result = Core.PmfCompiler.Compile("demand", pmfData, _grid, seed: seed);
        
        // Assert
        Assert.Equal(seed, result.Provenance.Seed);
        Assert.Contains("Pcg32", result.Provenance.RngAlgorithm ?? "");
    }
}
