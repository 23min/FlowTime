using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace FlowTime.Tests.Schemas;

public class TelemetryManifestSchemaTests
{
    [Fact]
    public void ManifestSchema_Requires_SupportsClassMetrics_Flag()
    {
        var schema = LoadTelemetryManifestSchema();
        var manifest = CreateBaseManifest();

        var evaluation = schema.Evaluate(manifest, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.False(evaluation.IsValid, "Manifest without supportsClassMetrics should be invalid.");
    }

    [Fact]
    public void ManifestSchema_Requires_ClassFields_When_SupportsTrue()
    {
        var schema = LoadTelemetryManifestSchema();
        var manifest = CreateBaseManifest(includeClassId: false);
        manifest["supportsClassMetrics"] = true;
        manifest["classes"] = new JsonArray("Retail", "Wholesale");
        manifest["classCoverage"] = "full";

        var evaluation = schema.Evaluate(manifest, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.False(evaluation.IsValid, "supportsClassMetrics=true must require per-file classId entries.");
    }

    [Fact]
    public void ManifestSchema_Allows_TotalsOnly_When_SupportsFalse()
    {
        var schema = LoadTelemetryManifestSchema();
        var manifest = CreateBaseManifest(includeClassId: false);
        manifest["supportsClassMetrics"] = false;

        var evaluation = schema.Evaluate(manifest, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(evaluation.IsValid, evaluation.ToString());
    }

    private static JsonObject CreateBaseManifest(bool includeClassId = true)
    {
        var file = new JsonObject
        {
            ["nodeId"] = "OrderService",
            ["metric"] = "Arrivals",
            ["path"] = "OrderService_arrivals.csv",
            ["hash"] = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ["points"] = 4
        };

        if (includeClassId)
        {
            file["classId"] = "DEFAULT";
        }

        var manifest = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["grid"] = new JsonObject
            {
                ["bins"] = 4,
                ["binSize"] = 5,
                ["binUnit"] = "minutes"
            },
            ["files"] = new JsonArray(file),
            ["provenance"] = new JsonObject
            {
                ["runId"] = "run_abc",
                ["scenarioHash"] = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                ["capturedAtUtc"] = "2025-11-30T00:00:00Z"
            }
        };

        return manifest;
    }

    private static JsonSchema LoadTelemetryManifestSchema()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "telemetry-manifest.schema.json"));
        var schemaNode = JsonNode.Parse(File.ReadAllText(schemaPath)) ?? throw new InvalidOperationException("Schema JSON could not be parsed.");
        if (schemaNode is JsonObject schemaObject)
        {
            schemaObject.Remove("$schema");
        }

        return JsonSchema.FromText(schemaNode.ToJsonString());
    }
}
