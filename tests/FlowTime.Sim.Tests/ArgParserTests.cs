using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ArgParserTests
{
    [Fact]
    public void Defaults_When_No_Args()
    {
        var opts = ArgParser.ParseArgs(Array.Empty<string>());
        Assert.Equal("", opts.ModelPath);
        Assert.Equal("http://localhost:8080", opts.FlowTimeUrl);
        Assert.Equal("csv", opts.Format);
        Assert.False(opts.Verbose);
    }

    [Fact]
    public void Parses_Verbose_Flag()
    {
        var opts = ArgParser.ParseArgs(new[] { "--verbose" });
        Assert.True(opts.Verbose);
    }
}
