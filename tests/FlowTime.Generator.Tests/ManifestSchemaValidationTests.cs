using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using Json.Schema;

namespace FlowTime.Generator.Tests;

public class ManifestSchemaValidationTests
{
    [Fact]
    public async Task RunArtifactWriter_ManifestMatchesSchema()
    {
        var schema = LoadManifestSchema();
        var tempDir = Path.Combine(Path.GetTempPath(), $"flowtime-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var grid = new TimeGrid
            {
                Bins = 4,
                BinSize = 5,
                BinUnit = "minutes"
            };

            var model = new ModelDefinition
            {
                SchemaVersion = 1,
                Grid = new GridDefinition { Bins = 4, BinSize = 5, BinUnit = "minutes" },
                Nodes = new List<NodeDefinition>(),
                Outputs = new List<OutputDefinition>()
            };

            var context = new Dictionary<NodeId, double[]>();
            var writeRequest = new RunArtifactWriter.WriteRequest
            {
                Model = model,
                Grid = grid,
                Context = context,
                SpecText = "schemaVersion: 1\n",
                DeterministicRunId = true,
                OutputDirectory = tempDir
            };

            var writeResult = await RunArtifactWriter.WriteArtifactsAsync(writeRequest);
            var manifestPath = Path.Combine(writeResult.RunDirectory, "manifest.json");
            Assert.True(File.Exists(manifestPath));

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var node = JsonNode.Parse(manifestJson) ?? throw new InvalidOperationException("Manifest JSON invalid");
            var evaluation = schema.Evaluate(node);
            Assert.True(evaluation.IsValid, evaluation.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static JsonSchema LoadManifestSchema()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "manifest.schema.json"));
        var schemaText = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaText);
    }
}
