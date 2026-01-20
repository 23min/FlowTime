using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests.Service;

public class SeriesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly string dataDir;
    private readonly string seriesRoot;

    public SeriesEndpointsTests(WebApplicationFactory<Program> factory)
    {
        dataDir = Path.Combine(Path.GetTempPath(), "flow-sim-series-tests", Guid.NewGuid().ToString("N"));
        seriesRoot = Path.Combine(dataDir, "series");
        Directory.CreateDirectory(dataDir);

        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", dataDir);

        factory = factory.WithWebHostBuilder(builder => { });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestCsv_ReturnsSeries()
    {
        var request = new
        {
            seriesId = "arrivals_demo",
            format = "csv",
            content = "bin,value\n0,100\n1,120\n2,90\n",
            metadata = new { units = "items", source = "test", binSize = 1, binUnit = "hours" }
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/series/ingest", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("arrivals_demo", json.RootElement.GetProperty("seriesId").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("count").GetInt32());

        var seriesDir = Path.Combine(seriesRoot, "arrivals_demo");
        Assert.True(Directory.Exists(seriesDir));
    }

    [Fact]
    public async Task IngestMalformed_ReturnsBadRequest()
    {
        var request = new
        {
            seriesId = "bad_series",
            format = "csv",
            content = "bin,value\n0,100\n1,abc\n"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/series/ingest", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Summarize_ReturnsStats()
    {
        var seriesId = "summary_series";
        var ingestRequest = new
        {
            seriesId,
            format = "csv",
            content = "bin,value\n0,100\n1,120\n2,90\n3,110\n",
            metadata = new { units = "items", binSize = 1, binUnit = "hours" }
        };
        var ingestContent = new StringContent(JsonSerializer.Serialize(ingestRequest), Encoding.UTF8, "application/json");
        var ingestResponse = await client.PostAsync("/api/v1/series/ingest", ingestContent);
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);

        var summaryRequest = new { seriesId, detailLevel = "basic" };
        var summaryContent = new StringContent(JsonSerializer.Serialize(summaryRequest), Encoding.UTF8, "application/json");
        var summaryResponse = await client.PostAsync("/api/v1/series/summarize", summaryContent);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var json = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
        Assert.Equal(seriesId, json.RootElement.GetProperty("seriesId").GetString());
        Assert.Equal(4, json.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(90, json.RootElement.GetProperty("min").GetDouble());
        Assert.Equal(120, json.RootElement.GetProperty("max").GetDouble());
        Assert.True(json.RootElement.TryGetProperty("percentiles", out var percentiles));
        Assert.Equal(JsonValueKind.Object, percentiles.ValueKind);
    }

    [Fact]
    public async Task Summarize_DetailLevelBasic()
    {
        var seriesId = "detail_basic";
        var ingestRequest = new
        {
            seriesId,
            format = "csv",
            content = "bin,value\n0,10\n1,20\n2,30\n",
            metadata = new { units = "items", binSize = 1, binUnit = "hours" }
        };
        var ingestContent = new StringContent(JsonSerializer.Serialize(ingestRequest), Encoding.UTF8, "application/json");
        var ingestResponse = await client.PostAsync("/api/v1/series/ingest", ingestContent);
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);

        var summaryRequest = new { seriesId, detailLevel = "basic" };
        var summaryContent = new StringContent(JsonSerializer.Serialize(summaryRequest), Encoding.UTF8, "application/json");
        var summaryResponse = await client.PostAsync("/api/v1/series/summarize", summaryContent);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var json = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("diagnostics", out _));
    }

    [Fact]
    public async Task Summarize_DetailLevelExpert()
    {
        var seriesId = "detail_expert";
        var ingestRequest = new
        {
            seriesId,
            format = "csv",
            content = "bin,value\n0,10\n1,20\n2,30\n",
            metadata = new { units = "items", binSize = 1, binUnit = "hours" }
        };
        var ingestContent = new StringContent(JsonSerializer.Serialize(ingestRequest), Encoding.UTF8, "application/json");
        var ingestResponse = await client.PostAsync("/api/v1/series/ingest", ingestContent);
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);

        var summaryRequest = new { seriesId, detailLevel = "expert" };
        var summaryContent = new StringContent(JsonSerializer.Serialize(summaryRequest), Encoding.UTF8, "application/json");
        var summaryResponse = await client.PostAsync("/api/v1/series/summarize", summaryContent);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var json = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("diagnostics", out var diagnostics));
        Assert.Equal(JsonValueKind.Object, diagnostics.ValueKind);
    }
}
