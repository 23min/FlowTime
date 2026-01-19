using System.Text.Json;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Artifacts;

public sealed class RunArtifactWriterWarningTests : IDisposable
{
    private readonly string rootDirectory;

    public RunArtifactWriterWarningTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_warnings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task WriteArtifactsAsync_WritesInvariantWarningsToRunJson()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 2d } },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceNode",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors"
                        }
                    }
                }
            }
        };

        var grid = new TimeGrid(1, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 1d },
            [new NodeId("served")] = new[] { 2d },
            [new NodeId("errors")] = new[] { 0d }
        };

        var specText = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
nodes:
  - id: arrivals
    kind: const
    values: [1]
  - id: served
    kind: const
    values: [2]
  - id: errors
    kind: const
    values: [0]
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            EdgeSeries = new[]
            {
                new RunArtifactWriter.EdgeSeriesInput
                {
                    EdgeId = "ServiceNode->Downstream",
                    Metric = "flowTotal",
                    Values = new[] { 0d }
                }
            },
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "warnings-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var runJsonPath = Path.Combine(result.RunDirectory, "run.json");
        var runJson = await File.ReadAllTextAsync(runJsonPath);

        using var doc = JsonDocument.Parse(runJson);
        var warnings = doc.RootElement.GetProperty("warnings");
        Assert.True(warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0);
        Assert.Contains(warnings.EnumerateArray(), warning => warning.GetProperty("code").GetString() == "served_exceeds_arrivals");
    }

    [Fact]
    public async Task WriteArtifactsAsync_WritesEdgeConservationWarningsToRunJson()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 1d } },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceNode",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Downstream",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "downstream_arrivals",
                            Served = "downstream_served"
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "ServiceNode",
                        Target = "Downstream",
                        Measure = "served"
                    }
                }
            }
        };

        var grid = new TimeGrid(1, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 1d },
            [new NodeId("served")] = new[] { 1d },
            [new NodeId("errors")] = new[] { 0d },
            [new NodeId("downstream_arrivals")] = new[] { 2d },
            [new NodeId("downstream_served")] = new[] { 2d }
        };

        var specText = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
    - id: Downstream
      kind: service
      semantics:
        arrivals: downstream_arrivals
        served: downstream_served
  edges:
    - source: ServiceNode
      target: Downstream
      measure: served
nodes:
  - id: arrivals
    kind: const
    values: [1]
  - id: served
    kind: const
    values: [1]
  - id: errors
    kind: const
    values: [0]
  - id: downstream_arrivals
    kind: const
    values: [2]
  - id: downstream_served
    kind: const
    values: [2]
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            EdgeSeries = new[]
            {
                new RunArtifactWriter.EdgeSeriesInput
                {
                    EdgeId = "ServiceNode->Downstream",
                    Metric = "flowTotal",
                    Values = new[] { 0d }
                }
            },
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "warnings-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var runJsonPath = Path.Combine(result.RunDirectory, "run.json");
        var runJson = await File.ReadAllTextAsync(runJsonPath);

        using var doc = JsonDocument.Parse(runJson);
        var warnings = doc.RootElement.GetProperty("warnings");
        Assert.Contains(warnings.EnumerateArray(), warning => warning.GetProperty("code").GetString() == "edge_flow_mismatch_outgoing");
        Assert.Contains(warnings.EnumerateArray(), warning => warning.GetProperty("code").GetString() == "edge_flow_mismatch_incoming");
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
