using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;
using System.Security.Cryptography;

namespace FlowTime.Api.Tests;

public class StateResponseSchemaTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;
    private readonly JsonSchema schema;

    private static readonly object setupLock = new();
    private static bool schemaRunCreated;

    private const string schemaRunId = "run_schema_validation";
    private const int binCount = 4;
    private const int binSizeMinutes = 5;
    private static readonly DateTime startTimeUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public StateResponseSchemaTests(TestWebApplicationFactory factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        client = factory.CreateClient();

        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "time-travel-state.schema.json"));
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"State schema file not found at '{schemaPath}'.");
        }

        schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
    }

    [Fact]
    public async Task State_Response_MatchesSchema()
    {
        var runId = EnsureSchemaRun();
        var response = await client.GetAsync($"/v1/runs/{runId}/state?binIndex=1");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        ValidateAgainstSchema(json);
    }

    [Fact]
    public async Task StateWindow_Response_MatchesSchema()
    {
        var runId = EnsureSchemaRun();
        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        ValidateAgainstSchema(json);
    }

    private string EnsureSchemaRun()
    {
        lock (setupLock)
        {
            if (!schemaRunCreated)
            {
                var runDir = Path.Combine(factory.TestDataDirectory, schemaRunId);
                var modelDir = Path.Combine(runDir, "model");

                if (!Directory.Exists(runDir))
                {
                    Directory.CreateDirectory(modelDir);
                    WriteBaseSeries(modelDir);
                    var modelPath = Path.Combine(modelDir, "model.yaml");
                    File.WriteAllText(modelPath, BuildValidModelYaml(), System.Text.Encoding.UTF8);
                    var modelHash = ComputeFileHash(modelPath);
                    WriteMetadata(modelDir, schemaRunId, "telemetry", modelHash);
                    File.WriteAllText(Path.Combine(runDir, "run.json"), BuildRunJson(schemaRunId, "telemetry", modelHash), System.Text.Encoding.UTF8);
                }

                schemaRunCreated = true;
            }

            return schemaRunId;
        }
    }

    private void ValidateAgainstSchema(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException($"Response payload could not be parsed as JSON: {ex.Message}\nPayload:\n{json}");
        }

        if (node is null)
        {
            throw new Xunit.Sdk.XunitException("Response payload parsed to null JSON node.");
        }

        var evaluation = schema.Evaluate(node);
        if (!evaluation.IsValid)
        {
            throw new Xunit.Sdk.XunitException($"Response failed schema validation. Payload:{Environment.NewLine}{json}");
        }
    }

    private static void WriteBaseSeries(string modelDir)
    {
        WriteSeries(modelDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(modelDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_capacity.csv", new double[] { 12, 7, 9, 4 });

        WriteSeries(modelDir, "SupportQueue_arrivals.csv", new double[] { 9, 7, 9, 5 });
        WriteSeries(modelDir, "SupportQueue_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "SupportQueue_errors.csv", new double[] { 0, 0, 0, 0 });
        WriteSeries(modelDir, "SupportQueue_queue.csv", new double[] { 2, 10, 20, 0 });
    }

    private static void WriteSeries(string directory, string fileName, IReadOnlyList<double> values)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path);
        writer.NewLine = "\n";
        writer.WriteLine("bin_index,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{values[i]}"));
        }
    }

    private static void WriteMetadata(string modelDirectory, string runIdentifier, string mode, string modelHash)
    {
        var metadata = new
        {
            templateId = "order-system",
            templateTitle = "Order System Fixture",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode,
            modelHash
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "metadata.json"), metadataJson, System.Text.Encoding.UTF8);

        var provenance = new
        {
            source = "flowtime-sim",
            templateId = "order-system",
            templateVersion = "1.0.0",
            mode,
            modelId = runIdentifier,
            schemaVersion = 1
        };

        var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "provenance.json"), provenanceJson, System.Text.Encoding.UTF8);
    }

    private static string BuildRunJson(string runIdentifier, string mode, string modelHash)
    {
        var grid = new
        {
            bins = binCount,
            binSize = binSizeMinutes,
            binUnit = "minutes",
            binMinutes = binSizeMinutes,
            timezone = "UTC",
            align = "left"
        };

        var manifest = new
        {
            schemaVersion = 1,
            runId = runIdentifier,
            engineVersion = "0.0-test",
            source = mode,
            grid,
            modelHash,
            scenarioHash = "sha256:test",
            createdUtc = startTimeUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            warnings = Array.Empty<string>(),
            series = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static string BuildValidModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        slaMin: null
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queue: "file:SupportQueue_queue.csv"
        capacity: null
  edges: []
""";
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
