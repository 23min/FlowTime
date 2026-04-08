using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests.Service;

public class ProfileEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly string dataDir;
    private readonly string templateYaml;

    public ProfileEndpointsTests(WebApplicationFactory<Program> factory)
    {
        dataDir = Path.Combine(Path.GetTempPath(), "flow-sim-profile-tests", Guid.NewGuid().ToString("N"));
        var templatesDir = Path.Combine(dataDir, "templates");
        var storageRoot = Path.Combine(dataDir, "storage");
        Directory.CreateDirectory(templatesDir);
        Directory.CreateDirectory(storageRoot);

        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", dataDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", templatesDir);
        Environment.SetEnvironmentVariable("Storage__Backend", "filesystem");
        Environment.SetEnvironmentVariable("Storage__Root", storageRoot);

        factory = factory.WithWebHostBuilder(builder => { });
        client = factory.CreateClient();

        templateYaml = @"schemaVersion: 1
generator: flowtime-sim
metadata:
  id: draft-profile
  title: Draft Profile
  description: Draft profile template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: Service
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []
nodes:
  - id: arrivals
    kind: pmf
    pmf:
      values: [10, 20]
      probabilities: [0.5, 0.5]
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
    public async Task FitProfile_FromSeries_ReturnsWeights()
    {
        var ingestRequest = new
        {
            seriesId = "profile_series",
            format = "csv",
            content = "bin,value\n0,10\n1,20\n2,30\n",
            metadata = new { units = "items", binSize = 1, binUnit = "hours" }
        };
        var ingestContent = new StringContent(JsonSerializer.Serialize(ingestRequest), Encoding.UTF8, "application/json");
        var ingestResponse = await client.PostAsync("/api/v1/series/ingest", ingestContent);
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);

        var fitRequest = new { mode = "profile", seriesId = "profile_series", detailLevel = "basic" };
        var fitContent = new StringContent(JsonSerializer.Serialize(fitRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/profiles/fit", fitContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var profile = json.RootElement.GetProperty("profile");
        Assert.Equal("inline", profile.GetProperty("kind").GetString());
        Assert.Equal(3, profile.GetProperty("weights").GetArrayLength());
    }

    [Fact]
    public async Task FitPmf_FromSamples_ReturnsDistribution()
    {
        var fitRequest = new
        {
            mode = "pmf",
            samples = new[] { 1d, 1d, 2d, 3d }
        };
        var fitContent = new StringContent(JsonSerializer.Serialize(fitRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/profiles/fit", fitContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pmf = json.RootElement.GetProperty("pmf");
        Assert.Equal(3, pmf.GetProperty("values").GetArrayLength());
        Assert.Equal(3, pmf.GetProperty("probabilities").GetArrayLength());
    }

    [Fact]
    public async Task FitProfile_DetailLevelExpert_ReturnsDiagnostics()
    {
        var ingestRequest = new
        {
            seriesId = "profile_expert",
            format = "csv",
            content = "bin,value\n0,5\n1,10\n2,15\n"
        };
        var ingestContent = new StringContent(JsonSerializer.Serialize(ingestRequest), Encoding.UTF8, "application/json");
        var ingestResponse = await client.PostAsync("/api/v1/series/ingest", ingestContent);
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);

        var fitRequest = new { mode = "profile", seriesId = "profile_expert", detailLevel = "expert" };
        var fitContent = new StringContent(JsonSerializer.Serialize(fitRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/profiles/fit", fitContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("diagnostics", out _));
    }

    [Fact]
    public async Task PreviewProfile_ReturnsSummary()
    {
        var previewRequest = new
        {
            profile = new { kind = "inline", weights = new[] { 0.5, 1.0, 1.5 } },
            detailLevel = "basic"
        };
        var previewContent = new StringContent(JsonSerializer.Serialize(previewRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/profiles/preview", previewContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("summary", out _));
    }

    [Fact]
    public async Task MapProfileToDraft_UpdatesYaml()
    {
        var request = new
        {
            source = new { type = "inline", id = "draft-profile", content = templateYaml },
            nodeId = "arrivals",
            profile = new { kind = "inline", weights = new[] { 0.5, 1.0, 1.5 } },
            provenance = new Dictionary<string, string> { ["profile.source"] = "series:profile_series" }
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/drafts/map-profile", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var updatedYaml = json.RootElement.GetProperty("content").GetString();
        Assert.NotNull(updatedYaml);
        Assert.Contains("profile:", updatedYaml);
        Assert.Contains("weights:", updatedYaml);
    }
}
