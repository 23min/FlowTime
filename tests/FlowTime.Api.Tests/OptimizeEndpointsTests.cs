using System.Net;
using System.Net.Http.Json;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for POST /v1/optimize.
/// The Rust engine is NOT enabled in the test factory.
/// </summary>
public sealed class OptimizeEndpointsTests : IClassFixture<TestWebApplicationFactory>
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

    public OptimizeEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    private static object ValidBody() => new
    {
        yaml = MinimalYaml,
        paramIds = new[] { "arrivals" },
        metricSeriesId = "metric",
        objective = "minimize",
        searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
    };

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = Array.Empty<string>(),
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingObjective_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidObjective_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "maximise",   // typo
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSearchRanges_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchRangeLoEqualsHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 50.0, hi = 50.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchRangesMissingEntry_Returns400()
    {
        // paramIds names "capacity" but searchRanges only has "arrivals" — missing entry.
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals", "capacity" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", ValidBody());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
