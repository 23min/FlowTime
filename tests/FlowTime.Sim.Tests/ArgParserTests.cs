using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ArgParserTests
{
    [Fact]
    public void Defaults_When_No_Args()
    {
        // Clear environment variables to test pure defaults
        var originalUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL");
        var originalVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION");
        
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", null);
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", null);
            
            var opts = ArgParser.ParseArgs(Array.Empty<string>());
            Assert.Equal("", opts.ModelPath);
            Assert.Equal("http://localhost:8080", opts.FlowTimeUrl);
            Assert.Equal("v1", opts.ApiVersion);
            Assert.Equal("csv", opts.Format);
            Assert.False(opts.Verbose);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", originalUrl);
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", originalVersion);
        }
    }

    [Fact]
    public void Uses_Environment_Variables_When_Set()
    {
        // Set environment variables
        var originalUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL");
        var originalVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION");
        
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", "http://test-api:9000");
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", "v2");
            
            var opts = ArgParser.ParseArgs(Array.Empty<string>());
            Assert.Equal("http://test-api:9000", opts.FlowTimeUrl);
            Assert.Equal("v2", opts.ApiVersion);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", originalUrl);
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", originalVersion);
        }
    }

    [Fact]
    public void Command_Line_Args_Override_Environment_Variables()
    {
        // Set environment variables
        var originalUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL");
        var originalVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION");
        
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", "http://env-api:8000");
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", "v2");
            
            var opts = ArgParser.ParseArgs(new[] { "--flowtime", "http://cli-api:7000", "--api-version", "v3" });
            // Command line should override environment
            Assert.Equal("http://cli-api:7000", opts.FlowTimeUrl);
            Assert.Equal("v3", opts.ApiVersion);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("FLOWTIME_API_BASEURL", originalUrl);
            Environment.SetEnvironmentVariable("FLOWTIME_API_VERSION", originalVersion);
        }
    }

    [Fact]
    public void Parses_Verbose_Flag()
    {
        var opts = ArgParser.ParseArgs(new[] { "--verbose" });
        Assert.True(opts.Verbose);
    }
}
