using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for POST /v1/sweep.
///
/// The Rust engine is NOT enabled in the test factory, so all tests that reach
/// the engine will receive 503. Validation tests (400) are exercised independently.
/// </summary>
public sealed class SweepEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client;

    private const string MinimalValidYaml = """
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

    public SweepEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { paramId = "arrivals", values = new[] { 10.0, 20.0 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = (string?)null, paramId = "arrivals", values = new[] { 10.0 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingParamId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = MinimalValidYaml, values = new[] { 10.0, 20.0 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullParamId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = MinimalValidYaml, paramId = (string?)null, values = new[] { 10.0 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullValues_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = MinimalValidYaml, paramId = "arrivals", values = (double[]?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyValues_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = MinimalValidYaml, paramId = "arrivals", values = Array.Empty<double>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        // The test factory does NOT set RustEngine:Enabled=true, so SweepRunner is not
        // registered and the endpoint must return 503.
        var response = await client.PostAsJsonAsync("/v1/sweep",
            new { yaml = MinimalValidYaml, paramId = "arrivals", values = new[] { 10.0, 20.0 } });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
