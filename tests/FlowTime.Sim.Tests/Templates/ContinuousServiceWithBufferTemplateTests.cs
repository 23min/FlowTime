using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.Services;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public sealed class ContinuousServiceWithBufferTemplateTests
{
    private readonly TemplateService templateService;

    public ContinuousServiceWithBufferTemplateTests()
    {
        var templatesDirectory = ResolveTemplatesDirectory();
        var templates = Directory
            .EnumerateFiles(templatesDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => File.ReadAllText(path));

        templateService = new TemplateService(templates, NullLogger<TemplateService>.Instance);
    }

    [Fact]
    public async Task ItDocumentProcessingTemplate_UsesClassesAndContinuousServiceWithBufferNodes()
    {
        var yaml = await templateService.GenerateEngineModelAsync(
            "it-document-processing-continuous",
            new Dictionary<string, object>());

        var model = ModelService.ParseAndConvert(yaml);
        Assert.NotNull(model.Topology);

        var classIds = model.Classes.Select(c => c.Id).ToArray();
        Assert.Contains("Invoice", classIds);
        Assert.Contains("Contract", classIds);
        Assert.Contains("Claim", classIds);

        var arrivalClasses = model.Traffic?.Arrivals.Select(a => a.ClassId).ToArray() ?? Array.Empty<string>();
        Assert.All(arrivalClasses, classId => Assert.False(string.IsNullOrWhiteSpace(classId)));

        var pipelineNodes = new[]
        {
            "IntakeQueue",
            "IngressDbQueue",
            "InvoiceProcessing",
            "ContractProcessing",
            "ClaimProcessing",
            "EgressDbQueue",
            "DeliveryQueue"
        };

        foreach (var nodeId in pipelineNodes)
        {
            var node = model.Topology!.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(node);
            Assert.Equal("serviceWithBuffer", node!.Kind);
        }

        var router = model.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, "DocumentTypeRouter", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(router);
        Assert.NotNull(router!.Router);
        Assert.Contains(router.Router!.Routes, route =>
            string.Equals(route.Target, "invoice_queue_demand", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(router.Router!.Routes, route =>
            string.Equals(route.Target, "contract_queue_demand", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(router.Router!.Routes, route =>
            string.Equals(route.Target, "claim_queue_demand", StringComparison.OrdinalIgnoreCase));

        var scheduledBuffers = model.Nodes
            .Where(n => string.Equals(n.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase))
            .Where(n => n.DispatchSchedule is not null)
            .ToList();
        Assert.Empty(scheduledBuffers);
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
