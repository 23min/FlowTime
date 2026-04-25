using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.Contracts.Dtos;
using FlowTime.Contracts.Services;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public sealed class TransitNodeTemplateTests
{
    [Fact]
    public async Task TransportationClassesTemplate_UsesTransitNodesForLineDelays()
    {
        var model = await LoadTemplateAsync("transportation-basic-classes");
        Assert.NotNull(model.Topology);
        var nodeIds = model.Topology!.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("TransitAirport", nodeIds);
        Assert.Contains("TransitDowntown", nodeIds);
        Assert.Contains("TransitIndustrial", nodeIds);

        AssertNodeKind(model, "TransitAirport", "service");
        AssertNodeKind(model, "TransitDowntown", "service");
        AssertNodeKind(model, "TransitIndustrial", "service");

        AssertEdge(model, "line_to_airport", "LineAirport:out", "TransitAirport:in");
        AssertEdge(model, "transit_to_airport", "TransitAirport:out", "Airport:in");
        AssertEdge(model, "line_to_downtown", "LineDowntown:out", "TransitDowntown:in");
        AssertEdge(model, "transit_to_downtown", "TransitDowntown:out", "Downtown:in");
        AssertEdge(model, "line_to_industrial", "LineIndustrial:out", "TransitIndustrial:in");
        AssertEdge(model, "transit_to_industrial", "TransitIndustrial:out", "Industrial:in");
    }

    [Fact]
    public async Task IncidentRetryTemplate_UsesAnalyticsDelayTransitNode()
    {
        var model = await LoadTemplateAsync("supply-chain-incident-retry");
        Assert.NotNull(model.Topology);
        var nodeIds = model.Topology!.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("AnalyticsDelay", nodeIds);
        AssertNodeKind(model, "AnalyticsDelay", "service");

        AssertEdge(model, "effort_analytics", "IncidentIntake:out", "AnalyticsDelay:in");
        AssertEdge(model, "analytics_delay_to_support", "AnalyticsDelay:out", "SupportAnalytics:in");
    }

    // m-E24-02: tests now consume the unified ModelDto (SimModelArtifact deleted).
    private static async Task<ModelDto> LoadTemplateAsync(string templateId)
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templateService = new TemplateService(templatesDirectory, NullLogger<TemplateService>.Instance);
        var yaml = await templateService.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
        return ModelService.ParseYaml(yaml);
    }

    private static void AssertNodeKind(ModelDto model, string nodeId, string expectedKind)
    {
        var node = Assert.Single(model.Topology!.Nodes, candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        Assert.True(
            string.Equals(node.Kind, expectedKind, StringComparison.OrdinalIgnoreCase),
            $"Expected node '{nodeId}' to be kind '{expectedKind}', but was '{node.Kind}'.");
    }

    private static void AssertEdge(ModelDto model, string edgeId, string from, string to)
    {
        var edge = Assert.Single(model.Topology!.Edges, candidate => string.Equals(candidate.Id, edgeId, StringComparison.Ordinal));
        Assert.Equal(from, edge.From);
        Assert.Equal(to, edge.To);
        Assert.True(edge.Lag == null || edge.Lag == 0, $"Expected edge '{edgeId}' to have no lag.");
    }

    private static string ResolveTemplatesDirectory()
    {
        var directory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrWhiteSpace(directory))
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

        throw new DirectoryNotFoundException("Unable to resolve templates directory relative to solution root.");
    }
}
