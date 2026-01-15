using System.Net;
using System.Net.Http.Json;
using FlowTime.Contracts.TimeTravel;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Xunit;

namespace FlowTime.Sim.Tests;

public class RunOrchestrationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public RunOrchestrationEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PostRun_CreatesDeterministicSimulationBundle()
    {
        var (factory, dataRoot, templatesRoot, previous) = CreateClientWithIsolatedRoots();
        try
        {
            var templatePath = Path.Combine(templatesRoot, "sim-order.yaml");
            await File.WriteAllTextAsync(templatePath, SimulationTemplate);
            var client = factory.CreateClient();

            var request = new RunCreateRequest
            {
                TemplateId = "sim-order",
                Mode = "simulation",
                Options = new RunCreationOptions
                {
                    DeterministicRunId = true,
                    DryRun = false,
                    OverwriteExisting = false
                }
            };

            var response = await client.PostAsJsonAsync("/api/v1/orchestration/runs", request);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"Unexpected status {response.StatusCode}: {responseBody}");

            var payload = await response.Content.ReadFromJsonAsync<RunCreateResponse>(SerializerOptions);
            Assert.NotNull(payload);
            Assert.False(payload!.IsDryRun);
            Assert.False(payload.WasReused);
            Assert.NotNull(payload.Metadata);
            Assert.Equal("sim-order", payload.Metadata!.TemplateId);

            var runDir = Path.Combine(dataRoot, "runs", payload.Metadata.RunId);
            Assert.True(Directory.Exists(runDir));
            Assert.True(File.Exists(Path.Combine(runDir, "model", "model.yaml")));
        }
        finally
        {
            RestoreEnvironment(previous);
        }
    }

    [Fact]
    public async Task PostRun_ReusesExistingBundle_WhenInputsMatch()
    {
        var (factory, dataRoot, templatesRoot, previous) = CreateClientWithIsolatedRoots();
        try
        {
            var templatePath = Path.Combine(templatesRoot, "sim-order.yaml");
            await File.WriteAllTextAsync(templatePath, SimulationTemplate);
            var client = factory.CreateClient();

            var request = new RunCreateRequest
            {
                TemplateId = "sim-order",
                Mode = "simulation",
                Options = new RunCreationOptions
                {
                    DeterministicRunId = true
                }
            };

            var first = await client.PostAsJsonAsync("/api/v1/orchestration/runs", request);
            var firstBody = await first.Content.ReadAsStringAsync();
            Assert.True(first.StatusCode == HttpStatusCode.Created, $"Unexpected status {first.StatusCode}: {firstBody}");
            var firstPayload = await first.Content.ReadFromJsonAsync<RunCreateResponse>(SerializerOptions);
            Assert.NotNull(firstPayload);
            Assert.False(firstPayload!.WasReused);

            var second = await client.PostAsJsonAsync("/api/v1/orchestration/runs", request);
            var secondBody = await second.Content.ReadAsStringAsync();
            Assert.True(second.StatusCode == HttpStatusCode.Created, $"Unexpected status {second.StatusCode}: {secondBody}");
            var secondPayload = await second.Content.ReadFromJsonAsync<RunCreateResponse>(SerializerOptions);
            Assert.NotNull(secondPayload);
            Assert.True(secondPayload!.WasReused);
            Assert.Equal(firstPayload.Metadata!.RunId, secondPayload.Metadata!.RunId);

            var runDir = Path.Combine(dataRoot, "runs", firstPayload.Metadata!.RunId);
            Assert.True(Directory.Exists(runDir));
        }
        finally
        {
            RestoreEnvironment(previous);
        }
    }

    private (WebApplicationFactory<Program> factory, string dataRoot, string templatesRoot, (string? Data, string? Templates) previous) CreateClientWithIsolatedRoots()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "flow-sim-orchestration-tests", Guid.NewGuid().ToString("N"));
        var templatesRoot = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templatesRoot);

        var previous = (
            Data: Environment.GetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR"),
            Templates: Environment.GetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR"));

        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", dataRoot);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", templatesRoot);

        var configuredFactory = this.factory.WithWebHostBuilder(_ => { });

        return (configuredFactory, dataRoot, templatesRoot, previous);
    }

    private static void RestoreEnvironment((string? Data, string? Templates) previous)
    {
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", previous.Data);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", previous.Templates);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SimulationTemplate = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order
  title: Simulation Order Template
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
