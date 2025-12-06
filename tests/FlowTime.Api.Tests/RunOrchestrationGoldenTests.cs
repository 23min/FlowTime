using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Generator.Orchestration;
using FlowTime.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTime.Api.Tests;

public class RunOrchestrationGoldenTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TestWebApplicationFactory factory;
    private readonly WebApplicationFactory<Program> serverFactory;
    private readonly HttpClient client;
    private readonly string dataRoot;
    private readonly string templateDirectory;
    private readonly string sourceRoot;

    public RunOrchestrationGoldenTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        dataRoot = Path.Combine(factory.TestDataDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        templateDirectory = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(Path.Combine(templateDirectory, "test-order.yaml"), TestTemplateYaml);
        File.WriteAllText(Path.Combine(templateDirectory, "sim-order.yaml"), SimulationTemplateYaml);

        sourceRoot = Path.Combine(dataRoot, "golden-source");
        Directory.CreateDirectory(sourceRoot);

        serverFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDirectory);
            builder.UseSetting("ArtifactsDirectory", dataRoot);
        });

        client = serverFactory.CreateClient();
    }

    [Fact]
    public async Task ImportSimulationRun_ResponseMatchesGolden()
    {
        var sourceRunPath = await CreateSimulationBundleAsync();
        var createNode = await ImportRunAsync(sourceRunPath);
        var runId = createNode["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing.");

        var sanitizedCreate = SanitizeCreateResponse(createNode);
        GoldenTestUtils.AssertMatchesGolden("simulation-create-run-response.golden.json", sanitizedCreate);

        var detailNode = (await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}")) ?? throw new InvalidOperationException("Detail response missing.");
        var sanitizedDetail = SanitizeCreateResponse(detailNode);
        GoldenTestUtils.AssertMatchesGolden("simulation-get-run-response.golden.json", sanitizedDetail);
    }

    [Fact]
    public async Task ImportTelemetryRun_ResponseMatchesGolden()
    {
        var sourceRunPath = await CreateTelemetryBundleAsync();
        var createNode = await ImportRunAsync(sourceRunPath);
        var runId = createNode["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing.");

        var sanitizedCreate = SanitizeCreateResponse(createNode);
        GoldenTestUtils.AssertMatchesGolden("create-run-response.golden.json", sanitizedCreate);

        var detailNode = (await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}")) ?? throw new InvalidOperationException("Detail response missing.");
        var sanitizedDetail = SanitizeCreateResponse(detailNode);
        GoldenTestUtils.AssertMatchesGolden("get-run-response.golden.json", sanitizedDetail);

        var listNode = (await client.GetFromJsonAsync<JsonNode>("/v1/runs?mode=telemetry&hasWarnings=false&page=1&pageSize=10")) ?? throw new InvalidOperationException("List response missing.");
        var sanitizedList = SanitizeListResponse(listNode);
        GoldenTestUtils.AssertMatchesGolden("list-runs-response.golden.json", sanitizedList);
    }

    private async Task<JsonNode> ImportRunAsync(string bundlePath)
    {
        var response = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath
        });

        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
    }

    private static JsonNode SanitizeCreateResponse(JsonNode node)
    {
        var clone = node.DeepClone();
        if (clone is JsonObject obj)
        {
            obj["warnings"] = obj["warnings"] ?? new JsonArray();
            obj["canReplay"] = obj["canReplay"] is null ? JsonValue.Create<bool?>(null) : obj["canReplay"];
            if (obj["telemetry"] is JsonObject telemetry)
            {
                var available = telemetry["available"]?.GetValue<bool?>() ?? false;
                telemetry["available"] = available;
                telemetry["generatedAtUtc"] = telemetry["generatedAtUtc"] is null ? null : "GENERATED_AT_UTC";
                telemetry["warningCount"] = telemetry["warningCount"] ?? JsonValue.Create(0);
                if (!available)
                {
                    telemetry["sourceRunId"] = null;
                }
                else
                {
                    var sourceNode = telemetry["sourceRunId"];
                    var sourceValue = sourceNode?.GetValue<string?>();
                    telemetry["sourceRunId"] = string.IsNullOrWhiteSpace(sourceValue) ? null : "CAPTURE_SOURCE_RUN_ID";
                }
            }
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

            if (metadata["inputHash"] is not null)
            {
                metadata["inputHash"] = "INPUT_HASH";
            }

            if (metadata["rng"] is JsonObject rng)
            {
                rng["kind"] = rng["kind"] ?? "pcg32";
                if (rng["seed"] is JsonValue seedValue && seedValue.TryGetValue(out int seed))
                {
                    rng["seed"] = JsonValue.Create(seed);
                }
                else
                {
                    rng["seed"] = JsonValue.Create(123);
                }
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
                    if (item["telemetry"] is JsonObject telemetry)
                    {
                        var available = telemetry["available"]?.GetValue<bool?>() ?? false;
                        telemetry["available"] = available;
                        telemetry["generatedAtUtc"] = telemetry["generatedAtUtc"] is null ? null : "GENERATED_AT_UTC";
                        telemetry["warningCount"] = telemetry["warningCount"] ?? JsonValue.Create(0);
                        if (!available)
                        {
                            telemetry["sourceRunId"] = null;
                        }
                        else
                        {
                            var sourceNode = telemetry["sourceRunId"];
                            var sourceValue = sourceNode?.GetValue<string?>();
                            telemetry["sourceRunId"] = string.IsNullOrWhiteSpace(sourceValue) ? null : "CAPTURE_SOURCE_RUN_ID";
                        }
                    }

                    if (item["rng"] is JsonObject rng)
                    {
                        rng["kind"] = rng["kind"] ?? "pcg32";
                        if (rng["seed"] is JsonValue seedValue && seedValue.TryGetValue(out int seed))
                        {
                            rng["seed"] = JsonValue.Create(seed);
                        }
                        else
                        {
                            rng["seed"] = JsonValue.Create(123);
                        }
                    }

                    if (item["inputHash"] is not null)
                    {
                        item["inputHash"] = "INPUT_HASH";
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

    private async Task<string> CreateSimulationBundleAsync()
    {
        await using var scope = serverFactory.Services.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<RunOrchestrationService>();
        var runsRoot = Path.Combine(sourceRoot, "simulation");
        Directory.CreateDirectory(runsRoot);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "sim-order",
            Mode = "simulation",
            OutputRoot = runsRoot,
            DeterministicRunId = true,
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            }
        };

        var outcome = await orchestration.CreateRunAsync(request).ConfigureAwait(false);
        return outcome.Result?.RunDirectory ?? throw new InvalidOperationException("Failed to create simulation bundle.");
    }

    private async Task<string> CreateTelemetryBundleAsync()
    {
        var captureDir = await CreateTelemetryCaptureAsync();
        await using var scope = serverFactory.Services.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<RunOrchestrationService>();
        var runsRoot = Path.Combine(sourceRoot, "telemetry");
        Directory.CreateDirectory(runsRoot);

        var request = new RunOrchestrationRequest
        {
            TemplateId = "test-order",
            Mode = "telemetry",
            CaptureDirectory = captureDir,
            TelemetryBindings = new Dictionary<string, string>
            {
                ["telemetryArrivals"] = "OrderService_arrivals.csv",
                ["telemetryServed"] = "OrderService_served.csv"
            },
            OutputRoot = runsRoot,
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request).ConfigureAwait(false);
        return outcome.Result?.RunDirectory ?? throw new InvalidOperationException("Failed to create telemetry bundle.");
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
        serverFactory.Dispose();
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

    private const string SimulationTemplateYaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order
  title: Simulation Order Template
  description: Simulation golden template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 4
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [10, 12, 14, 16]
  - id: served
    kind: const
    values: [8, 11, 13, 15]
  - id: errors
    kind: const
    values: [1, 0, 0, 0]

outputs:
  - series: "*"
""";
}
