using System.Globalization;
using System.Text.Json;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Artifacts;

public sealed class RunArtifactWriterEdgeSeriesTests : IDisposable
{
    private readonly string rootDirectory;

    public RunArtifactWriterEdgeSeriesTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_edges_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task WriteArtifactsAsync_WritesEdgeSeriesIndexEntry()
    {
        var (model, grid, context, specText) = BuildFixture();
        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "source_to_sink",
                Metric = "flowTotal",
                Values = new[] { 5d, 6d }
            }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            EdgeSeries = edgeSeries,
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "edge-series-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));

        var expectedId = "edge_source_to_sink_flowTotal@EDGE_SOURCE_TO_SINK_FLOWTOTAL@DEFAULT";
        var series = doc.RootElement.GetProperty("series");
        Assert.Contains(series.EnumerateArray(), entry =>
            string.Equals(entry.GetProperty("id").GetString(), expectedId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.GetProperty("kind").GetString(), "edge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteArtifactsAsync_WritesEdgeSeriesCsv()
    {
        var (model, grid, context, specText) = BuildFixture();
        var edgeSeries = new[]
        {
            new RunArtifactWriter.EdgeSeriesInput
            {
                EdgeId = "source_to_sink",
                Metric = "flowTotal",
                Values = new[] { 5d, 6d }
            }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            EdgeSeries = edgeSeries,
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "edge-series-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var path = Path.Combine(seriesDir, "edge_source_to_sink_flowTotal@EDGE_SOURCE_TO_SINK_FLOWTOTAL@DEFAULT.csv");

        Assert.True(File.Exists(path), "Expected edge series CSV to be emitted.");
        Assert.Equal(new[] { 5d, 6d }, ReadSeriesValues(path));
    }

    private static (ModelDefinition Model, TimeGrid Grid, Dictionary<NodeId, double[]> Context, string SpecText) BuildFixture()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 1d, 2d } },
                new NodeDefinition { Id = "served", Kind = "const", Values = new[] { 1d, 2d } }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served"
                        }
                    }
                }
            }
        };

        var grid = new TimeGrid(2, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 1d, 2d },
            [new NodeId("served")] = new[] { 1d, 2d }
        };

        var specText = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: Source
      kind: service
      semantics:
        arrivals: arrivals
        served: served
nodes:
  - id: arrivals
    kind: const
    values: [1, 2]
  - id: served
    kind: const
    values: [1, 2]
""";

        return (model, grid, context, specText);
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
