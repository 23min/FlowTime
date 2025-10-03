namespace FlowTime.Tests.Legacy;

/// <summary>
/// Tests for PMF grid alignment and repeat policies
/// Status: FAILING (RED) - Grid alignment logic doesn't exist yet
/// </summary>
public class PmfGridAlignmentTests
{
    [Fact]
    public void PmfNode_WithRepeatPolicy_GeneratesRepeatingPattern()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData, Core.RepeatPolicy.Repeat);
        var grid = new Core.TimeGrid(48, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 42);
        
        // Act
        var series = pmfNode.Evaluate(grid, rng);
        
        // Assert
        Assert.Equal(48, series.Length);
        // Each value should be either 100 or 200
        Assert.All(series, v => Assert.True(v == 100 || v == 200));
    }
    
    [Fact]
    public void PmfNode_WithErrorPolicy_ThrowsOnOutOfBounds()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData, Core.RepeatPolicy.Error);
        var grid = new Core.TimeGrid(100, 1, Core.TimeUnit.Hours); // More bins than samples
        var rng = new Core.Pcg32(seed: 42);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            pmfNode.Evaluate(grid, rng));
    }
    
    [Fact]
    public void PmfNode_DefaultRepeatPolicy_IsRepeat()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        
        // Act
        var pmfNode = new Core.PmfNode("demand", pmfData);
        
        // Assert
        Assert.Equal(Core.RepeatPolicy.Repeat, pmfNode.RepeatPolicy);
    }
    
    [Fact]
    public void PmfNode_RepeatPolicy_AppliesToAllBins()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData, Core.RepeatPolicy.Repeat);
        var grid = new Core.TimeGrid(1000, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 123);
        
        // Act
        var series = pmfNode.Evaluate(grid, rng);
        
        // Assert
        Assert.Equal(1000, series.Length);
        Assert.All(series, v => Assert.True(v == 100 || v == 200));
    }
    
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void PmfNode_DifferentGridSizes_HandlesRepeat(int bins)
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 50, Probability = 0.3 },
            new Core.PmfEntry { Value = 100, Probability = 0.7 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData, Core.RepeatPolicy.Repeat);
        var grid = new Core.TimeGrid(bins, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 42);
        
        // Act
        var series = pmfNode.Evaluate(grid, rng);
        
        // Assert
        Assert.Equal(bins, series.Length);
        Assert.All(series, v => Assert.True(v == 50 || v == 100));
    }
    
    [Fact]
    public void PmfNode_GridAlignment_PreservesDistribution()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.3 },
            new Core.PmfEntry { Value = 200, Probability = 0.7 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData, Core.RepeatPolicy.Repeat);
        var grid = new Core.TimeGrid(10000, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 42);
        
        // Act
        var series = pmfNode.Evaluate(grid, rng);
        
        // Assert - Distribution should be approximately 30% / 70%
        var count100 = series.Count(v => v == 100);
        var count200 = series.Count(v => v == 200);
        var ratio100 = count100 / 10000.0;
        var ratio200 = count200 / 10000.0;
        
        Assert.InRange(ratio100, 0.25, 0.35); // 30% ± 5%
        Assert.InRange(ratio200, 0.65, 0.75); // 70% ± 5%
    }
    
    [Fact]
    public void PmfNode_ErrorPolicy_MessageIncludesNodeName()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var pmfNode = new Core.PmfNode("demandNode", pmfData, Core.RepeatPolicy.Error);
        var grid = new Core.TimeGrid(100, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 42);
        
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            pmfNode.Evaluate(grid, rng));
        
        Assert.Contains("demandNode", ex.Message);
    }
    
    [Fact]
    public void PmfNode_WithTimeGrid_SamplesPerBin()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData);
        var grid = new Core.TimeGrid(24, 1, Core.TimeUnit.Hours);
        var rng = new Core.Pcg32(seed: 42);
        
        // Act
        var series = pmfNode.Evaluate(grid, rng);
        
        // Assert - One sample per bin
        Assert.Equal(24, series.Length);
    }
    
    [Fact]
    public void PmfNode_MultipleEvaluations_UseDifferentSeeds()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData);
        var grid = new Core.TimeGrid(100, 1, Core.TimeUnit.Hours);
        var rng1 = new Core.Pcg32(seed: 42);
        var rng2 = new Core.Pcg32(seed: 43);
        
        // Act
        var series1 = pmfNode.Evaluate(grid, rng1);
        var series2 = pmfNode.Evaluate(grid, rng2);
        
        // Assert - Different seeds produce different sequences
        Assert.NotEqual(series1, series2);
    }
    
    [Fact]
    public void PmfNode_SameSeed_ProducesSameResults()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var pmfNode = new Core.PmfNode("demand", pmfData);
        var grid = new Core.TimeGrid(100, 1, Core.TimeUnit.Hours);
        var rng1 = new Core.Pcg32(seed: 42);
        var rng2 = new Core.Pcg32(seed: 42);
        
        // Act
        var series1 = pmfNode.Evaluate(grid, rng1);
        var series2 = pmfNode.Evaluate(grid, rng2);
        
        // Assert - Same seed produces identical sequences
        Assert.Equal(series1, series2);
    }
}
