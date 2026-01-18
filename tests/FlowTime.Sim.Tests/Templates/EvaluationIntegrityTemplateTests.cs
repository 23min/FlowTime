using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Sim.Core.Analysis;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public sealed class EvaluationIntegrityTemplateTests
{
    private readonly TemplateService templateService;

    public EvaluationIntegrityTemplateTests()
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

    [Fact]
    public async Task TransportationTemplate_ClassPropagation_LeavesNoDerivedGaps()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "transportation-basic-classes",
            new Dictionary<string, object>());

        var analysis = TemplateInvariantAnalyzer.Analyze(yaml);

        var classCoverageWarnings = analysis.Warnings
            .Where(warning =>
                warning.Code.StartsWith("class_series_missing_", StringComparison.OrdinalIgnoreCase) ||
                warning.Code.StartsWith("class_series_partial_", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(warning.Code, "class_coverage_failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(warning.Code, "topology_class_coverage_failed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allowedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HubDispatchRouter",
            "Airport",
            "Downtown",
            "Industrial"
        };

        Assert.All(classCoverageWarnings, warning =>
        {
            if (warning.NodeId is { } nodeId &&
                nodeId.Contains("dlq", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Assert.True(
                warning.NodeId is { } value && allowedNodes.Contains(value),
                $"Unexpected class coverage warning on node '{warning.NodeId}'.");
        });
    }

    [Fact]
    public async Task TransportationTemplate_RouterOverrides_DoNotLeakClasses()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "transportation-basic-classes",
            new Dictionary<string, object>());

        var analysis = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.DoesNotContain(
            analysis.Warnings,
            warning => string.Equals(warning.Code, "router_class_leakage", StringComparison.OrdinalIgnoreCase));
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
