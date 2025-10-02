namespace FlowTime.Tests.Pmf;

/// <summary>
/// Tests for PMF compilation provenance tracking
/// Status: FAILING (RED) - Provenance tracking doesn't exist yet
/// </summary>
public class PmfProvenanceTests
{
    [Fact]
    public void PmfProvenance_IncludesNodeName()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demandNode");
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("demandNode", result.CompiledPmf.Provenance.NodeName);
    }
    
    [Fact]
    public void PmfProvenance_IncludesEntryCount()
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
        Assert.Equal(3, result.CompiledPmf.Provenance.EntryCount);
    }
    
    [Fact]
    public void PmfProvenance_IncludesOriginalEntries()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 0.6 },
            new Core.PmfEntry { Value = 200, Probability = 0.4 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        var provenance = result.CompiledPmf.Provenance;
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(provenance.OriginalEntries);
        Assert.Equal(2, provenance.OriginalEntries.Count);
        Assert.Equal(100, provenance.OriginalEntries[0].Value);
        Assert.Equal(0.6, provenance.OriginalEntries[0].Probability);
    }
    
    [Fact]
    public void PmfProvenance_IncludesCompilationTimestamp()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var before = DateTimeOffset.UtcNow;
        
        // Act
        System.Threading.Thread.Sleep(10); // Ensure time passes
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        System.Threading.Thread.Sleep(10);
        var after = DateTimeOffset.UtcNow;
        
        // Assert
        Assert.True(result.IsSuccess);
        var timestamp = result.CompiledPmf.Provenance.CompiledAt;
        Assert.True(timestamp >= before);
        Assert.True(timestamp <= after);
    }
    
    [Fact]
    public void PmfProvenance_IncludesCompilerVersion()
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
        Assert.Matches(@"\d+\.\d+\.\d+", version); // Semantic version format
    }
    
    [Fact]
    public void PmfProvenance_IncludesRepeatPolicy()
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
    
    [Fact]
    public void PmfProvenance_DefaultRepeatPolicy_IsRepeat()
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
        Assert.Equal(Core.RepeatPolicy.Repeat, 
            result.CompiledPmf.Provenance.RepeatPolicy);
    }
    
    [Fact]
    public void PmfProvenance_IncludesValueRange()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 50, Probability = 0.3 },
            new Core.PmfEntry { Value = 100, Probability = 0.5 },
            new Core.PmfEntry { Value = 150, Probability = 0.2 }
        };
        
        // Act
        var result = Core.PmfCompiler.Compile(pmfData, "demand");
        var provenance = result.CompiledPmf.Provenance;
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50, provenance.MinValue);
        Assert.Equal(150, provenance.MaxValue);
    }
    
    [Fact]
    public void PmfProvenance_SerializesToJson()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        var provenance = result.CompiledPmf.Provenance;
        
        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(provenance);
        
        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"nodeName\":\"test\"", json);
        Assert.Contains("\"entryCount\":1", json);
    }
    
    [Fact]
    public void PmfProvenance_DeserializesFromJson()
    {
        // Arrange
        var json = @"{
            ""nodeName"": ""testNode"",
            ""entryCount"": 2,
            ""compiledAt"": ""2025-10-02T10:00:00Z"",
            ""compilerVersion"": ""0.5.0"",
            ""repeatPolicy"": ""Repeat""
        }";
        
        // Act
        var provenance = System.Text.Json.JsonSerializer.Deserialize<Core.PmfProvenance>(json);
        
        // Assert
        Assert.NotNull(provenance);
        Assert.Equal("testNode", provenance.NodeName);
        Assert.Equal(2, provenance.EntryCount);
    }
    
    [Fact]
    public void PmfProvenance_ImmutableAfterCreation()
    {
        // Arrange
        var pmfData = new[]
        {
            new Core.PmfEntry { Value = 100, Probability = 1.0 }
        };
        var result = Core.PmfCompiler.Compile(pmfData, "test");
        var provenance = result.CompiledPmf.Provenance;
        
        // Act & Assert - Provenance properties should be read-only
        var type = provenance.GetType();
        var nodeNameProp = type.GetProperty("NodeName");
        Assert.NotNull(nodeNameProp);
        Assert.Null(nodeNameProp.SetMethod); // No setter
    }
}
