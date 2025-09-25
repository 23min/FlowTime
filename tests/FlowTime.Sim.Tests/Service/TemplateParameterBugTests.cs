using FlowTime.Sim.Service;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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
        var repository = new FileSystemTemplateRepository(templatesDirectory, NullLogger<FileSystemTemplateRepository>.Instance);
        var parameters = new Dictionary<string, object>
        {
            {"bins", 4},
            {"binMinutes", 60}
        };

        // Test all templates
        var templateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };

        foreach (var templateId in templateIds)
        {
            // Act
            var generatedModel = await repository.GenerateModelAsync(templateId, parameters);

            // Assert
            Assert.NotNull(generatedModel);
            Assert.DoesNotContain("  parameters:", generatedModel);
            
            // Additional check - verify the model is valid YAML and doesn't contain parameters in metadata
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
        var repository = new FileSystemTemplateRepository(templatesDirectory, NullLogger<FileSystemTemplateRepository>.Instance);

        // Act
        var templates = await repository.GetAllTemplatesAsync();

        // Assert
        Assert.NotEmpty(templates);
        
        var expectedTemplateIds = new[] { "it-system-microservices", "supply-chain-multi-tier", "manufacturing-line", "transportation-basic" };
        
        foreach (var expectedId in expectedTemplateIds)
        {
            var template = templates.FirstOrDefault(t => t.Id == expectedId);
            Assert.NotNull(template);
            
            // Verify the raw template YAML contains parameters section
            Assert.Contains("parameters:", template.Yaml);
            
            // Additional verification - check that parameters section is in the metadata
            var lines = template.Yaml.Split('\n');
            var inMetadataSection = false;
            var foundParametersInMetadata = false;

            foreach (var line in lines)
            {
                if (line.Trim() == "metadata:")
                {
                    inMetadataSection = true;
                    continue;
                }

                if (inMetadataSection && !string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                {
                    inMetadataSection = false;
                }

                if (inMetadataSection && line.Trim().StartsWith("parameters:"))
                {
                    foundParametersInMetadata = true;
                    break;
                }
            }

            Assert.True(foundParametersInMetadata, 
                $"Template {expectedId} should have 'parameters:' section in metadata");
        }
    }
}