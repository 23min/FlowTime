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
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", root);
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
        var res = await client.PostAsync("/v1/sim/run", content);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var runId = await ExtractField(res, "simRunId");
        Assert.StartsWith("sim_", runId);

        var idx = await client.GetAsync($"/v1/sim/runs/{runId}/index");
        Assert.Equal(HttpStatusCode.OK, idx.StatusCode);
        var indexJson = await idx.Content.ReadAsStringAsync();
        Assert.Contains("arrivals@COMP_A", indexJson);

        var series = await client.GetAsync($"/v1/sim/runs/{runId}/series/arrivals@COMP_A");
        Assert.Equal(HttpStatusCode.OK, series.StatusCode);
        var csv = await series.Content.ReadAsStringAsync();
        Assert.Contains("t,value", csv);
        Assert.Contains("0,1", csv);
    }

    [Fact]
    public async Task Overlay_Creates_New_Run()
    {
    var client = appFactory.CreateClient();
    var baseRes = await client.PostAsync("/v1/sim/run", new StringContent(sampleConstYaml, Encoding.UTF8, "text/plain"));
        baseRes.EnsureSuccessStatusCode();
        var baseRunId = await ExtractField(baseRes, "simRunId");
        var overlayPayload = "{ \"baseRunId\": \"" + baseRunId + "\", \"overlay\": { \"seed\": 99 } }";
        var overlayRes = await client.PostAsync("/v1/sim/overlay", new StringContent(overlayPayload, Encoding.UTF8, "application/json"));
        overlayRes.EnsureSuccessStatusCode();
        var newRunId = await ExtractField(overlayRes, "simRunId");
        Assert.NotEqual(baseRunId, newRunId);
    }

    [Fact]
    public async Task Cli_And_Service_Per_Series_Hash_Parity()
    {
    var client = appFactory.CreateClient();
    var res = await client.PostAsync("/v1/sim/run", new StringContent(sampleConstYaml, Encoding.UTF8, "text/plain"));
        res.EnsureSuccessStatusCode();
        var runId = await ExtractField(res, "simRunId");
        var dataDir = Environment.GetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR")!;
        var serviceSeriesPath = Path.Combine(dataDir, "runs", runId, "series", "arrivals@COMP_A.csv");
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

    [Fact]
    public async Task Catalogs_List_Returns_Available_Catalogs()
    {
        var client = appFactory.CreateClient();
        
        // Set up test catalogs directory
        var testDataDir = Path.Combine(Path.GetTempPath(), "flow-sim-catalog-tests", Guid.NewGuid().ToString("N"));
        var testCatalogsRoot = Path.Combine(testDataDir, "catalogs");
        Directory.CreateDirectory(testCatalogsRoot);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", testDataDir);

        // Create a test catalog
        var testCatalogYaml = @"version: 1
metadata:
  id: test-catalog
  title: Test Catalog
  description: A test catalog for unit tests
components:
  - id: COMP_A
    label: Component A
  - id: COMP_B  
    label: Component B
connections:
  - from: COMP_A
    to: COMP_B
classes: [""DEFAULT""]
layoutHints:
  rankDir: LR";

        await File.WriteAllTextAsync(Path.Combine(testCatalogsRoot, "test-catalog.yaml"), testCatalogYaml);

        var res = await client.GetAsync("/v1/sim/catalogs");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        
        // Should contain our test catalog
        Assert.Contains("test-catalog", json);
        Assert.Contains("Test Catalog", json);
        Assert.Contains("\"componentCount\":2", json);
        Assert.Contains("\"connectionCount\":1", json);
    }

    [Fact]
    public async Task Catalogs_Get_Returns_Specific_Catalog()
    {
        var client = appFactory.CreateClient();
        
        // Set up test catalogs directory 
        var testDataDir = Path.Combine(Path.GetTempPath(), "flow-sim-catalog-tests", Guid.NewGuid().ToString("N"));
        var testCatalogsRoot = Path.Combine(testDataDir, "catalogs");
        Directory.CreateDirectory(testCatalogsRoot);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", testDataDir);

        var testCatalogYaml = @"version: 1
metadata:
  id: specific-test
  title: Specific Test Catalog
components:
  - id: NODE_X
    label: Node X
classes: [""DEFAULT""]";

        await File.WriteAllTextAsync(Path.Combine(testCatalogsRoot, "specific-test.yaml"), testCatalogYaml);

        var res = await client.GetAsync("/v1/sim/catalogs/specific-test");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        
        Assert.Contains("NODE_X", json);
        Assert.Contains("Specific Test Catalog", json);
    }

    [Fact] 
    public async Task Catalogs_Get_Returns_NotFound_For_Missing_Catalog()
    {
        var client = appFactory.CreateClient();
        var res = await client.GetAsync("/v1/sim/catalogs/non-existent");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Catalogs_Validate_Returns_Valid_For_Good_Catalog()
    {
        var client = appFactory.CreateClient();
        
        var validCatalogYaml = @"version: 1
metadata:
  id: valid-test
  title: Valid Test Catalog
components:
  - id: COMP_A
    label: Component A
  - id: COMP_B
    label: Component B
connections:
  - from: COMP_A
    to: COMP_B
classes: [""DEFAULT""]";

        var content = new StringContent(validCatalogYaml, Encoding.UTF8, "text/plain");
        var res = await client.PostAsync("/v1/sim/catalogs/validate", content);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"valid\":true", json);
        Assert.Contains("\"hash\":\"sha256:", json);
        Assert.Contains("\"componentCount\":2", json);
    }

    [Fact]
    public async Task Catalogs_Validate_Returns_Invalid_For_Bad_Catalog()
    {
        var client = appFactory.CreateClient();
        
        var invalidCatalogYaml = @"version: 1
metadata:
  id: invalid-test
  title: Invalid Test Catalog
components:
  - id: COMP@INVALID
    label: Bad Component
connections:
  - from: COMP@INVALID
    to: NON_EXISTENT
classes: [""DEFAULT""]";

        var content = new StringContent(invalidCatalogYaml, Encoding.UTF8, "text/plain");
        var res = await client.PostAsync("/v1/sim/catalogs/validate", content);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"valid\":false", json);
        Assert.Contains("errors", json);
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