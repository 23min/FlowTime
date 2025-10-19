using FlowTime.Adapters.Synthetic;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using Json.Schema;
using System.Text.Json.Nodes;
using Xunit;

namespace FlowTime.Adapters.Synthetic.Tests;

public class FileSeriesReaderTests : IDisposable
{
    private readonly string testRunDirectory;
    private readonly string artifactRoot;
    private readonly RunArtifactWriter.WriteResult writeResult;

    public FileSeriesReaderTests()
    {
        writeResult = CreateRunArtifacts(out testRunDirectory, out artifactRoot);
    }

    [Fact]
    public async Task ReadRunInfoAsync_ValidRunJson_ReturnsCorrectManifest()
    {
        var reader = new FileSeriesReader();
        var manifest = await reader.ReadRunInfoAsync(testRunDirectory);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(writeResult.RunId, manifest.RunId);
        Assert.Equal("0.1.0", manifest.EngineVersion);
        Assert.Equal("engine", manifest.Source);
        Assert.Equal(4, manifest.Grid.Bins);
        Assert.Equal(60, manifest.Grid.BinMinutes);
        Assert.Equal("UTC", manifest.Grid.Timezone);
        Assert.Equal("left", manifest.Grid.Align);
        Assert.Equal(2, manifest.Series.Length);
    }

    [Fact]
    public async Task ReadIndexAsync_ValidIndexJson_ReturnsCorrectIndex()
    {
        var reader = new FileSeriesReader();
        var index = await reader.ReadIndexAsync(testRunDirectory);

        Assert.Equal(1, index.SchemaVersion);
        Assert.Equal(4, index.Grid.Bins);
        Assert.Equal(60, index.Grid.BinMinutes);
        Assert.Equal(2, index.Series.Length);

        var demandSeries = index.Series.First(s => s.Id.StartsWith("demand", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("flow", demandSeries.Kind);
        Assert.Equal("entities/bin", demandSeries.Unit);
        Assert.Equal(4, demandSeries.Points);
    }

    [Fact]
    public async Task ReadSeriesAsync_ValidCsv_ReturnsCorrectSeries()
    {
        var reader = new FileSeriesReader();
        var demandSeriesId = Directory.GetFiles(Path.Combine(testRunDirectory, "series"))
            .Select(Path.GetFileNameWithoutExtension)
            .First(id => id!.StartsWith("demand", StringComparison.OrdinalIgnoreCase));

        var series = await reader.ReadSeriesAsync(testRunDirectory, demandSeriesId!);

        Assert.Equal(4, series.Length);
        Assert.Equal(10.0, series[0]);
        Assert.Equal(20.0, series[1]);
        Assert.Equal(30.0, series[2]);
        Assert.Equal(40.0, series[3]);
    }

    [Fact]
    public void SeriesExists_ExistingFile_ReturnsTrue()
    {
        var reader = new FileSeriesReader();
        var demandSeriesId = Directory.GetFiles(Path.Combine(testRunDirectory, "series"))
            .Select(Path.GetFileNameWithoutExtension)
            .First(id => id!.StartsWith("demand", StringComparison.OrdinalIgnoreCase));

        Assert.True(reader.SeriesExists(testRunDirectory, demandSeriesId!));
    }

    [Fact]
    public void SeriesExists_NonExistingFile_ReturnsFalse()
    {
        var reader = new FileSeriesReader();
        Assert.False(reader.SeriesExists(testRunDirectory, "nonexistent@COMP_A"));
    }

    [Fact]
    public async Task ReadSeriesAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var reader = new FileSeriesReader();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.ReadSeriesAsync(testRunDirectory, "nonexistent@COMP_A"));
    }

    [Fact]
    public async Task ReadManifestAsync_ValidManifest_ReturnsData()
    {
        var schema = LoadManifestSchema();
        var manifestText = await File.ReadAllTextAsync(Path.Combine(testRunDirectory, "manifest.json"));
        var evaluation = schema.Evaluate(JsonNode.Parse(manifestText)!, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(evaluation.IsValid, string.Join("; ", CollectErrors(evaluation)));

        var reader = new FileSeriesReader();
        var manifest = await reader.ReadManifestAsync(testRunDirectory);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(writeResult.ScenarioHash, manifest.ScenarioHash);
        Assert.Null(manifest.Provenance);
    }

    [Fact]
    public async Task ReadManifestAsync_InvalidSnakeCaseProvenance_Throws()
    {
        var invalidPath = Path.Combine(artifactRoot, "invalid-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(invalidPath);

        var manifestText = await File.ReadAllTextAsync(Path.Combine(testRunDirectory, "manifest.json"));
        var node = JsonNode.Parse(manifestText) ?? throw new InvalidOperationException("Manifest could not be parsed");
        node["provenance"] = new JsonObject
        {
            ["has_provenance"] = true,
            ["model_id"] = "sim-order",
            ["template_id"] = "transportation-basic"
        };
        await File.WriteAllTextAsync(Path.Combine(invalidPath, "manifest.json"), node.ToJsonString());

        var reader = new FileSeriesReader();
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadManifestAsync(invalidPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static RunArtifactWriter.WriteResult CreateRunArtifacts(out string runDirectory, out string rootDirectory)
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), "flowtime-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);

        var grid = new TimeGrid
        {
            Bins = 4,
            BinSize = 1,
            BinUnit = "hours"
        };

        var model = new ModelDefinition
        {
            SchemaVersion = 1,
            Grid = new GridDefinition { Bins = 4, BinSize = 1, BinUnit = "hours" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "demand", Kind = "const", Values = new[] { 10d, 20d, 30d, 40d } },
                new() { Id = "served", Kind = "const", Values = new[] { 8d, 16d, 24d, 32d } }
            },
            Outputs = new List<OutputDefinition>
            {
                new() { Series = "demand", As = "demand@COMP_A.csv" },
                new() { Series = "served", As = "served@COMP_A.csv" }
            }
        };

        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 10d, 20d, 30d, 40d },
            [new NodeId("served")] = new[] { 8d, 16d, 24d, 32d }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = "schemaVersion: 1\n",
            DeterministicRunId = true,
            OutputDirectory = rootDirectory
        };

        var result = RunArtifactWriter.WriteArtifactsAsync(request).GetAwaiter().GetResult();
        runDirectory = result.RunDirectory;
        return result;
    }

    private static JsonSchema LoadManifestSchema()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "manifest.schema.json"));
        var schemaText = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaText);
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults results)
    {
        if (results.IsValid)
        {
            yield break;
        }

        if (results.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                yield return $"{results.InstanceLocation}: {error.Value}";
            }
        }

        foreach (var detail in results.Details)
        {
            foreach (var message in CollectErrors(detail))
            {
                yield return message;
            }
        }
    }
}
