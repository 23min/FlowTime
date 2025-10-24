using System.IO;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Service;

public class TemplateParameterBugTests
{
    [Fact]
    public async Task GenerateModelAsync_ShouldStripParametersFromMetadata()
    {
        // Arrange
        var templatesDirectory = ResolveTemplatesDirectory();
        // Preload templates explicitly to avoid directory scanning variability
        var templateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        var preloaded = new Dictionary<string, string>();
        foreach (var id in templateIds)
        {
            var path = Path.Combine(templatesDirectory, id + ".yaml");
            Assert.True(File.Exists(path), $"Expected template file missing: {path}");
            preloaded[id] = await File.ReadAllTextAsync(path);
        }

        var service = new TemplateService(preloaded, NullLogger<TemplateService>.Instance);
        var parameters = new Dictionary<string, object>
        {
            {"bins", 6},
            {"binSize", 60},
            {"requestPattern", new[] {100, 120, 140, 130, 110, 100}},
            {"loadBalancerCapacity", new[] {150, 150, 150, 150, 150, 150}},
            {"authCapacity", new[] {140, 140, 140, 140, 140, 140}},
            {"databaseCapacity", new[] {130, 130, 130, 130, 130, 130}},
            {"demandPattern", new[] {10, 20, 30, 40, 30, 20}},
            {"capacityPattern", new[] {15, 25, 35, 45, 35, 25}},
            {"supplierCapacity", new[] {50, 55, 60, 65, 70, 75}},
            {"plantCapacity", new[] {45, 50, 55, 60, 55, 50}}
        };

        foreach (var templateId in templateIds)
        {
            // Act
            var generatedModel = await service.GenerateEngineModelAsync(templateId, parameters);

            // Assert
            Assert.NotNull(generatedModel);

            // Additional check - ensure parameters section is not present in the output (engine schema)
            var lines = generatedModel.Split('\n');
            var inMetadataSection = false;
            var foundParametersInMetadata = false;

            foreach (var line in lines)
            {
                if (line.Trim() == "metadata:")
                {
                    inMetadataSection = true;
                    continue;
                }

                // Exit metadata section when we hit a non-indented line (excluding empty lines)
                if (inMetadataSection && !string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                {
                    inMetadataSection = false;
                }

                // Check for parameters within metadata section
                if (inMetadataSection && line.Trim().StartsWith("parameters:"))
                {
                    foundParametersInMetadata = true;
                    break;
                }
            }

            Assert.False(foundParametersInMetadata, 
                $"Template {templateId} generated model still contains 'parameters:' in metadata section");
        }
    }

    [Fact]
    public async Task GetAllTemplatesAsync_ShouldReturnTemplatesWithParameters()
    {
        // Arrange
        var templatesDirectory = ResolveTemplatesDirectory();
        // Preload specific templates explicitly
        var templateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        var preloaded = new Dictionary<string, string>();
        foreach (var id in templateIds)
        {
            var path = Path.Combine(templatesDirectory, id + ".yaml");
            Assert.True(File.Exists(path), $"Expected template file missing: {path}");
            preloaded[id] = await File.ReadAllTextAsync(path);
        }

        var service = new TemplateService(preloaded, NullLogger<TemplateService>.Instance);

        // Act
        var templates = await service.GetAllTemplatesAsync();

        // Assert
        Assert.NotEmpty(templates);
        
        var expectedTemplateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        
        foreach (var expectedId in expectedTemplateIds)
        {
            var template = templates.FirstOrDefault(t => t.Metadata.Id == expectedId);
            Assert.NotNull(template);

            // Verify new service exposes parameters via the model
            Assert.NotNull(template!.Parameters);
            Assert.NotEmpty(template.Parameters);
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
