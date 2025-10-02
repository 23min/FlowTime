namespace FlowTime.Tests.Pmf;

/// <summary>
/// Tests for 4-phase PMF compilation pipeline
/// Status: FAILING (RED) - PMF compilation pipeline doesn't exist yet
/// Phase 1: Validation
/// Phase 2: Grid Alignment  
/// Phase 3: Compilation
/// Phase 4: Provenance
/// </summary>
public class PmfCompilationPipelineTests
{
    // ============================================================
    // PHASE 1: VALIDATION
    // ============================================================
    
    [Fact]
    public void CompilePmf_ValidDistribution_Succeeds()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.3 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 },
            new Core.PmfEntry { Value = 300, Probability = 0.2 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledPmf);
    }
    
    [Fact]
    public void CompilePmf_ProbabilitiesSumToOne_Succeeds()
    {
        // Arrange - Exactly 1.0
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 10, Probability = 0.25 },
            new Core.PmfEntry { Value = 20, Probability = 0.25 },
            new Core.PmfEntry { Value = 30, Probability = 0.25 },
            new Core.PmfEntry { Value = 40, Probability = 0.25 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        
        // Assert
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public void CompilePmf_ProbabilitiesSumLessThanOne_ReturnsError()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.3 },
            new Core.PmfEntry { Value = 200, Probability = 0.4 }
            // Sum = 0.7, not 1.0
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("probability") && e.Contains("1.0"));
    }
    
    [Fact]
    public void CompilePmf_ProbabilitiesSumGreaterThanOne_ReturnsError()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.6 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
            // Sum = 1.1, not 1.0
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("probability") && e.Contains("1.0"));
    }
    
    [Fact]
    public void CompilePmf_NegativeProbability_ReturnsError()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = -0.1 },
            new Core.PmfEntry { Value = 200, Probability = 1.1 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => 
            e.Contains("probability") && (e.Contains("negative") || e.Contains("0")));
    }
    
    [Fact]
    public void CompilePmf_ProbabilityGreaterThanOne_ReturnsError()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.5 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("probability") && e.Contains("1.0"));
    }
    
    [Fact]
    public void CompilePmf_EmptyDistribution_ReturnsError()
    {
        // Arrange
        var pmfData = Array.Empty<Core.PmfEntry>();
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }
    
    [Fact]
    public void CompilePmf_SingleEntry_Succeeds()
    {
        // Arrange - Degenerate distribution (probability = 1.0)
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "constant");
        
        // Assert
        Assert.True(result.IsSuccess);
    }
    
    // ============================================================
    // PHASE 2: GRID ALIGNMENT
    // ============================================================
    
    [Fact]
    public void CompilePmf_WithGridAlignment_AppliesRepeatPolicy()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var grid = new Core.TimeGrid(24, 1, Core.TimeUnit.Hours);
        var options = new Core.PmfCompilerOptions
        {
            RepeatPolicy = Core.RepeatPolicy.Repeat
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand", grid, options);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Core.RepeatPolicy.Repeat, result.CompiledPmf.RepeatPolicy);
    }
    
    [Fact]
    public void CompilePmf_WithErrorPolicy_HandlesOutOfBounds()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        var grid = new Core.TimeGrid(24, 1, Core.TimeUnit.Hours);
        var options = new Core.PmfCompilerOptions
        {
            RepeatPolicy = Core.RepeatPolicy.Error
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand", grid, options);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Core.RepeatPolicy.Error, result.CompiledPmf.RepeatPolicy);
    }
    
    [Fact]
    public void CompilePmf_GridAlignment_PreservesDistribution()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.3 },
            new Core.PmfEntry { Value = 200, Probability = 0.7 }
        };
        var grid = new Core.TimeGrid(100, 1, Core.TimeUnit.Hours);
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand", grid);
        var compiled = result.CompiledPmf;
        
        // Assert - Distribution probabilities preserved
        Assert.True(result.IsSuccess);
        Assert.Equal(2, compiled.Entries.Count);
        Assert.Equal(0.3, compiled.Entries[0].Probability, precision: 5);
        Assert.Equal(0.7, compiled.Entries[1].Probability, precision: 5);
    }
    
    // ============================================================
    // PHASE 3: COMPILATION
    // ============================================================
    
    [Fact]
    public void CompilePmf_GeneratesOptimizedSampler()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.3 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 },
            new Core.PmfEntry { Value = 300, Probability = 0.2 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        var compiled = result.CompiledPmf;
        
        // Assert - Has cumulative distribution for efficient sampling
        Assert.True(result.IsSuccess);
        Assert.NotNull(compiled.CumulativeDistribution);
        Assert.Equal(3, compiled.CumulativeDistribution.Length);
    }
    
    [Fact]
    public void CompiledPmf_CumulativeDistribution_IsMonotonic()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 10, Probability = 0.2 },
            new Core.PmfEntry { Value = 20, Probability = 0.3 },
            new Core.PmfEntry { Value = 30, Probability = 0.5 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        var cdf = result.CompiledPmf.CumulativeDistribution;
        
        // Assert - CDF is strictly increasing and ends at 1.0
        Assert.True(result.IsSuccess);
        Assert.True(cdf[0] > 0);
        Assert.True(cdf[1] > cdf[0]);
        Assert.True(cdf[2] > cdf[1]);
        Assert.Equal(1.0, cdf[2], precision: 10);
    }
    
    [Fact]
    public void CompiledPmf_Sample_ReturnsDeterministicResults()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var result = Core.PmfCompiler.Compile(pmfData, "constant");
        var compiled = result.CompiledPmf;
        var rng = new Core.Pcg32(seed: 42);
        
        // Act
        var sample1 = compiled.Sample(rng);
        var sample2 = compiled.Sample(rng);
        
        // Assert - Degenerate distribution always returns same value
        Assert.Equal(100, sample1);
        Assert.Equal(100, sample2);
    }
    
    // ============================================================
    // PHASE 4: PROVENANCE
    // ============================================================
    
    [Fact]
    public void CompilePmf_IncludesProvenance()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 200, Probability = 0.5 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        var compiled = result.CompiledPmf;
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(compiled.Provenance);
        Assert.Equal("demand", compiled.Provenance.NodeName);
        Assert.Equal(2, compiled.Provenance.EntryCount);
    }
    
    [Fact]
    public void CompilePmf_Provenance_IncludesTimestamp()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var before = DateTimeOffset.UtcNow;
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        var after = DateTimeOffset.UtcNow;
        
        // Assert
        Assert.True(result.IsSuccess);
        var timestamp = result.CompiledPmf.Provenance.CompiledAt;
        Assert.True(timestamp >= before && timestamp <= after);
    }
    
    [Fact]
    public void CompilePmf_Provenance_IncludesCompilerVersion()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        
        // Assert
        Assert.True(result.IsSuccess);
        var version = result.CompiledPmf.Provenance.CompilerVersion;
        Assert.NotNull(version);
        Assert.False(string.IsNullOrWhiteSpace(version));
    }
    
    [Fact]
    public void CompilePmf_Provenance_IncludesOptions()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var options = new Core.PmfCompilerOptions
        {
            RepeatPolicy = Core.RepeatPolicy.Error
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "test", options: options);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Core.RepeatPolicy.Error, 
            result.CompiledPmf.Provenance.RepeatPolicy);
    }
}
