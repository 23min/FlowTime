using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;

namespace FlowTime.Api.Tests;

public class RunOrchestrationGoldenTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;
    private readonly string dataRoot;
    private readonly string templateDirectory;

    public RunOrchestrationGoldenTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        dataRoot = factory.TestDataDirectory;
        templateDirectory = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(Path.Combine(templateDirectory, "test-order.yaml"), TestTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDirectory);
            builder.UseSetting("ArtifactsDirectory", dataRoot);
        });

        client = customizedFactory.CreateClient();
    }

    [Fact]
    public async Task CreateRun_ResponseMatchesGolden()
    {
        var captureDir = await CreateTelemetryCaptureAsync();

        var payload = new
        {
            templateId = "test-order",
            mode = "telemetry",
            telemetry = new
            {
                captureDirectory = captureDir,
                bindings = new Dictionary<string, string>
                {
                    ["telemetryArrivals"] = "OrderService_arrivals.csv",
                    ["telemetryServed"] = "OrderService_served.csv"
                }
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var createNode = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        var runId = createNode["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing.");

        var sanitizedCreate = SanitizeCreateResponse(createNode);
        AssertGolden("create-run-response.golden.json", sanitizedCreate);

        var detailNode = (await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}")) ?? throw new InvalidOperationException("Detail response missing.");
        var sanitizedDetail = SanitizeCreateResponse(detailNode);
        AssertGolden("get-run-response.golden.json", sanitizedDetail);

        var listNode = (await client.GetFromJsonAsync<JsonNode>("/v1/runs?mode=telemetry&hasWarnings=false&page=1&pageSize=10")) ?? throw new InvalidOperationException("List response missing.");
        var sanitizedList = SanitizeListResponse(listNode);
        AssertGolden("list-runs-response.golden.json", sanitizedList);
    }

    private static void AssertGolden(string fileName, JsonNode actual)
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Golden", fileName);
        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException($"Golden file not found at {expectedPath}. Actual sanitized payload:\n{actual.ToJsonString(SerializerOptions)}");
        }

        var expectedJson = File.ReadAllText(expectedPath);
        var expectedNode = JsonNode.Parse(expectedJson) ?? throw new InvalidOperationException($"Golden file '{fileName}' did not contain valid JSON.");

        Assert.Equal(
            expectedNode.ToJsonString(SerializerOptions),
            actual.ToJsonString(SerializerOptions));
    }

    private static JsonNode SanitizeCreateResponse(JsonNode node)
    {
        var clone = node.DeepClone();
        if (clone is JsonObject obj)
        {
            obj["warnings"] = obj["warnings"] ?? new JsonArray();
            obj["canReplay"] = obj["canReplay"] is null ? JsonValue.Create<bool?>(null) : obj["canReplay"];
        }

        if (clone["metadata"] is JsonObject metadata)
        {
            metadata["runId"] = "RUN_ID";
            metadata["provenanceHash"] = "PROVENANCE_HASH";
            if (metadata["storage"] is JsonObject storage)
            {
                storage["modelPath"] = "MODEL_PATH";
                storage["metadataPath"] = "METADATA_PATH";
                storage["provenancePath"] = storage["provenancePath"] is null ? null : "PROVENANCE_PATH";
            }

            if (metadata["schema"] is JsonObject schema)
            {
                schema["hash"] = "SCHEMA_HASH";
            }
        }

        return clone;
    }

    private static JsonNode SanitizeListResponse(JsonNode node)
    {
        var clone = node.DeepClone();
        if (clone["items"] is JsonArray items)
        {
            JsonNode? firstClone = null;
            foreach (var element in items)
            {
                if (element is JsonObject item)
                {
                    item["runId"] = "RUN_ID";
                    if (item["createdUtc"] is not null)
                    {
                        item["createdUtc"] = "CREATED_UTC";
                    }
                    firstClone ??= item.DeepClone();
                }
            }

            if (firstClone is not null)
            {
                items.Clear();
                items.Add(firstClone);
            }
        }

        clone["totalCount"] = 1;
        clone["page"] = 1;

        return clone;
    }

    private async Task<string> CreateTelemetryCaptureAsync()
    {
        var sourceRoot = Path.Combine(dataRoot, $"source_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(sourceRoot, "run_capture", includeTopology: true);
        var captureDir = Path.Combine(sourceRoot, "capture");
        Directory.CreateDirectory(captureDir);

        var capture = new TelemetryCapture();
        await capture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        return captureDir;
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private const string TestTemplateYaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-order
  title: Test Order System
  description: Test telemetry playback template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: telemetryArrivals
    type: string
    default: ""
  - name: telemetryServed
    type: string
    default: ""

grid:
  bins: 4
  binSize: 5
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: "order_arrivals"
        served: "order_served"
        errors: "order_errors"
  edges: []

nodes:
  - id: order_arrivals
    kind: const
    source: ${telemetryArrivals}
  - id: order_served
    kind: const
    source: ${telemetryServed}
  - id: order_errors
    kind: const
    values: [0, 0, 0, 0]

outputs:
  - series: order_arrivals
    as: OrderService_arrivals.csv
  - series: order_served
    as: OrderService_served.csv
  - series: order_errors
    as: OrderService_errors.csv
""";
}
