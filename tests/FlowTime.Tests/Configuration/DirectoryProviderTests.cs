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
        Assert.EndsWith("data", dataDir);
        
        // Should not be relative to current directory when solution root is found
        Assert.DoesNotContain("FlowTime.Tests", dataDir);
        
        // Should contain the directory where FlowTime.sln exists
        var solutionRoot = DirectoryProvider.FindSolutionRoot();
        Assert.NotNull(solutionRoot);
        Assert.Equal(Path.Combine(solutionRoot, "data"), dataDir);
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
