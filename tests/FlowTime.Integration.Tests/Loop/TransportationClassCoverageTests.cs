using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.API.Services;
using FlowTime.Contracts.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Integration.Tests.Loop;

public sealed class TransportationClassCoverageTests
{
    [Fact]
    public async Task TransportationQueuesExposeClassSeries()
    {
        using var temp = new TempDirectory();
        var templateService = await CreateTemplateServiceAsync();
        var engineModelYaml = await templateService.GenerateEngineModelAsync(
            "transportation-basic-classes",
            new Dictionary<string, object>(),
            TemplateMode.Simulation);

        var modelDefinition = ModelService.ParseAndConvert(engineModelYaml);
        var (grid, graph) = ModelParser.ParseModel(modelDefinition);
        var context = graph.Evaluate(grid)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var result = await RunArtifactWriter.WriteArtifactsAsync(new RunArtifactWriter.WriteRequest
        {
            Model = modelDefinition,
            Grid = grid,
            Context = context,
            SpecText = engineModelYaml,
            OutputDirectory = temp.Path,
            DeterministicRunId = true
        });

        var stateService = TestStateQueryServiceFactory.Create(result.RunDirectory);
        var endBin = Math.Min(1, grid.Length - 1);
        var window = await stateService.GetStateWindowAsync(
            result.RunId,
            startBin: 0,
            endBin: endBin,
            mode: GraphQueryMode.Operational,
            cancellationToken: default);

        AssertClassCoverage(window, "HubQueue", "Airport", "Downtown", "Industrial");
        AssertClassCoverage(window, "AirportDispatchQueue", "Airport");
        AssertClassCoverage(window, "DowntownDispatchQueue", "Downtown");
        AssertClassCoverage(window, "IndustrialDispatchQueue", "Industrial");
    }

    private static async Task<TemplateService> CreateTemplateServiceAsync()
    {
        var templatesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../templates"));
        Assert.True(Directory.Exists(templatesDirectory), $"Templates directory not found at {templatesDirectory}");

        var templatePath = Path.Combine(templatesDirectory, "transportation-basic-classes.yaml");
        Assert.True(File.Exists(templatePath), "Template transportation-basic-classes.yaml not found.");

        var yamlById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transportation-basic-classes"] = await File.ReadAllTextAsync(templatePath)
        };

        return new TemplateService(yamlById, NullLogger<TemplateService>.Instance);
    }

    private static void AssertClassCoverage(StateWindowResponse window, string nodeId, params string[] expectedClasses)
    {
        var node = window.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(node);
        Assert.NotNull(node!.ByClass);

        foreach (var classId in expectedClasses)
        {
            Assert.True(node.ByClass!.ContainsKey(classId), $"{nodeId} missing class '{classId}'.");
        }
    }
}
