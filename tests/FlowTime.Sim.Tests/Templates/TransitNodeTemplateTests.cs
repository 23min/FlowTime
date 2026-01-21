using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Tests.Templates;

public sealed class TransitNodeTemplateTests
{
    [Fact]
    public async Task TransportationClassesTemplate_UsesTransitNodesForLineDelays()
    {
        var model = await LoadTemplateAsync("transportation-basic-classes");
        var nodeIds = model.Topology.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

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
        var nodeIds = model.Topology.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("AnalyticsDelay", nodeIds);
        AssertNodeKind(model, "AnalyticsDelay", "service");

        AssertEdge(model, "effort_analytics", "IncidentIntake:out", "AnalyticsDelay:in");
        AssertEdge(model, "analytics_delay_to_support", "AnalyticsDelay:out", "SupportAnalytics:in");
    }

    private static async Task<SimModelArtifact> LoadTemplateAsync(string templateId)
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templateService = new TemplateService(templatesDirectory, NullLogger<TemplateService>.Instance);
        var yaml = await templateService.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<SimModelArtifact>(yaml);
    }

    private static void AssertNodeKind(SimModelArtifact model, string nodeId, string expectedKind)
    {
        var node = Assert.Single(model.Topology.Nodes, candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        Assert.True(
            string.Equals(node.Kind, expectedKind, StringComparison.OrdinalIgnoreCase),
            $"Expected node '{nodeId}' to be kind '{expectedKind}', but was '{node.Kind}'.");
    }

    private static void AssertEdge(SimModelArtifact model, string edgeId, string from, string to)
    {
        var edge = Assert.Single(model.Topology.Edges, candidate => string.Equals(candidate.Id, edgeId, StringComparison.Ordinal));
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
