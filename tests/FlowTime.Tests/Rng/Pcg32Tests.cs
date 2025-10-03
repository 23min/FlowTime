namespace FlowTime.Tests.Rng;

/// <summary>
/// Tests for PCG32 random number generator algorithm
/// Status: FAILING (RED) - PCG32 implementation doesn't exist yet
/// </summary>
public class Pcg32Tests
{
    [Fact]
    public void Pcg32_WithSeed_Initializes()
    {
        // Arrange & Act
        var rng = new Core.Pcg32(seed: 12345);
        
        // Assert
        Assert.NotNull(rng);
    }
    
    [Fact]
    public void Pcg32_NextUInt32_ReturnsValue()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        
        // Act
        var value = rng.NextUInt32();
        
        // Assert
        Assert.True(value >= 0);
    }
    
    [Fact]
    public void Pcg32_SameSeed_ProducesSameSequence()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 42);
        var rng2 = new Core.Pcg32(seed: 42);
        
        // Act - Generate 100 numbers from each
        var sequence1 = Enumerable.Range(0, 100).Select(_ => rng1.NextUInt32()).ToArray();
        var sequence2 = Enumerable.Range(0, 100).Select(_ => rng2.NextUInt32()).ToArray();
        
        // Assert
        Assert.Equal(sequence1, sequence2);
    }
    
    [Fact]
    public void Pcg32_DifferentSeeds_ProduceDifferentSequences()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 111);
        var rng2 = new Core.Pcg32(seed: 222);
        
        // Act
        var sequence1 = Enumerable.Range(0, 100).Select(_ => rng1.NextUInt32()).ToArray();
        var sequence2 = Enumerable.Range(0, 100).Select(_ => rng2.NextUInt32()).ToArray();
        
        // Assert
        Assert.NotEqual(sequence1, sequence2);
    }
    
    [Fact]
    public void Pcg32_NextDouble_ReturnsBetweenZeroAndOne()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        
        // Act & Assert - Generate 1000 samples
        for (int i = 0; i < 1000; i++)
        {
            var value = rng.NextDouble();
            Assert.True(value >= 0.0);
            Assert.True(value < 1.0);
        }
    }
    
    [Fact]
    public void Pcg32_NextDouble_UniformDistribution()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        var buckets = new int[10];
        
        // Act - Generate 10000 samples and count distribution
        for (int i = 0; i < 10000; i++)
        {
            var value = rng.NextDouble();
            var bucket = (int)(value * 10);
            if (bucket >= 0 && bucket < 10)
                buckets[bucket]++;
        }
        
        // Assert - Each bucket should have roughly 1000 samples (allow 800-1200)
        foreach (var count in buckets)
        {
            Assert.True(count >= 800 && count <= 1200, 
                $"Bucket count {count} outside expected range [800, 1200]");
        }
    }
    
    [Fact]
    public void Pcg32_NextInt_ReturnsWithinRange()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        
        // Act & Assert
        for (int i = 0; i < 1000; i++)
        {
            var value = rng.NextInt(0, 100);
            Assert.True(value >= 0);
            Assert.True(value < 100);
        }
    }
    
    [Fact]
    public void Pcg32_NextInt_InvalidRange_Throws()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => rng.NextInt(100, 0));
        Assert.Throws<ArgumentException>(() => rng.NextInt(50, 50));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    public void Pcg32_KnownSeeds_ProduceExpectedSequence(int seed)
    {
        // Arrange
        var rng = new Core.Pcg32(seed: seed);
        
        // Act - Generate first value
        var first = rng.NextUInt32();
        
        // Reset with same seed
        var rng2 = new Core.Pcg32(seed: seed);
        var firstAgain = rng2.NextUInt32();
        
        // Assert
        Assert.Equal(first, firstAgain);
    }
    
    [Fact]
    public void Pcg32_LargeSequence_RemainsConsistent()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 999);
        var rng2 = new Core.Pcg32(seed: 999);
        
        // Act - Generate 10000 numbers
        var sequence1 = Enumerable.Range(0, 10000).Select(_ => rng1.NextUInt32()).ToArray();
        var sequence2 = Enumerable.Range(0, 10000).Select(_ => rng2.NextUInt32()).ToArray();
        
        // Assert
        Assert.Equal(sequence1, sequence2);
    }
    
    [Fact]
    public void Pcg32_FullRange_CoversUInt32()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        var samples = new HashSet<uint>();
        
        // Act - Generate many samples to ensure good coverage
        for (int i = 0; i < 10000; i++)
        {
            samples.Add(rng.NextUInt32());
        }
        
        // Assert - Should have high uniqueness (>9000 unique values)
        Assert.True(samples.Count > 9000, 
            $"Only {samples.Count} unique values out of 10000 samples");
    }
    
    [Fact]
    public void Pcg32_Clone_ProducesSameSequence()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        rng.NextUInt32(); // Advance state
        rng.NextUInt32();
        
        // Act
        var clone = rng.Clone();
        
        var value1 = rng.NextUInt32();
        var value2 = clone.NextUInt32();
        
        // Assert
        Assert.Equal(value1, value2);
    }
    
    [Fact]
    public void Pcg32_State_IsSerializable()
    {
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        rng.NextUInt32(); // Advance state
        
        // Act
        var state = rng.GetState();
        var restored = Core.Pcg32.FromState(state);
        
        var value1 = rng.NextUInt32();
        var value2 = restored.NextUInt32();
        
        // Assert
        Assert.Equal(value1, value2);
    }
}
