using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Contracts.Storage;
using FlowTime.Generator.Orchestration;
using FlowTime.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTime.Api.Tests;

public class RunOrchestrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string simulationTemplateId = "sim-order";

    private readonly TestWebApplicationFactory factory;
    private readonly WebApplicationFactory<Program> serverFactory;
    private readonly HttpClient client;
    private readonly string dataRoot;
    private readonly string templateDirectory;
    private readonly string sourceRoot;

    public RunOrchestrationTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        dataRoot = Path.Combine(factory.TestDataDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        templateDirectory = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(Path.Combine(templateDirectory, $"{simulationTemplateId}.yaml"), simulationTemplateYaml);
        sourceRoot = Path.Combine(dataRoot, "source-runs");
        Directory.CreateDirectory(sourceRoot);

        serverFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDirectory);
            builder.UseSetting("ArtifactsDirectory", dataRoot);
        });

        client = serverFactory.CreateClient();
    }

    [Fact]
    public async Task ImportRun_FromDirectory_Succeeds()
    {
        var sourceRunPath = await CreateSimulationBundleAsync("sim-import");

        var response = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath = sourceRunPath
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        var runId = payload["metadata"]?["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId), "Response did not include a runId.");

        var detail = await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}");
        Assert.NotNull(detail);
        Assert.Equal(runId, detail?["metadata"]?["runId"]?.GetValue<string>());

        var listing = await client.GetFromJsonAsync<JsonNode>("/v1/runs?page=1&pageSize=50");
        Assert.NotNull(listing);
        var items = listing?["items"]?.AsArray() ?? new JsonArray();
        Assert.Contains(items, node => string.Equals(node?["runId"]?.GetValue<string>(), runId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportRun_FromArchive_Succeeds()
    {
        var sourceRunPath = await CreateSimulationBundleAsync("sim-archive");
        var archiveBase64 = await CreateArchiveBase64Async(sourceRunPath);
        var response = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundleArchiveBase64 = archiveBase64
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var runId = payload?["metadata"]?["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId), "runId missing from response.");

        var detail = await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}");
        Assert.NotNull(detail);
        Assert.Equal(runId, detail?["metadata"]?["runId"]?.GetValue<string>());
    }

    [Fact]
    public async Task ImportRun_FromStorageRef_Succeeds()
    {
        var sourceRunPath = await CreateSimulationBundleAsync("sim-storage");
        var archiveBytes = await CreateArchiveBytesAsync(sourceRunPath);
        var storageRoot = Path.Combine(factory.TestDataDirectory, "storage");
        Directory.CreateDirectory(storageRoot);
        var backend = new FileSystemStorageBackend(storageRoot);
        var runId = Path.GetFileName(sourceRunPath);

        var write = await backend.WriteAsync(new StorageWriteRequest
        {
            Kind = StorageKind.Run,
            Id = runId,
            Content = archiveBytes,
            ContentType = "application/zip"
        });

        var response = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundleRef = write.Reference
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var importedRunId = payload?["metadata"]?["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(importedRunId));
    }

    [Fact]
    public async Task ImportRun_DuplicateWithoutOverwrite_ReturnsConflict()
    {
        var sourceRunPath = await CreateSimulationBundleAsync("sim-duplicate");
        var first = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath = sourceRunPath
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath = sourceRunPath
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadAsStringAsync();
        Assert.Contains("already exists", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRun_WithOverwrite_ReplacesExistingBundle()
    {
        var sourceRunPath = await CreateSimulationBundleAsync("sim-overwrite");
        var first = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath = sourceRunPath
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/v1/runs", new
        {
            bundlePath = sourceRunPath,
            overwriteExisting = true
        });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task LegacyTemplatePayload_ReturnsGone()
    {
        var legacyPayload = new
        {
            templateId = simulationTemplateId,
            mode = "simulation",
            parameters = new { bins = 4 }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", legacyPayload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("FlowTime.Sim", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> CreateSimulationBundleAsync(string runIdPrefix)
    {
        await using var scope = serverFactory.Services.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<RunOrchestrationService>();
        var runsRoot = Path.Combine(sourceRoot, runIdPrefix);
        Directory.CreateDirectory(runsRoot);

        var request = new RunOrchestrationRequest
        {
            TemplateId = simulationTemplateId,
            Mode = "simulation",
            OutputRoot = runsRoot,
            DeterministicRunId = true,
            RunId = $"{runIdPrefix}_{Guid.NewGuid():N}",
            Parameters = new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            }
        };

        var outcome = await orchestration.CreateRunAsync(request).ConfigureAwait(false);
        var result = outcome.Result ?? throw new InvalidOperationException("Run orchestration did not return a bundle.");
        return result.RunDirectory;
    }

    private static async Task<string> CreateArchiveBase64Async(string runDirectory)
    {
        var bytes = await CreateArchiveBytesAsync(runDirectory).ConfigureAwait(false);
        return Convert.ToBase64String(bytes);
    }

    private static async Task<byte[]> CreateArchiveBytesAsync(string runDirectory)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"flowtime_api_import_{Guid.NewGuid():N}.zip");
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }
        ZipFile.CreateFromDirectory(runDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: true);
        var bytes = await File.ReadAllBytesAsync(archivePath).ConfigureAwait(false);
        File.Delete(archivePath);
        return bytes;
    }

    public void Dispose()
    {
        client.Dispose();
        serverFactory.Dispose();
    }

    private const string simulationTemplateYaml = """
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
