using System.Net;
using System.Net.Http.Json;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for POST /v1/sensitivity.
/// The Rust engine is NOT enabled in the test factory — all paths that reach
/// the engine return 503. Validation (400) is checked independently.
/// </summary>
public sealed class SensitivityEndpointsTests : IClassFixture<TestWebApplicationFactory>
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

    public SensitivityEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { paramIds = new[] { "arrivals" }, metricSeriesId = "arrivals" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = (string?)null, paramIds = new[] { "arrivals" }, metricSeriesId = "arrivals" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = MinimalYaml, paramIds = (string[]?)null, metricSeriesId = "arrivals" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = MinimalYaml, paramIds = Array.Empty<string>(), metricSeriesId = "arrivals" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = MinimalYaml, paramIds = new[] { "arrivals" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = MinimalYaml, paramIds = new[] { "arrivals" }, metricSeriesId = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        var response = await client.PostAsJsonAsync("/v1/sensitivity",
            new { yaml = MinimalYaml, paramIds = new[] { "arrivals" }, metricSeriesId = "arrivals" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
