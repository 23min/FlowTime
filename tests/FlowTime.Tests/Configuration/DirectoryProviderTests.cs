using FlowTime.Core.Configuration;

namespace FlowTime.Tests.Configuration;

public class DirectoryProviderTests
{
    [Fact]
    public void GetDefaultDataDirectory_FindsSolutionRoot()
    {
        // Act
        var dataDir = DirectoryProvider.GetDefaultDataDirectory();
        
        // Assert
        // Should find the solution root and append /data
        Assert.Contains("flowtime-vnext", dataDir);
        Assert.EndsWith("data", dataDir);
        
        // Should not be relative to current directory when solution root is found
        Assert.DoesNotContain("FlowTime.Tests", dataDir);
    }
    
    [Fact]
    public void FindSolutionRoot_ReturnsValidPath()
    {
        // Act
        var solutionRoot = DirectoryProvider.FindSolutionRoot();
        
        // Assert
        Assert.NotNull(solutionRoot);
        Assert.True(Directory.Exists(solutionRoot));
        Assert.True(File.Exists(Path.Combine(solutionRoot, "FlowTime.sln")));
    }
}
