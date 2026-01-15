using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.Services;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Analysis;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace FlowTime.Sim.Tests.Templates;

public class RouterTemplateRegressionTests
{
    private readonly TemplateService templateService;
    private static IReadOnlyList<object[]>? cachedRouterExpectations;

    public RouterTemplateRegressionTests()
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var preloaded = Directory
            .EnumerateFiles(templatesDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path =>
                {
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
                ("picker_wave_queue", 4, 0, "wave_dispatch_capacity")
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

    [Fact]
    public async Task TransportationClassesTemplate_UsesRouterOutputsForDispatchQueues()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "transportation-basic-classes",
            new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(yaml);
        Assert.NotNull(model.Nodes);

        var removedNodes = new[] { "hub_dispatch_airport", "hub_dispatch_industrial", "hub_dispatch_downtown" };
        foreach (var id in removedNodes)
        {
            Assert.DoesNotContain(model.Nodes!, n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        var demandNodes = new[]
        {
            "airport_dispatch_queue_demand",
            "downtown_dispatch_queue_demand",
            "industrial_dispatch_queue_demand"
        };

        foreach (var nodeId in demandNodes)
        {
            var node = model.Nodes!.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(node);
            Assert.Equal("0", node!.Expr);
        }
    }

    [Fact]
    public async Task TransportationClassesTemplate_DoesNotEmitQueueDepthMismatchWarnings()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "transportation-basic-classes",
            new Dictionary<string, object>());

        var analysis = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "queue_depth_mismatch", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(warning.NodeId, "AirportDispatchQueue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "queue_depth_mismatch", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(warning.NodeId, "DowntownDispatchQueue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "queue_depth_mismatch", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(warning.NodeId, "IndustrialDispatchQueue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SupplyChainClassesTemplate_UsesRouterOutputsForReturnsQueues()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "supply-chain-multi-tier-classes",
            new Dictionary<string, object>());

        var model = ModelService.ParseAndConvert(yaml);
        Assert.NotNull(model.Nodes);

        var inflowNodes = new[]
        {
            "restock_inflow",
            "recover_inflow",
            "scrap_inflow"
        };

        foreach (var nodeId in inflowNodes)
        {
            var node = model.Nodes!.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(node);
            Assert.Equal("0", node!.Expr);
        }
    }

    [Theory]
    [MemberData(nameof(AllRouterExpectations))]
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

    public static IEnumerable<object[]> AllRouterExpectations()
    {
        if (cachedRouterExpectations is not null)
        {
            foreach (var entry in cachedRouterExpectations)
            {
                yield return entry;
            }
            yield break;
        }

        var results = new List<object[]>();
        var templatesDirectory = ResolveTemplatesDirectory();
        foreach (var path in Directory.EnumerateFiles(templatesDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
        {
            var templateId = Path.GetFileNameWithoutExtension(path);
            var yaml = File.ReadAllText(path);
            foreach (var expectation in ParseRouterExpectations(templateId, yaml))
            {
                results.Add(new object[]
                {
                    expectation.TemplateId,
                    expectation.RouterId,
                    expectation.QueueId,
                    expectation.Targets
                });
            }
        }

        cachedRouterExpectations = results;
        foreach (var entry in results)
        {
            yield return entry;
        }
    }

    private sealed record RouterExpectation(string TemplateId, string RouterId, string QueueId, string[] Targets);

    private static IEnumerable<RouterExpectation> ParseRouterExpectations(string templateId, string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            yield break;
        }

        if (GetChild(root, "nodes") is not YamlSequenceNode nodesSequence)
        {
            yield break;
        }

        foreach (var node in nodesSequence.Children.OfType<YamlMappingNode>())
        {
            var kindValue = GetScalar(node, "kind");
            if (!string.Equals(kindValue, "router", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nodeId = GetScalar(node, "id");
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                continue;
            }

            var queueId = string.Empty;
            if (GetChild(node, "inputs") is YamlMappingNode inputsMap)
            {
                queueId = GetScalar(inputsMap, "queue") ?? string.Empty;
            }

            var targets = new List<string>();
            if (GetChild(node, "routes") is YamlSequenceNode routesSequence)
            {
                foreach (var routeNode in routesSequence.Children.OfType<YamlMappingNode>())
                {
                    var targetValue = GetScalar(routeNode, "target");
                    if (!string.IsNullOrWhiteSpace(targetValue))
                    {
                        targets.Add(targetValue);
                    }
                }
            }

            if (targets.Count == 0)
            {
                targets.Add(string.Empty);
            }

            yield return new RouterExpectation(templateId, nodeId, queueId, targets.ToArray());
        }
    }

    private static YamlNode? GetChild(YamlMappingNode map, string key)
    {
        foreach (var (childKey, value) in map.Children)
        {
            if (childKey is YamlScalarNode scalarKey &&
                string.Equals(scalarKey.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetScalar(YamlMappingNode map, string key)
    {
        return GetChild(map, key) is YamlScalarNode scalar ? scalar.Value : null;
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
