using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ArgParserTests
{
    [Fact]
    public void Defaults_When_No_Args()
    {
        var opts = ArgParser.ParseArgs(Array.Empty<string>());
        Assert.Equal("", opts.Verb);
        Assert.Null(opts.Noun);
        Assert.Null(opts.TemplateId);
        Assert.Equal("yaml", opts.Format);
        Assert.False(opts.Verbose);
    }

    [Fact]
    public void Parses_List_Templates_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "templates" });
        Assert.Equal("list", opts.Verb);
        Assert.Equal("templates", opts.Noun);
    }

    [Fact]
    public void Parses_List_Models_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "models" });
        Assert.Equal("list", opts.Verb);
        Assert.Equal("models", opts.Noun);
    }

    [Fact]
    public void Parses_List_Without_Noun()
    {
        var opts = ArgParser.ParseArgs(new[] { "list" });
        Assert.Equal("list", opts.Verb);
        Assert.Null(opts.Noun); // Defaults to templates in command handler
    }

    [Fact]
    public void Parses_Show_Template_Command_With_Id()
    {
        var opts = ArgParser.ParseArgs(new[] { "show", "template", "--id", "test-template" });
        Assert.Equal("show", opts.Verb);
        Assert.Equal("template", opts.Noun);
        Assert.Equal("test-template", opts.TemplateId);
    }

    [Fact]
    public void Parses_Show_Without_Noun_With_Id()
    {
        var opts = ArgParser.ParseArgs(new[] { "show", "--id", "test-template" });
        Assert.Equal("show", opts.Verb);
        Assert.Null(opts.Noun); // Defaults to template in command handler
        Assert.Equal("test-template", opts.TemplateId);
    }

    [Fact]
    public void Parses_Generate_Command_With_Params_And_Output()
    {
        var opts = ArgParser.ParseArgs(new[] { "generate", "--id", "test-template", "--params", "params.json", "--out", "model.yaml" });
        Assert.Equal("generate", opts.Verb);
        Assert.Null(opts.Noun); // Noun is optional for generate
        Assert.Equal("test-template", opts.TemplateId);
        Assert.Equal("params.json", opts.ParamsPath);
        Assert.Equal("model.yaml", opts.OutputPath);
    }

    [Fact]
    public void Parses_Generate_Model_Command_Explicit()
    {
        var opts = ArgParser.ParseArgs(new[] { "generate", "model", "--id", "test-template" });
        Assert.Equal("generate", opts.Verb);
        Assert.Equal("model", opts.Noun);
        Assert.Equal("test-template", opts.TemplateId);
    }

    [Fact]
    public void Parses_Verbose_Flag()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "templates", "--verbose" });
        Assert.True(opts.Verbose);
    }

    [Fact]
    public void Parses_Json_Format()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "templates", "--format", "json" });
        Assert.Equal("json", opts.Format);
    }

    [Fact]
    public void Parses_Templates_Dir()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "templates", "--templates-dir", "/custom/templates" });
        Assert.Equal("/custom/templates", opts.TemplatesDir);
    }

    [Fact]
    public void Parses_Models_Dir()
    {
        var opts = ArgParser.ParseArgs(new[] { "list", "models", "--models-dir", "/custom/models" });
        Assert.Equal("/custom/models", opts.ModelsDir);
    }

    [Fact]
    public void Parses_Init_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "init" });
        Assert.Equal("init", opts.Verb);
        Assert.Null(opts.Noun);
    }

    [Fact]
    public void Parses_Init_With_Custom_Paths()
    {
        var opts = ArgParser.ParseArgs(new[] { "init", "--templates-dir", "/custom/templates", "--models-dir", "/custom/models" });
        Assert.Equal("init", opts.Verb);
        Assert.Equal("/custom/templates", opts.TemplatesDir);
        Assert.Equal("/custom/models", opts.ModelsDir);
    }

    [Fact]
    public void Parses_Show_Model_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "show", "model", "--id", "my-model" });
        Assert.Equal("show", opts.Verb);
        Assert.Equal("model", opts.Noun);
        Assert.Equal("my-model", opts.TemplateId);
    }

    [Fact]
    public void Parses_Validate_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "validate", "--id", "test-template" });
        Assert.Equal("validate", opts.Verb);
        Assert.Null(opts.Noun);
        Assert.Equal("test-template", opts.TemplateId);
    }

    [Fact]
    public void Parses_Validate_Template_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "validate", "template", "--id", "test-template", "--params", "params.json" });
        Assert.Equal("validate", opts.Verb);
        Assert.Equal("template", opts.Noun);
        Assert.Equal("test-template", opts.TemplateId);
        Assert.Equal("params.json", opts.ParamsPath);
    }

    [Fact]
    public void Parses_Validate_Params_Command()
    {
        var opts = ArgParser.ParseArgs(new[] { "validate", "params", "--id", "test-template", "--params", "params.json" });
        Assert.Equal("validate", opts.Verb);
        Assert.Equal("params", opts.Noun);
        Assert.Equal("test-template", opts.TemplateId);
        Assert.Equal("params.json", opts.ParamsPath);
    }
}
