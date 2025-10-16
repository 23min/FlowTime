using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Configuration;

namespace FlowTime.Api.Tests;

public class RunOrchestrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string templateId = "test-order";

    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;
    private readonly string dataRoot;
    private readonly string templateDirectory;

    public RunOrchestrationTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        dataRoot = factory.TestDataDirectory;
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", dataRoot);

        templateDirectory = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(Path.Combine(templateDirectory, $"{templateId}.yaml"), TestTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["TemplatesDirectory"] = templateDirectory,
                    ["ArtifactsDirectory"] = dataRoot
                };
                config.AddInMemoryCollection(settings!);
            });
        });

        client = customizedFactory.CreateClient();
    }

    [Fact]
    public async Task CreateTelemetryRun_ReturnsMetadataAndCreatesRun()
    {
        var captureDir = await CreateTelemetryCaptureAsync();

        var requestPayload = new
        {
            templateId,
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
            parameters = new
            {
                bins = 4,
                binSize = 5
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        var metadata = payload["metadata"] ?? throw new InvalidOperationException("Metadata is missing from response.");
        var runId = metadata["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId));

        var runPath = Path.Combine(dataRoot, runId!);
        Assert.True(Directory.Exists(runPath), $"Expected run directory to exist at {runPath}");
        Assert.True(File.Exists(Path.Combine(runPath, "model", "model.yaml")), "Expected model.yaml to be present.");

        var detail = await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}");
        Assert.NotNull(detail);
        Assert.Equal(runId, detail!["metadata"]?["runId"]?.GetValue<string>());
        Assert.Equal("telemetry", detail["metadata"]?["mode"]?.GetValue<string>());
        Assert.True(detail["metadata"]?["telemetrySourcesResolved"]?.GetValue<bool?>());

        var listing = await client.GetFromJsonAsync<JsonNode>("/v1/runs");
        Assert.NotNull(listing);
        var items = listing!["items"]?.AsArray() ?? new JsonArray();
        Assert.Contains(items, node => string.Equals(node?["runId"]?.GetValue<string>(), runId, StringComparison.OrdinalIgnoreCase));
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
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);
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
