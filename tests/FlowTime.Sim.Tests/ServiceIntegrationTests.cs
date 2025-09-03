using System.Net;
using System.Security.Cryptography;
using System.Text;
using FlowTime.Sim.Core;
using FlowTime.Sim.Cli; // CLI types
using FlowTime.Sim.Service;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests;

// Facade kept minimal; direct call now that duplicate using removed.
internal static class RunArtifactsWriterFacade
{
    public static Task<RunArtifacts> WriteAsync(string originalYaml, SimulationSpec spec, ArrivalGenerationResult arrivals, string rootOutDir, bool includeEvents, CancellationToken ct)
        => RunArtifactsWriter.WriteAsync(originalYaml, spec, arrivals, rootOutDir, includeEvents, ct);
}

public class ServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> appFactory;
    public ServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        appFactory = factory.WithWebHostBuilder(builder => { });
        var root = Path.Combine(Path.GetTempPath(), "flow-sim-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT", root);
    }

    private static string sampleConstYaml = "schemaVersion: 1\n" +
        "rng: pcg\n" +
        "seed: 42\n" +
        "grid:\n" +
        "  bins: 3\n" +
        "  binMinutes: 60\n" +
        "arrivals:\n" +
        "  kind: const\n" +
        "  values: [1,2,3]\n" +
        "route:\n" +
        "  id: COMP_A\n";

    [Fact]
    public async Task Run_Then_Fetch_Index_And_Series_Succeeds()
    {
    var client = appFactory.CreateClient();
    var content = new StringContent(sampleConstYaml, Encoding.UTF8, "text/plain");
        var res = await client.PostAsync("/sim/run", content);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var runId = await ExtractField(res, "simRunId");
        Assert.StartsWith("sim_", runId);

        var idx = await client.GetAsync($"/sim/runs/{runId}/index");
        Assert.Equal(HttpStatusCode.OK, idx.StatusCode);
        var indexJson = await idx.Content.ReadAsStringAsync();
        Assert.Contains("arrivals@COMP_A", indexJson);

        var series = await client.GetAsync($"/sim/runs/{runId}/series/arrivals@COMP_A");
        Assert.Equal(HttpStatusCode.OK, series.StatusCode);
        var csv = await series.Content.ReadAsStringAsync();
        Assert.Contains("t,value", csv);
        Assert.Contains("0,1", csv);
    }

    [Fact]
    public async Task Overlay_Creates_New_Run()
    {
    var client = appFactory.CreateClient();
    var baseRes = await client.PostAsync("/sim/run", new StringContent(sampleConstYaml, Encoding.UTF8, "text/plain"));
        baseRes.EnsureSuccessStatusCode();
        var baseRunId = await ExtractField(baseRes, "simRunId");
        var overlayPayload = "{ \"baseRunId\": \"" + baseRunId + "\", \"overlay\": { \"seed\": 99 } }";
        var overlayRes = await client.PostAsync("/sim/overlay", new StringContent(overlayPayload, Encoding.UTF8, "application/json"));
        overlayRes.EnsureSuccessStatusCode();
        var newRunId = await ExtractField(overlayRes, "simRunId");
        Assert.NotEqual(baseRunId, newRunId);
    }

    [Fact]
    public async Task Cli_And_Service_Per_Series_Hash_Parity()
    {
    var client = appFactory.CreateClient();
    var res = await client.PostAsync("/sim/run", new StringContent(sampleConstYaml, Encoding.UTF8, "text/plain"));
        res.EnsureSuccessStatusCode();
        var runId = await ExtractField(res, "simRunId");
        var runsRoot = Environment.GetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT")!;
        var serviceSeriesPath = Path.Combine(runsRoot, "runs", runId, "series", "arrivals@COMP_A.csv");
        Assert.True(File.Exists(serviceSeriesPath));
        var serviceHash = await Sha256File(serviceSeriesPath);

        var cliOut = Path.Combine(Path.GetTempPath(), "flow-sim-cli-parity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cliOut);
    var spec = SimulationSpecLoader.LoadFromString(sampleConstYaml);
        var validation = SimulationSpecValidator.Validate(spec);
        Assert.True(validation.IsValid, string.Join(";", validation.Errors));
        var arrivals = ArrivalGenerators.Generate(spec);
    var cliArtifacts = await RunArtifactsWriterFacade.WriteAsync(sampleConstYaml, spec, arrivals, cliOut, includeEvents: true, CancellationToken.None);
    var cliSeriesPath = Path.Combine(cliArtifacts.RunDirectory, "series", "arrivals@COMP_A.csv");
        var cliHash = await Sha256File(cliSeriesPath);

        Assert.Equal(cliHash, serviceHash);
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

    private static async Task<string> Sha256File(string path)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fs, CancellationToken.None);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}