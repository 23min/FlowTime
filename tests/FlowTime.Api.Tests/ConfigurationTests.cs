using Microsoft.Extensions.Configuration;
using Xunit;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for configuration logic, particularly data storage directory configuration
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void GetArtifactsDirectory_WithEnvironmentVariable_ReturnsEnvironmentValue()
    {
        // Arrange
        var testPath = "/tmp/test-env-path";
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", testPath);
        
        var configuration = new ConfigurationBuilder().Build();

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(testPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }

    [Fact]
    public void GetArtifactsDirectory_WithConfiguration_ReturnsConfigValue()
    {
        // Arrange
        var testPath = "/tmp/test-config-path";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = testPath
            })
            .Build();

        // Ensure no environment variable
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(testPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }

    [Fact]
    public void GetArtifactsDirectory_NoConfiguration_ReturnsDefaultData()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var expectedPath = "/workspaces/flowtime-vnext/data";

        // Ensure no environment variable or configuration
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(expectedPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }

    [Fact]
    public void GetArtifactsDirectory_EnvironmentOverridesConfiguration()
    {
        // Arrange
        var envPath = "/tmp/test-env-priority";
        var configPath = "/tmp/test-config-priority";
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = configPath
            })
            .Build();

        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", envPath);

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(envPath, result);
            Assert.NotEqual(configPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }

    [Fact]
    public void GetArtifactsDirectory_EmptyEnvironmentVariable_FallsBackToConfiguration()
    {
        // Arrange
        var configPath = "/tmp/test-config-fallback";
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = configPath
            })
            .Build();

        // Set empty environment variable (should be ignored)
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", "");

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(configPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }

    [Fact]
    public void GetArtifactsDirectory_WhitespaceEnvironmentVariable_FallsBackToConfiguration()
    {
        // Arrange
        var configPath = "/tmp/test-config-whitespace";
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = configPath
            })
            .Build();

        // Set whitespace-only environment variable (should be ignored)
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", "   ");

        try
        {
            // Act
            var result = Program.GetArtifactsDirectory(configuration);

            // Assert
            Assert.Equal(configPath, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
    }
}
