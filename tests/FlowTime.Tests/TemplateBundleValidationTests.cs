using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Tests;

public sealed class TemplateBundleValidationTests
{
    [Fact]
    public async Task AllTemplatesGenerateValidBundlesWithDefaults()
    {
        var templatesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../templates"));
        Assert.True(Directory.Exists(templatesDirectory), $"Templates directory not found at {templatesDirectory}");

        var templateFiles = Directory.GetFiles(templatesDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(templateFiles);

        var yamlById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in templateFiles)
        {
            var templateId = Path.GetFileNameWithoutExtension(file);
            var yaml = await File.ReadAllTextAsync(file);
            yamlById[templateId] = yaml;
        }

        var service = new TemplateService(yamlById, NullLogger<TemplateService>.Instance);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"flowtime_template_validation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            foreach (var templateId in yamlById.Keys)
            {
                var engineModelYaml = await service.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
                engineModelYaml = engineModelYaml.Replace("\r\n", "\n");

                var modelDefinition = ModelService.ParseAndConvert(engineModelYaml);
                var (grid, graph) = ModelParser.ParseModel(modelDefinition);

                var context = graph.Evaluate(grid)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

                var request = new RunArtifactWriter.WriteRequest
                {
                    Model = modelDefinition,
                    Grid = grid,
                    Context = context,
                    SpecText = engineModelYaml,
                    OutputDirectory = tempRoot,
                    DeterministicRunId = true
                };

                await RunArtifactWriter.WriteArtifactsAsync(request);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
