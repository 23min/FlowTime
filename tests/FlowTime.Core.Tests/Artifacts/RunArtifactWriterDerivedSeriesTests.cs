using System.Globalization;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Execution;

namespace FlowTime.Core.Tests.Artifacts;

public sealed class RunArtifactWriterDerivedSeriesTests : IDisposable
{
    private readonly string rootDirectory;

    public RunArtifactWriterDerivedSeriesTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_derived_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task WriteArtifactsAsync_ComputesQueueDepthWithLossAndInitialCondition()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 5d, 5d, 5d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 3d, 3d, 3d } },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 1d, 0d, 1d } }
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
                            QueueDepth = "queue_depth"
                        },
                        InitialCondition = new InitialConditionDefinition { QueueDepth = 2d }
                    }
                }
            }
        };

        var grid = new TimeGrid(3, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 5d, 5d, 5d },
            [new NodeId("served")] = new[] { 3d, 3d, 3d },
            [new NodeId("errors")] = new[] { 1d, 0d, 1d }
        };

        var specText = """
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
topology:
  nodes:
    - id: QueueNode
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
        queueDepth: queue_depth
      initialCondition:
        queueDepth: 2
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
            TemplateId = "queue-depth-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var queuePath = Path.Combine(seriesDir, "queue_depth@QUEUE_DEPTH@DEFAULT.csv");
        Assert.True(File.Exists(queuePath), "Expected queue depth series to be materialized.");
        Assert.Equal(new[] { 3d, 5d, 6d }, ReadSeriesValues(queuePath));
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
