using FlowTime.Contracts.Services;
using FlowTime.Core;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Integration.Tests;

public class SimToEngineWorkflowTests
{
    private static TemplateService CreateService(string templateId, string yaml)
    {
        var preloaded = new Dictionary<string, string>
        {
            [templateId] = yaml
        };

        return new TemplateService(preloaded, NullLogger<TemplateService>.Instance);
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
    public async Task NetworkReliability_ArrayParameter_BindsValues()
    {
        var templateId = "network-reliability";
        var templateYaml = await File.ReadAllTextAsync(GetRepoPath("templates", $"{templateId}.yaml"));
        var service = CreateService(templateId, templateYaml);

        var overrideValues = Enumerable.Range(0, 12).Select(i => 80d + i * 2).ToArray();
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = overrideValues,
            ["rngSeed"] = 99
        };

        var engineYaml = await service.GenerateEngineModelAsync(templateId, parameters);
        Assert.Contains("values: [80, 82, 84, 86, 88, 90, 92, 94, 96, 98, 100, 102]", engineYaml);

        var modelDefinition = ModelService.ParseAndConvert(engineYaml);
        var baseNode = modelDefinition.Nodes.First(n => string.Equals(n.Id, "base_requests", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(overrideValues, baseNode.Values);
    }

    [Fact]
    public async Task NetworkReliability_ArrayParameter_LengthMismatch_Throws()
    {
        var templateId = "network-reliability";
        var templateYaml = await File.ReadAllTextAsync(GetRepoPath("templates", $"{templateId}.yaml"));
        var service = CreateService(templateId, templateYaml);

        var invalidValues = Enumerable.Range(0, 10).Select(i => (double)i).ToArray();
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = invalidValues,
            ["rngSeed"] = 42
        };

        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            service.GenerateEngineModelAsync(templateId, parameters));

        Assert.Contains("length", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task Telemetry_Mode_Model_Parses_WithFileSources()
    {
        var templateId = "it-system-microservices";
        var templateYaml = await File.ReadAllTextAsync(GetRepoPath("templates", $"{templateId}.yaml"));
        var service = CreateService(templateId, templateYaml);

        var parameters = new Dictionary<string, object>
        {
            ["telemetryRequestsSource"] = "file://telemetry/order-service_arrivals.csv"
        };

        var engineYaml = await service.GenerateEngineModelAsync(templateId, parameters, TemplateMode.Telemetry);

        var validation = ModelValidator.Validate(engineYaml);
        Assert.True(validation.IsValid, $"Telemetry model invalid: {string.Join("; ", validation.Errors)}");
        Assert.Contains("mode: telemetry", engineYaml);
        Assert.Contains("source: file://telemetry/order-service_arrivals.csv", engineYaml);

        var modelDefinition = ModelService.ParseAndConvert(engineYaml);
        var (grid, graph) = ModelParser.ParseModel(modelDefinition);
        Assert.True(grid.Bins > 0);
        Assert.NotEmpty(graph.TopologicalOrder());
    }
}
