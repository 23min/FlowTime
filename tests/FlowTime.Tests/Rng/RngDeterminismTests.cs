namespace FlowTime.Tests.Rng;

/// <summary>
/// Tests for RNG determinism guarantees
/// Status: FAILING (RED) - Determinism features don't exist yet
/// </summary>
public class RngDeterminismTests
{
    [Fact]
    public void Determinism_SameSeedProducesSameResults_SingleValue()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 42);
        var rng2 = new Core.Pcg32(seed: 42);
        
        // Act
        var value1 = rng1.NextDouble();
        var value2 = rng2.NextDouble();
        
        // Assert
        Assert.Equal(value1, value2);
    }
    
    [Fact]
    public void Determinism_SameSeedProducesSameResults_Array()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 12345);
        var rng2 = new Core.Pcg32(seed: 12345);
        
        // Act
        var array1 = new double[1000];
        var array2 = new double[1000];
        
        for (int i = 0; i < 1000; i++)
        {
            array1[i] = rng1.NextDouble();
            array2[i] = rng2.NextDouble();
        }
        
        // Assert
        Assert.Equal(array1, array2);
    }
    
    [Fact]
    public void Determinism_AcrossProcessRuns_ProducesSameSequence()
    {
        // This simulates multiple process runs with same seed
        // Arrange & Act
        var results1 = GenerateSequenceInIsolation(seed: 999);
        var results2 = GenerateSequenceInIsolation(seed: 999);
        
        // Assert
        Assert.Equal(results1, results2);
    }
    
    private static uint[] GenerateSequenceInIsolation(int seed)
    {
        var rng = new Core.Pcg32(seed: seed);
        return Enumerable.Range(0, 100).Select(_ => rng.NextUInt32()).ToArray();
    }
    
    [Fact]
    public void Determinism_CrossPlatform_ConsistentAlgorithm()
    {
        // PCG32 algorithm should produce same results regardless of platform
        // Arrange
        var rng = new Core.Pcg32(seed: 12345);
        
        // Act - First 5 values with seed 12345 (known reference values)
        var values = new uint[]
        {
            rng.NextUInt32(),
            rng.NextUInt32(),
            rng.NextUInt32(),
            rng.NextUInt32(),
            rng.NextUInt32()
        };
        
        // Assert - These should match PCG32 reference implementation
        // (Actual values depend on PCG32 spec - update after implementation)
        Assert.Equal(5, values.Length);
        Assert.All(values, v => Assert.True(v > 0)); // All non-zero
    }
    
    [Fact]
    public void Determinism_AfterStateRestore_ContinuesCorrectly()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 42);
        rng1.NextUInt32(); // Advance
        rng1.NextUInt32();
        
        var state = rng1.GetState();
        
        // Act
        var value1 = rng1.NextUInt32();
        
        var rng2 = Core.Pcg32.FromState(state);
        var value2 = rng2.NextUInt32();
        
        // Assert
        Assert.Equal(value1, value2);
    }
    
    [Fact]
    public void Determinism_InterleavedSequences_Independent()
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: 111);
        var rng2 = new Core.Pcg32(seed: 222);
        
        // Act - Interleave calls
        var seq1 = new List<uint>();
        var seq2 = new List<uint>();
        
        for (int i = 0; i < 100; i++)
        {
            seq1.Add(rng1.NextUInt32());
            seq2.Add(rng2.NextUInt32());
        }
        
        // Verify independence by recreating
        var rng1Check = new Core.Pcg32(seed: 111);
        var seq1Check = Enumerable.Range(0, 100).Select(_ => rng1Check.NextUInt32()).ToArray();
        
        // Assert
        Assert.Equal(seq1, seq1Check);
    }
    
    [Fact]
    public void Determinism_ThreadSafety_NotRequired()
    {
        // PCG32 is NOT thread-safe by design (for performance)
        // Each thread should have its own instance
        
        // Arrange
        var rng1 = new Core.Pcg32(seed: 42);
        var rng2 = new Core.Pcg32(seed: 42);
        
        // Act - Sequential access (not parallel)
        var values1 = Enumerable.Range(0, 100).Select(_ => rng1.NextUInt32()).ToArray();
        var values2 = Enumerable.Range(0, 100).Select(_ => rng2.NextUInt32()).ToArray();
        
        // Assert
        Assert.Equal(values1, values2);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Determinism_EdgeCaseSeeds_ProduceConsistentResults(int seed)
    {
        // Arrange
        var rng1 = new Core.Pcg32(seed: seed);
        var rng2 = new Core.Pcg32(seed: seed);
        
        // Act
        var seq1 = Enumerable.Range(0, 50).Select(_ => rng1.NextUInt32()).ToArray();
        var seq2 = Enumerable.Range(0, 50).Select(_ => rng2.NextUInt32()).ToArray();
        
        // Assert
        Assert.Equal(seq1, seq2);
    }
    
    [Fact]
    public void Determinism_Documentation_SpecifiesGuarantees()
    {
        // This test documents the determinism guarantees:
        // 1. Same seed → same sequence (always)
        // 2. Cross-platform consistent (PCG32 standard algorithm)
        // 3. State serializable and restorable
        // 4. NOT thread-safe (use separate instances per thread)
        
        var rng = new Core.Pcg32(seed: 42);
        Assert.NotNull(rng);
    }
}
