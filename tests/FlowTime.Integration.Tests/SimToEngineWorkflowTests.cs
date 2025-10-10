using FlowTime.Contracts.Services;
using FlowTime.Core;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Integration.Tests;

public class SimToEngineWorkflowTests
{
    private static NodeBasedTemplateService CreateService(string templateId, string yaml)
    {
        var preloaded = new Dictionary<string, string>
        {
            [templateId] = yaml
        };

        return new NodeBasedTemplateService(preloaded, NullLogger<NodeBasedTemplateService>.Instance);
    }

    private static string GetRepoPath(params string[] segments)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        return Path.Combine(new[] { repoRoot }.Concat(segments).ToArray());
    }

    [Fact]
    public async Task Sim_Template_Generates_Engine_Model_That_Parses()
    {
        var templateId = "m0.const.sim";
        var templateYaml = await File.ReadAllTextAsync(GetRepoPath("examples", $"{templateId}.yaml"));
        var service = CreateService(templateId, templateYaml);

        var engineYaml = await service.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());

        var validation = ModelValidator.Validate(engineYaml);
        Assert.True(validation.IsValid, $"Generated engine model invalid: {string.Join("; ", validation.Errors)}");
        Assert.Contains("schemaVersion: 1", engineYaml);

        var modelDefinition = ModelService.ParseAndConvert(engineYaml);
        var (grid, graph) = ModelParser.ParseModel(modelDefinition);
        Assert.True(grid.Bins > 0);
        Assert.NotEmpty(graph.TopologicalOrder());
    }

    [Fact]
    public async Task Expression_Nodes_Are_Converted_To_Engine_Expr_Field()
    {
        var templateId = "m0.const.sim";
        var templateYaml = await File.ReadAllTextAsync(GetRepoPath("examples", $"{templateId}.yaml"));
        var service = CreateService(templateId, templateYaml);

        var engineYaml = await service.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
        Assert.DoesNotContain("expression:", engineYaml, StringComparison.Ordinal);

        var modelDefinition = ModelService.ParseAndConvert(engineYaml);
        var exprNode = modelDefinition.Nodes.FirstOrDefault(n => n.Id.Equals("nodeA", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(exprNode);
        Assert.Equal("expr", exprNode!.Kind);
        Assert.Equal("arrivals", exprNode.Expr);
    }
}
