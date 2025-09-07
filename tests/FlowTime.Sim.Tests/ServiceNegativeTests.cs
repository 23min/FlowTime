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
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT", root);
    }

    [Fact]
    public async Task Get_Index_Unknown_Run_Returns_404()
    {
    var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/sim/runs/does-not-exist/index");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Not found", body);
    }

    [Fact]
    public async Task Get_Series_Unknown_Run_Returns_404()
    {
    var client = factory.CreateClient();
        var res = await client.GetAsync("/v1/sim/runs/does-not-exist/series/arrivals@COMP_A");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_Series_Unknown_Series_Returns_404()
    {
        // Create a real run first
    var client = factory.CreateClient();
        var yaml = "schemaVersion: 1\nrng: pcg\nseed: 1\ngrid:\n  bins: 2\n  binMinutes: 60\narrivals:\n  kind: const\n  values: [1,1]\nroute:\n  id: COMP_A\n";
        var create = await client.PostAsync("/v1/sim/run", new StringContent(yaml, Encoding.UTF8, "text/plain"));
        create.EnsureSuccessStatusCode();
        var runId = await ExtractField(create, "simRunId");
        var res = await client.GetAsync($"/v1/sim/runs/{runId}/series/served@NOTCOMP");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Overlay_BaseRun_NotFound_Returns_404()
    {
    var client = factory.CreateClient();
        var payload = "{ \"baseRunId\": \"missing_run\", \"overlay\": { \"seed\": 123 } }";
        var res = await client.PostAsync("/v1/sim/overlay", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_RunId_Format_Returns_400()
    {
    var client = factory.CreateClient();
        // Use a dot which is disallowed by IsSafeId but does not trigger path segment normalization like "../"
        var res = await client.GetAsync("/v1/sim/runs/bad.id/index");
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
