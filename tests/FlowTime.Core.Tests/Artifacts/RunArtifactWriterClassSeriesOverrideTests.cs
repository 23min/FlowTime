using System.Globalization;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Artifacts;

public sealed class RunArtifactWriterClassSeriesOverrideTests : IDisposable
{
    private readonly string rootDirectory;

    public RunArtifactWriterClassSeriesOverrideTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_override_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task WriteArtifactsAsync_PropagatesClassSeriesOverridesToDerivedNodes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Classes =
            {
                new ClassDefinition { Id = "Alpha" },
                new ClassDefinition { Id = "Beta" }
            },
            Traffic = new TrafficDefinition
            {
                Arrivals =
                {
                    new ArrivalDefinition
                    {
                        NodeId = "other_source",
                        ClassId = "Beta",
                        Pattern = new ArrivalPatternDefinition { Kind = "constant", RatePerBin = 1 }
                    }
                }
            },
            Nodes =
            {
                new NodeDefinition { Id = "source_total", Kind = "const", Values = new[] { 10d, 10d } },
                new NodeDefinition { Id = "other_source", Kind = "const", Values = new[] { 2d, 2d } },
                new NodeDefinition { Id = "derived", Kind = "expr", Expr = "source_total + other_source" }
            }
        };

        var grid = new TimeGrid(2, 1, TimeUnit.Hours);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("source_total")] = new[] { 10d, 10d },
            [new NodeId("other_source")] = new[] { 2d, 2d },
            [new NodeId("derived")] = new[] { 12d, 12d }
        };
        var classSeriesOverride = new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>()
        {
            [new NodeId("source_total")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Alpha"] = new[] { 10d, 10d }
            }
        };

        var specText = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: other_source
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: source_total
    kind: const
    values: [10, 10]
  - id: other_source
    kind: const
    values: [2, 2]
  - id: derived
    kind: expr
    expr: source_total + other_source
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            ClassSeriesOverride = classSeriesOverride,
            SpecText = specText,
            RngSeed = 123,
            StartTimeBias = null,
            DeterministicRunId = true,
            OutputDirectory = rootDirectory,
            Verbose = false,
            ProvenanceJson = null,
            InputHash = null,
            TemplateId = "class-override-test"
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");

        var alphaPath = Path.Combine(seriesDir, "derived@DERIVED@Alpha.csv");
        var betaPath = Path.Combine(seriesDir, "derived@DERIVED@Beta.csv");

        Assert.True(File.Exists(alphaPath), "Expected derived Alpha class series to be materialized.");
        Assert.True(File.Exists(betaPath), "Expected derived Beta class series to be materialized.");

        Assert.Equal(new[] { 10d, 10d }, ReadSeriesValues(alphaPath));
        Assert.Equal(new[] { 2d, 2d }, ReadSeriesValues(betaPath));
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
