using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Contracts.Services;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public class CanonicalModelWriterTests
{
    private readonly TemplateService templateService;

    private const string TemplateId = "classy-template";
    private const string SinkTemplateId = "sink-role-template";
    private const string ClassyTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: classy-template
  title: Classy Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 1
  binUnit: hours
classes:
  - id: Order
    displayName: Order Flow
  - id: Refund
    displayName: Refund Flow
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "MIN(ingest, 8)"
  - id: errors
    kind: const
    values: [0, 0, 0, 0]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 20
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 5
topology:
  nodes:
    - id: Ingest
      kind: service
      semantics:
        arrivals: ingest
        served: served
        errors: errors
outputs:
  - series: served
    as: served.csv
""";
    private const string SinkTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sink-role-template
  title: Sink Role Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10]
  - id: served
    kind: const
    values: [10, 10]
topology:
  nodes:
    - id: TerminalSuccess
      kind: service
      nodeRole: sink
      semantics:
        arrivals: arrivals
        served: served
        errors: null
outputs:
  - series: served
    as: served.csv
""";

    public CanonicalModelWriterTests()
    {
        var templates = new Dictionary<string, string>
        {
            { TemplateId, ClassyTemplate },
            { SinkTemplateId, SinkTemplate }
        };

        templateService = new TemplateService(templates, NullLogger<TemplateService>.Instance);
    }

    [Fact]
    public async Task CanonicalModelWriter_Writes_Classes_Block()
    {
        var modelYaml = await templateService.GenerateEngineModelAsync(TemplateId, new Dictionary<string, object>());
        var yaml = LoadYaml(modelYaml);

        var classes = GetSequence(yaml, "classes");
        Assert.NotNull(classes);
        Assert.Equal(2, classes!.Children.Count);

        var ids = classes.Children
            .OfType<YamlMappingNode>()
            .Select(m => m.Children[new YamlScalarNode("id")].ToString())
            .ToArray();

        Assert.Contains("Order", ids);
        Assert.Contains("Refund", ids);

        var arrivals = GetSequence(yaml, "traffic", "arrivals");
        Assert.NotNull(arrivals);
        Assert.All(arrivals!.Children.OfType<YamlMappingNode>(), arrival =>
        {
            Assert.True(arrival.Children.ContainsKey(new YamlScalarNode("classId")), "Arrival must include classId when classes are declared.");
        });
    }

    [Fact]
    public async Task CanonicalModelWriter_Preserves_ClassOrder()
    {
        var modelYaml = await templateService.GenerateEngineModelAsync(TemplateId, new Dictionary<string, object>());
        var yaml = LoadYaml(modelYaml);

        var classes = GetSequence(yaml, "classes");
        Assert.NotNull(classes);

        var ids = classes!.Children
            .OfType<YamlMappingNode>()
            .Select(m => m.Children[new YamlScalarNode("id")].ToString())
            .ToArray();

        Assert.Equal(new[] { "Order", "Refund" }, ids);
    }

    [Fact]
    public async Task CanonicalModelWriter_Preserves_NodeRole()
    {
        var modelYaml = await templateService.GenerateEngineModelAsync(SinkTemplateId, new Dictionary<string, object>());

        Assert.Contains("nodeRole: sink", modelYaml);
    }

    [Fact]
    public async Task RunManifest_Includes_ClassSummary()
    {
        var modelYaml = await templateService.GenerateEngineModelAsync(TemplateId, new Dictionary<string, object>());
        var modelDefinition = ModelService.ParseAndConvert(modelYaml);

        Assert.NotNull(modelDefinition);
        var classesProperty = modelDefinition.GetType().GetProperty("Classes");
        Assert.NotNull(classesProperty);

        var classesValue = classesProperty!.GetValue(modelDefinition);
        Assert.NotNull(classesValue);

        var classIds = new List<string>();
        foreach (var entry in (IEnumerable<object>)classesValue)
        {
            var idProp = entry.GetType().GetProperty("Id");
            if (idProp != null)
            {
                classIds.Add(idProp.GetValue(entry)?.ToString() ?? string.Empty);
            }
        }

        Assert.Equal(new[] { "Order", "Refund" }, classIds);

        var grid = modelDefinition.Grid!;
        var timeUnit = TimeUnitExtensions.Parse(grid.BinUnit);
        var timeGrid = new TimeGrid(grid.Bins, grid.BinSize, timeUnit);
        var tempDir = Path.Combine(Path.GetTempPath(), $"class-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("ingest")] = Enumerable.Repeat(10d, grid.Bins).ToArray(),
            [new NodeId("served")] = Enumerable.Repeat(8d, grid.Bins).ToArray(),
            [new NodeId("errors")] = Enumerable.Repeat(0d, grid.Bins).ToArray()
        };

        var writeRequest = new RunArtifactWriter.WriteRequest
        {
            Model = modelDefinition,
            Grid = timeGrid,
            Context = context,
            SpecText = modelYaml,
            DeterministicRunId = true,
            OutputDirectory = tempDir
        };

        var writeResult = await RunArtifactWriter.WriteArtifactsAsync(writeRequest);

        var runJson = await File.ReadAllTextAsync(Path.Combine(writeResult.RunDirectory, "run.json"));
        Assert.Contains("\"classes\"", runJson);
        Assert.Contains("Order", runJson);
        Assert.Contains("Order Flow", runJson);

        var manifestJson = await File.ReadAllTextAsync(Path.Combine(writeResult.RunDirectory, "manifest.json"));
        Assert.Contains("\"classes\"", manifestJson);
        Assert.Contains("Refund", manifestJson);
        Assert.Contains("Order Flow", manifestJson);

        Directory.Delete(writeResult.RunDirectory, true);
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static YamlMappingNode LoadYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static YamlSequenceNode? GetSequence(YamlMappingNode root, params string[] path)
    {
        YamlNode current = root;
        foreach (var segment in path)
        {
            if (current is not YamlMappingNode mapping || !mapping.Children.TryGetValue(new YamlScalarNode(segment), out var next))
            {
                return null;
            }

            current = next;
        }

        return current as YamlSequenceNode;
    }
}
