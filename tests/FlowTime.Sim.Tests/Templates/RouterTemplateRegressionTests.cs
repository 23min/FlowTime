using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.Services;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public class RouterTemplateRegressionTests
{
    private readonly TemplateService templateService;

    public RouterTemplateRegressionTests()
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templateIds = new[]
        {
            "transportation-basic-classes",
            "supply-chain-multi-tier-classes"
        };

        var preloaded = templateIds.ToDictionary(
            id => id,
            id =>
            {
                var path = Path.Combine(templatesDirectory, $"{id}.yaml");
                Assert.True(File.Exists(path), $"Template file missing: {path}");
                return File.ReadAllText(path);
            });

        templateService = new TemplateService(preloaded, NullLogger<TemplateService>.Instance);
    }

    [Theory]
    [InlineData(
        "transportation-basic-classes",
        "hub_dispatch_router",
        "hub_dispatch",
        new[] { "airport_dispatch_queue_inflow", "downtown_dispatch_queue_inflow", "industrial_dispatch_queue_inflow" })]
    [InlineData(
        "supply-chain-multi-tier-classes",
        "returns_router",
        "returns_processed",
        new[] { "restock_inflow", "recover_inflow", "scrap_inflow" })]
    public async Task CanonicalModel_DefinesRouterNodes(
        string templateId,
        string routerId,
        string expectedQueue,
        string[] expectedTargets)
    {
        var yaml = await templateService.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(yaml);

        var router = model.Nodes
            .FirstOrDefault(n => string.Equals(n.Kind, "router", StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(n.Id, routerId, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(router);
        Assert.Equal(expectedQueue, router!.Router?.Inputs?.Queue);

        var actualTargets = router.Router?.Routes?.Select(r => r.Target).ToArray() ?? Array.Empty<string>();
        Assert.NotEmpty(actualTargets);

        foreach (var expected in expectedTargets)
        {
            Assert.Contains(expected, actualTargets);
        }
    }

    private static string ResolveTemplatesDirectory()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            var solutionPath = Path.Combine(directory, "FlowTime.sln");
            if (File.Exists(solutionPath))
            {
                var templatesPath = Path.Combine(directory, "templates");
                if (Directory.Exists(templatesPath))
                {
                    return templatesPath;
                }
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new DirectoryNotFoundException("Unable to locate templates directory relative to solution root.");
    }
}
