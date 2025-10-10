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
        var currentDir = Directory.GetCurrentDirectory();
        
        // Try multiple path combinations to find templates directory
        var possiblePaths = new[]
        {
            // From /workspaces/flowtime-sim-vnext/tests/FlowTime.Sim.Tests/bin/Debug/net9.0 to /workspaces/flowtime-sim-vnext/templates
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "templates"))
        };
        
        string templatesDirectory = "";
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                templatesDirectory = path;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(templatesDirectory))
        {
            throw new DirectoryNotFoundException($"Templates directory not found. Current directory: {currentDir}. Tried paths: {string.Join(", ", possiblePaths)}");
        }
        // Preload templates explicitly to avoid directory scanning variability
        var templateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        var preloaded = new Dictionary<string, string>();
        foreach (var id in templateIds)
        {
            var path = Path.Combine(templatesDirectory, id + ".yaml");
            Assert.True(File.Exists(path), $"Expected template file missing: {path}");
            preloaded[id] = await File.ReadAllTextAsync(path);
        }

        var service = new NodeBasedTemplateService(preloaded, NullLogger<NodeBasedTemplateService>.Instance);
        var parameters = new Dictionary<string, object>
        {
            {"bins", 3},
            {"binSize", 60},
            {"demandPattern", new[] {10, 20, 30}},
            {"capacityPattern", new[] {15, 25, 35}}
        };

        foreach (var templateId in templateIds)
        {
            // Act
            var generatedModel = await service.GenerateEngineModelAsync(templateId, parameters);

            // Assert
            Assert.NotNull(generatedModel);
            Assert.DoesNotContain("  parameters:", generatedModel);
            
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
        var currentDir = Directory.GetCurrentDirectory();
        
        // Try multiple path combinations to find templates directory
        var possiblePaths = new[]
        {
            // From /workspaces/flowtime-sim-vnext/tests/FlowTime.Sim.Tests/bin/Debug/net9.0 to /workspaces/flowtime-sim-vnext/templates
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "templates")),
            Path.GetFullPath(Path.Combine(currentDir, "templates"))
        };
        
        string templatesDirectory = "";
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                templatesDirectory = path;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(templatesDirectory))
        {
            throw new DirectoryNotFoundException($"Templates directory not found. Current directory: {currentDir}. Tried paths: {string.Join(", ", possiblePaths)}");
        }
        // Preload specific templates explicitly
        var templateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        var preloaded = new Dictionary<string, string>();
        foreach (var id in templateIds)
        {
            var path = Path.Combine(templatesDirectory, id + ".yaml");
            Assert.True(File.Exists(path), $"Expected template file missing: {path}");
            preloaded[id] = await File.ReadAllTextAsync(path);
        }

        var service = new NodeBasedTemplateService(preloaded, NullLogger<NodeBasedTemplateService>.Instance);

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
}