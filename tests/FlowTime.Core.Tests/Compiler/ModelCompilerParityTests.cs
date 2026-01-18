using System.Globalization;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Compiler;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Compiler;

public sealed class ModelCompilerParityTests : IDisposable
{
    private readonly string rootDirectory;

    public ModelCompilerParityTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_compiler_parity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task CompiledModel_EvaluatesSameAsArtifactWriter()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 5d, 5d, 5d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 3d, 3d, 3d } },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 1d, 0d, 1d } },
                new NodeDefinition { Id = "failures", Kind = "const", Values = new[] { 2d, 0d, 0d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "QueueNode",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors",
                            QueueDepth = "self"
                        },
                        InitialCondition = new InitialConditionDefinition { QueueDepth = 2d }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "RetryNode",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Failures = "failures",
                            RetryEcho = "retry_echo",
                            RetryKernel = new[] { 0d, 1d }
                        }
                    }
                }
            }
        };

        var grid = new TimeGrid(3, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 5d, 5d, 5d },
            [new NodeId("served")] = new[] { 3d, 3d, 3d },
            [new NodeId("errors")] = new[] { 1d, 0d, 1d },
            [new NodeId("failures")] = new[] { 2d, 0d, 0d }
        };

        var compiled = ModelCompiler.Compile(model);
        var (compiledGrid, graph) = ModelParser.ParseModel(compiled);
        var evaluated = graph.EvaluateWithOverrides(compiledGrid, context);

        var queueId = compiled.Topology!.Nodes.Single(node => node.Id == "QueueNode").Semantics.QueueDepth;
        var retryId = compiled.Topology!.Nodes.Single(node => node.Id == "RetryNode").Semantics.RetryEcho;
        Assert.NotNull(queueId);
        Assert.NotNull(retryId);

        var expectedQueue = evaluated[new NodeId(queueId!)];
        var expectedRetry = evaluated[new NodeId(retryId!)];

        var specText = $"""
schemaVersion: 1
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [5, 5, 5]
  - id: served
    kind: const
    values: [3, 3, 3]
  - id: errors
    kind: const
    values: [1, 0, 1]
  - id: failures
    kind: const
    values: [2, 0, 0]
topology:
  nodes:
    - id: QueueNode
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
        queueDepth: {queueId}
      initialCondition:
        queueDepth: 2
    - id: RetryNode
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        failures: failures
        retryEcho: {retryId}
        retryKernel: [0, 1]
outputs:
  - series: "*"
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "compiler-parity"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var queuePath = Path.Combine(seriesDir, BuildSeriesFileName(queueId));
        var retryPath = Path.Combine(seriesDir, BuildSeriesFileName(retryId));

        Assert.Equal(expectedQueue.ToArray(), ReadSeriesValues(queuePath));
        Assert.Equal(expectedRetry.ToArray(), ReadSeriesValues(retryPath));
    }

    private static string BuildSeriesFileName(string nodeId)
    {
        var componentId = nodeId.ToUpperInvariant();
        return $"{nodeId}@{componentId}@DEFAULT.csv";
    }

    private static double[] ReadSeriesValues(string path)
    {
        var lines = File.ReadAllLines(path);
        var values = new List<double>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 2)
            {
                continue;
            }

            values.Add(double.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        return values.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
