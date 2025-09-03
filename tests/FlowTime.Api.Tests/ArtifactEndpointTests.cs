using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Net;

namespace FlowTime.Api.Tests;

public class ArtifactEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public ArtifactEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_Runs_NonExistentRun_Returns_NotFound()
    {
        // Act
        var response = await client.GetAsync("/runs/nonexistent/index");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_Runs_ExistingRun_Returns_Index()
    {
        // Arrange: We need to generate some artifacts first
        // Run the CLI to generate an output
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts");
        Directory.CreateDirectory(runDir);

        // Create a minimal test run directory structure
        var runId = "test_run_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create index.json
        var indexContent = @"{
  ""schemaVersion"": 1,
  ""grid"": {
    ""bins"": 3,
    ""binMinutes"": 60,
    ""timezone"": ""UTC""
  },
  ""series"": [
    {
      ""id"": ""test@TEST@DEFAULT"",
      ""kind"": ""flow"",
      ""path"": ""series/test@TEST@DEFAULT.csv"",
      ""unit"": ""entities/bin"",
      ""componentId"": ""TEST"",
      ""class"": ""DEFAULT"",
      ""points"": 3,
      ""hash"": ""sha256:test""
    }
  ]
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "index.json"), indexContent);

        // Create series CSV
        var csvContent = "t,value\n0,10\n1,20\n2,30\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "test@TEST@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/runs/{runId}/index");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("schemaVersion", content);
        Assert.Contains("test@TEST@DEFAULT", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Runs_ExistingSeries_Returns_CSV()
    {
        // Arrange: Create test artifacts
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_series");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_series_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Create series CSV
        var csvContent = "t,value\n0,10\n1,20\n2,30\n";
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "test@TEST@DEFAULT.csv"), csvContent);

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/runs/{runId}/series/test@TEST@DEFAULT");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.ToString());
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("t,value", content);
        Assert.Contains("0,10", content);
        Assert.Contains("1,20", content);
        Assert.Contains("2,30", content);

        // Cleanup
        Directory.Delete(runDir, true);
    }

    [Fact]
    public async Task GET_Runs_NonExistentSeries_Returns_NotFound()
    {
        // Arrange: Create empty run directory
        var tempDir = Path.GetTempPath();
        var runDir = Path.Combine(tempDir, "test_artifacts_empty");
        Directory.CreateDirectory(runDir);

        var runId = "test_run_empty_123";
        var runPath = Path.Combine(runDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));

        // Configure the factory to use our temp directory
        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", runDir);
        }).CreateClient();

        // Act
        var response = await clientWithConfig.GetAsync($"/runs/{runId}/series/nonexistent@SERIES@DEFAULT");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Cleanup
        Directory.Delete(runDir, true);
    }
}
