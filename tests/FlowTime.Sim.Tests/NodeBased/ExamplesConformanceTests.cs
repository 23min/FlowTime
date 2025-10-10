using System.Reflection;
using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class ExamplesConformanceTests
{
    private static string FindExamplesDir()
    {
        // Start from the test assembly location and walk upward until we find the repo root containing "examples"
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "examples");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent) || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException("Could not locate the 'examples' directory.");
    }

    [Fact(Skip = "Pending FlowTime.Sim template expr support after repo consolidation")]
    public void All_Examples_Parse_With_Strict_TemplateParser()
    {
        var examplesDir = FindExamplesDir();
        var files = Directory.GetFiles(examplesDir, "*.yaml", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var yaml = File.ReadAllText(file);
            var template = TemplateParser.ParseFromYaml(yaml);

            Assert.NotNull(template);
            Assert.NotNull(template.Metadata);
            Assert.False(string.IsNullOrWhiteSpace(template.Metadata.Id));
            Assert.NotNull(template.Grid);
            Assert.True(template.Grid.Bins > 0);
            Assert.True(template.Grid.BinSize > 0);
            Assert.False(string.IsNullOrWhiteSpace(template.Grid.BinUnit));
            Assert.NotNull(template.Nodes);
            Assert.NotEmpty(template.Nodes);
            Assert.NotNull(template.Outputs);
            Assert.NotEmpty(template.Outputs);
        }
    }

    [Fact(Skip = "Pending FlowTime.Sim template expr support after repo consolidation")]
    public void Examples_Rng_When_Present_Is_Pcg32()
    {
        var examplesDir = FindExamplesDir();
        var files = Directory.GetFiles(examplesDir, "*.yaml", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var yaml = File.ReadAllText(file);
            var template = TemplateParser.ParseFromYaml(yaml);
            if (template.Rng is not null)
            {
                Assert.Equal("pcg32", template.Rng.Kind);
            }
        }
    }
}
