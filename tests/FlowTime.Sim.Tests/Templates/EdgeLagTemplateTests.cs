using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public sealed class EdgeLagTemplateTests
{
    [Fact]
    public async Task Templates_DoNotUseEdgeLag()
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templateService = new TemplateService(templatesDirectory, NullLogger<TemplateService>.Instance);
        var templates = await templateService.GetAllTemplatesAsync();
        var failures = new List<string>();

        foreach (var template in templates)
        {
            var yaml = await templateService.GenerateEngineModelAsync(template.Metadata.Id, new Dictionary<string, object>());
            // m-E24-02: deserialize into the unified ModelDto (SimModelArtifact deleted).
            var model = ModelService.ParseYaml(yaml);
            var laggedEdges = model.Topology?.Edges?
                .Where(edge => edge.Lag.HasValue && edge.Lag.Value > 0)
                .Select(edge => $"{edge.Id}({edge.From}->{edge.To}, lag {edge.Lag})")
                .ToList();

            if (laggedEdges != null && laggedEdges.Count > 0)
            {
                failures.Add($"{template.Metadata.Id}: {string.Join(", ", laggedEdges)}");
            }
        }

        Assert.True(failures.Count == 0, $"Templates still use edge lag: {string.Join("; ", failures)}");
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
