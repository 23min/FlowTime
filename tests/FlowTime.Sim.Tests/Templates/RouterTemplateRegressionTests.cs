using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.Services;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Analysis;
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
            "supply-chain-multi-tier-classes",
            "warehouse-picker-waves"
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

    public static IEnumerable<object[]> DispatchScheduleTemplates => new[]
    {
        new object[]
        {
            "transportation-basic-classes",
            new (string NodeId, int Period, int Phase, string CapacitySeries)[]
            {
                ("airport_dispatch_queue_depth", 6, 0, "cap_airport"),
                ("downtown_dispatch_queue_depth", 8, 2, "cap_downtown"),
                ("industrial_dispatch_queue_depth", 10, 4, "cap_industrial")
            }
        },
        new object[]
        {
            "warehouse-picker-waves",
            new (string NodeId, int Period, int Phase, string CapacitySeries)[]
            {
                ("picker_wave_backlog", 4, 0, "wave_dispatch_capacity")
            }
        }
    };

    [Theory]
    [MemberData(nameof(DispatchScheduleTemplates))]
    public async Task Templates_DefineDispatchSchedules(
        string templateId,
        (string NodeId, int Period, int Phase, string CapacitySeries)[] expectations)
    {
        var yaml = await templateService.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(yaml);

        foreach (var (nodeId, period, phase, capacitySeries) in expectations)
        {
            var backlog = model.Nodes
                .FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(backlog);
            Assert.NotNull(backlog!.DispatchSchedule);
            Assert.Equal(period, backlog.DispatchSchedule!.PeriodBins);
            Assert.Equal(phase, backlog.DispatchSchedule.PhaseOffset ?? 0);
            Assert.Equal(capacitySeries, backlog.DispatchSchedule.CapacitySeries);
        }
    }

    [Theory]
    [InlineData(
        "transportation-basic-classes",
        "hub_dispatch_router",
        "hub_dispatch",
        new[] { "airport_dispatch_queue_demand", "downtown_dispatch_queue_demand", "industrial_dispatch_queue_demand" })]
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

    [Fact]
    public async Task WarehousePickerTemplate_DoesNotEmitServedExceedsArrivalsWarnings()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "warehouse-picker-waves",
            new Dictionary<string, object>());

        var analysis = TemplateInvariantAnalyzer.Analyze(yaml);
        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "served_exceeds_arrivals", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(warning.NodeId, "PickerWave", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "served_exceeds_arrivals", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(warning.NodeId, "PackAndShip", StringComparison.OrdinalIgnoreCase));
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
