using FlowTime.Cli.Commands;

namespace FlowTime.Cli.Tests.Commands;

public class CliCommonArgsTests
{
    [Fact]
    public void Parse_NoArgs_AllDefaults()
    {
        var parsed = CliCommonArgs.Parse(Array.Empty<string>());
        Assert.False(parsed.ShowHelp);
        Assert.Null(parsed.SpecPath);
        Assert.Null(parsed.OutputPath);
        Assert.False(parsed.NoSession);
        Assert.Null(parsed.EnginePath);
        Assert.Null(parsed.ParseError);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void Parse_HelpFlag_SetsShowHelp(string flag)
    {
        var parsed = CliCommonArgs.Parse([flag]);
        Assert.True(parsed.ShowHelp);
    }

    [Theory]
    [InlineData("--spec", "path.json")]
    [InlineData("-s", "path.json")]
    [InlineData("--model", "model.yaml")]
    [InlineData("-m", "model.yaml")]
    public void Parse_SpecFlag_SetsSpecPath(string flag, string value)
    {
        var parsed = CliCommonArgs.Parse([flag, value]);
        Assert.Equal(value, parsed.SpecPath);
        Assert.Null(parsed.ParseError);
    }

    [Fact]
    public void Parse_PositionalSpecPath()
    {
        var parsed = CliCommonArgs.Parse(["model.yaml"]);
        Assert.Equal("model.yaml", parsed.SpecPath);
    }

    [Fact]
    public void Parse_PositionalDash_AliasForStdin()
    {
        // The literal "-" as the first positional is a conventional alias for stdin.
        // It starts with '-' so must be specifically allowed through.
        var parsed = CliCommonArgs.Parse(["-"]);
        Assert.Equal("-", parsed.SpecPath);
        Assert.Null(parsed.ParseError);
    }

    [Theory]
    [InlineData("--output", "out.json")]
    [InlineData("-o", "out.json")]
    public void Parse_OutputFlag_SetsOutputPath(string flag, string value)
    {
        var parsed = CliCommonArgs.Parse([flag, value]);
        Assert.Equal(value, parsed.OutputPath);
    }

    [Fact]
    public void Parse_NoSessionFlag()
    {
        var parsed = CliCommonArgs.Parse(["--no-session"]);
        Assert.True(parsed.NoSession);
    }

    [Fact]
    public void Parse_EngineFlag()
    {
        var parsed = CliCommonArgs.Parse(["--engine", "/path/to/engine"]);
        Assert.Equal("/path/to/engine", parsed.EnginePath);
    }

    [Fact]
    public void Parse_MultipleFlags_Combined()
    {
        var parsed = CliCommonArgs.Parse([
            "--spec", "s.json",
            "-o", "out.json",
            "--no-session",
            "--engine", "/e",
        ]);
        Assert.Equal("s.json", parsed.SpecPath);
        Assert.Equal("out.json", parsed.OutputPath);
        Assert.True(parsed.NoSession);
        Assert.Equal("/e", parsed.EnginePath);
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsParseError()
    {
        var parsed = CliCommonArgs.Parse(["--wat"]);
        Assert.NotNull(parsed.ParseError);
        Assert.Contains("--wat", parsed.ParseError);
    }

    [Fact]
    public void Parse_SpecFlagWithoutValue_ReturnsParseError()
    {
        var parsed = CliCommonArgs.Parse(["--spec"]);
        Assert.NotNull(parsed.ParseError);
        Assert.Contains("--spec", parsed.ParseError);
    }

    [Fact]
    public void Parse_OutputFlagWithoutValue_ReturnsParseError()
    {
        var parsed = CliCommonArgs.Parse(["--output"]);
        Assert.NotNull(parsed.ParseError);
    }

    [Fact]
    public void Parse_EngineFlagWithoutValue_ReturnsParseError()
    {
        var parsed = CliCommonArgs.Parse(["--engine"]);
        Assert.NotNull(parsed.ParseError);
    }

    [Fact]
    public void Parse_ShortFlagWithoutValue_ReturnsParseError()
    {
        var parsed = CliCommonArgs.Parse(["-s"]);
        Assert.NotNull(parsed.ParseError);
    }
}
