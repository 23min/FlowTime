using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests.Service;

public class DraftEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly string templateYaml;

    public DraftEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var testDataDir = Path.Combine(Path.GetTempPath(), "flow-sim-draft-tests", Guid.NewGuid().ToString("N"));
        var templatesDir = Path.Combine(testDataDir, "templates");
        var storageRoot = Path.Combine(testDataDir, "storage");
        Directory.CreateDirectory(templatesDir);
        Directory.CreateDirectory(storageRoot);

        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", testDataDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", templatesDir);
        Environment.SetEnvironmentVariable("Storage__Backend", "filesystem");
        Environment.SetEnvironmentVariable("Storage__Root", storageRoot);

        factory = factory.WithWebHostBuilder(builder => { });
        client = factory.CreateClient();

        templateYaml = @"schemaVersion: 1
generator: flowtime-sim
metadata:
  id: draft-inline
  title: Draft Inline
  description: Draft inline template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: bins
    type: integer
    description: Number of bins
    default: 3
  - name: binSize
    type: integer
    description: Size of each bin
    default: 1
grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes
topology:
  nodes:
    - id: DraftService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 100, 100]
  - id: errors
    kind: const
    values: [0, 0, 0]
  - id: served
    kind: expr
    expr: ""arrivals""
outputs:
  - series: ""*""
";
    }

    [Fact]
    public async Task ValidateDraftInline_ReturnsValidPayload()
    {
        var request = new
        {
            source = new { type = "inline", id = "draft-inline", content = templateYaml },
            parameters = new { bins = 3, binSize = 1 },
            mode = "simulation"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/drafts/validate", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(json.RootElement.TryGetProperty("warnings", out var warnings));
        Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
    }

    [Fact]
    public async Task GenerateDraftInline_ReturnsModel()
    {
        var request = new
        {
            source = new { type = "inline", id = "draft-inline", content = templateYaml },
            parameters = new { bins = 3, binSize = 1 },
            mode = "simulation"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/drafts/generate", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("model", out var modelElement));
        var modelYaml = modelElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(modelYaml));
    }

    [Fact]
    public async Task ValidateDraftId_ReturnsValidPayload()
    {
        var createRequest = new
        {
            draftId = "draft-file",
            content = templateYaml.Replace("draft-inline", "draft-file")
        };
        var createContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync("/api/v1/drafts", createContent);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var request = new
        {
            source = new { type = "draftId", id = "draft-file" },
            parameters = new { bins = 3, binSize = 1 },
            mode = "simulation"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/drafts/validate", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task RunDraftInline_ReturnsRunId()
    {
        var request = new
        {
            source = new { type = "inline", id = "draft-inline", content = templateYaml },
            parameters = new { bins = 3, binSize = 1 },
            mode = "simulation"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/drafts/run", content);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected Created but got {response.StatusCode}. Body: {body}");
        }

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("runId", out _));
        Assert.True(json.RootElement.TryGetProperty("bundleRef", out _));
    }
}
