using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ServiceNegativeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public ServiceNegativeTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(_ => { });
        var root = Path.Combine(Path.GetTempPath(), "flow-sim-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", root);
    }

    [Fact]
    public async Task Get_Index_Unknown_Run_Returns_404()
    {
    var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/sim/runs/does-not-exist/index");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        // Endpoint removed; framework returns default 404 body
    }

    [Fact]
    public async Task Get_Series_Unknown_Run_Returns_404()
    {
    var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/sim/runs/does-not-exist/series/arrivals@COMP_A");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact(Skip = "Legacy /v1/sim/runs endpoint removed - API now uses /api/v1 endpoints")]
    public async Task Invalid_RunId_Format_Returns_400()
    {
    var client = factory.CreateClient();
        // Use a dot which is disallowed by IsSafeId but does not trigger path segment normalization like "../"
        var res = await client.GetAsync("/v1/sim/runs/bad.id/index");
        // Endpoint validates ID format first, returns 400 for invalid format
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private static async Task<string> ExtractField(HttpResponseMessage res, string field)
    {
        var json = await res.Content.ReadAsStringAsync();
        var marker = "\"" + field + "\":";
        var idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.True(idx >= 0, $"Field {field} not found in response: {json}");
        var start = json.IndexOf('"', idx + marker.Length);
        var end = json.IndexOf('"', start + 1);
        return json.Substring(start + 1, end - start - 1);
    }
}
