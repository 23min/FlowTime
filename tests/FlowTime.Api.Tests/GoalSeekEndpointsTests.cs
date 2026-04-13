using System.Net;
using System.Net.Http.Json;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for POST /v1/goal-seek.
/// The Rust engine is NOT enabled in the test factory.
/// </summary>
public sealed class GoalSeekEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client;

    private const string MinimalYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
        """;

    public GoalSeekEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = (string?)null, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingParamId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLoEqualSearchHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 50.0, searchHi = 50.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLoGreaterThanSearchHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 100.0, searchHi = 10.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSearchBounds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
