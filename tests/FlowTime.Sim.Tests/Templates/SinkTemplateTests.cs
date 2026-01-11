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

public sealed class SinkTemplateTests
{
    [Fact]
    public async Task TransportationTemplates_UseSinkKind_ForTerminalLines()
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templateIds = new[] { "transportation-basic", "transportation-basic-classes" };
        var sinkNodes = new[] { "Airport", "Downtown", "Industrial" };
        var lineNodes = new[] { "LineAirport", "LineDowntown", "LineIndustrial" };
        var templateService = new TemplateService(templatesDirectory, NullLogger<TemplateService>.Instance);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var templateId in templateIds)
        {
            var yaml = await templateService.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
            var template = deserializer.Deserialize<SimModelArtifact>(yaml);

            foreach (var nodeId in sinkNodes)
            {
                var node = Assert.Single(template.Topology.Nodes, n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                Assert.True(
                    string.Equals(node.Kind, "sink", StringComparison.OrdinalIgnoreCase),
                    $"Expected {templateId} node '{nodeId}' to be kind sink, but was '{node.Kind}'.");
            }

            foreach (var nodeId in lineNodes)
            {
                var node = Assert.Single(template.Topology.Nodes, n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                Assert.False(
                    string.Equals(node.Kind, "sink", StringComparison.OrdinalIgnoreCase),
                    $"Expected {templateId} node '{nodeId}' to be non-sink, but was '{node.Kind}'.");
            }
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

        throw new DirectoryNotFoundException("Unable to resolve templates directory relative to solution root.");
    }
}
