using FlowTime.Cli.Configuration;
using System;
using System.IO;
using Xunit;

namespace FlowTime.Cli.Tests;

/// <summary>
/// Tests for CLI output directory configuration and environment variable support
/// </summary>
public class OutputDirectoryConfigurationTests : IDisposable
{
    private readonly string? _originalEnvVar;

    public OutputDirectoryConfigurationTests()
    {
        // Save original environment variable
        _originalEnvVar = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
    }

    public void Dispose()
    {
        // Restore original environment variable
        if (_originalEnvVar == null)
        {
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", _originalEnvVar);
        }
    }

    [Fact]
    public void GetDefaultOutputDirectory_WithEnvironmentVariable_ReturnsEnvironmentValue()
    {
        // Arrange
        var expectedPath = "/custom/env/path";
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", expectedPath);

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetDefaultOutputDirectory_NoEnvironmentVariable_ReturnsDefaultData()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "data");

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetDefaultOutputDirectory_EmptyEnvironmentVariable_FallsBackToDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", string.Empty);
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "data");

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetDefaultOutputDirectory_WhitespaceEnvironmentVariable_FallsBackToDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", "   ");
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "data");

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetDefaultOutputDirectory_RelativePathEnvironmentVariable_ReturnsRelativePath()
    {
        // Arrange
        var relativePath = "./custom-data";
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", relativePath);

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(relativePath, result);
    }

    [Fact]
    public void GetDefaultOutputDirectory_AbsolutePathEnvironmentVariable_ReturnsAbsolutePath()
    {
        // Arrange
        var absolutePath = "/var/lib/flowtime";
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", absolutePath);

        // Act
        var result = OutputDirectoryProvider.GetDefaultOutputDirectory();

        // Assert
        Assert.Equal(absolutePath, result);
    }
}
