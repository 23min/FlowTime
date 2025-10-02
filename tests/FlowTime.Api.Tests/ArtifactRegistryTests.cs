using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using Xunit;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for M2.7 Artifacts Registry functionality
/// Covers registry index management, new API endpoints, and engine integration
/// </summary>
public class ArtifactRegistryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ArtifactRegistryTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    // =============================================================================
    // PHASE 1: Registry Index Core Tests (Foundation - Write Before Implementation)
    // =============================================================================

    [Fact]
    public async Task Registry_ScanDirectory_CreatesValidIndex()
    {
        // Arrange: Create test artifacts directory with multiple runs
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_scan");
        Directory.CreateDirectory(artifactsDir);

        // Create multiple test runs with different characteristics
        await CreateTestRun(artifactsDir, "run_20250921T100000Z_test001", "Simple Model", new[] { "basic", "test" });
        await CreateTestRun(artifactsDir, "run_20250921T110000Z_test002", "Complex Model", new[] { "advanced", "production" });
        await CreateTestRun(artifactsDir, "run_20250921T120000Z_test003", "Debug Model", new[] { "debug", "experimental" });

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Act: Trigger registry scan (POST /v1/artifacts/index to rebuild)
        var response = await clientWithConfig.PostAsync("/v1/artifacts/index", null);

        // Assert: Registry index created successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify index file was created
        var indexPath = Path.Combine(artifactsDir, "registry-index.json");
        Assert.True(File.Exists(indexPath), "Registry index file should be created");

        // Verify index content structure
        var indexContent = await File.ReadAllTextAsync(indexPath);
        var indexData = JsonSerializer.Deserialize<JsonElement>(indexContent);
        
        Assert.True(indexData.TryGetProperty("artifacts", out var artifacts));
        Assert.Equal(3, artifacts.GetArrayLength());

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    [Fact]
    public async Task Registry_AddArtifact_UpdatesIndex()
    {
        // Arrange: Create base registry
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_add");
        Directory.CreateDirectory(artifactsDir);

        await CreateTestRun(artifactsDir, "run_20250921T100000Z_base", "Base Model", new[] { "base" });

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Build initial index
        await clientWithConfig.PostAsync("/v1/artifacts/index", null);
        
        var indexPath = Path.Combine(artifactsDir, "registry-index.json");
        var initialContent = await File.ReadAllTextAsync(indexPath);
        var initialData = JsonSerializer.Deserialize<JsonElement>(initialContent);
        Assert.Equal(1, initialData.GetProperty("artifacts").GetArrayLength());

        // Act: Add new artifact (simulate POST /v1/run creating new run)
        await CreateTestRun(artifactsDir, "run_20250921T110000Z_new", "New Model", new[] { "new" });
        
        // Trigger index update
        var updateResponse = await clientWithConfig.PostAsync("/v1/artifacts/index", null);

        // Assert: Index updated with new artifact
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        
        var updatedContent = await File.ReadAllTextAsync(indexPath);
        var updatedData = JsonSerializer.Deserialize<JsonElement>(updatedContent);
        Assert.Equal(2, updatedData.GetProperty("artifacts").GetArrayLength());

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    [Fact]
    public async Task Registry_InvalidArtifact_HandlesGracefully()
    {
        // Arrange: Create directory with valid and invalid artifacts
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_invalid");
        Directory.CreateDirectory(artifactsDir);

        // Valid artifact
        await CreateTestRun(artifactsDir, "run_20250921T100000Z_valid", "Valid Model", new[] { "valid" });
        
        // Invalid artifact (missing manifest.json)
        var invalidRunPath = Path.Combine(artifactsDir, "run_20250921T110000Z_invalid");
        Directory.CreateDirectory(invalidRunPath);
        await File.WriteAllTextAsync(Path.Combine(invalidRunPath, "run.json"), "{}");
        // Deliberately not creating manifest.json

        // Corrupt artifact (invalid JSON)
        var corruptRunPath = Path.Combine(artifactsDir, "run_20250921T120000Z_corrupt");
        Directory.CreateDirectory(corruptRunPath);
        await File.WriteAllTextAsync(Path.Combine(corruptRunPath, "manifest.json"), "{ invalid json");

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Act: Scan registry with invalid artifacts
        var response = await clientWithConfig.PostAsync("/v1/artifacts/index", null);

        // Assert: Registry handles gracefully, only includes valid artifacts
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var indexPath = Path.Combine(artifactsDir, "registry-index.json");
        Assert.True(File.Exists(indexPath));
        
        var indexContent = await File.ReadAllTextAsync(indexPath);
        var indexData = JsonSerializer.Deserialize<JsonElement>(indexContent);
        
        // Should only contain the valid artifact
        Assert.Equal(1, indexData.GetProperty("artifacts").GetArrayLength());

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    // =============================================================================
    // PHASE 2: Migration & Backward Compatibility Tests (Critical for Existing Data)
    // =============================================================================

    [Fact]
    public async Task Registry_Import_ExistingArtifacts_Success()
    {
        // Arrange: Use actual existing data structure (simulate current /data directory)
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_import");
        Directory.CreateDirectory(artifactsDir);

        // Create artifacts that match current data structure exactly
        await CreateRealWorldTestRun(artifactsDir, "run_20250921T161133Z_0bcdbb6f");

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Act: Import existing artifacts into registry
        var response = await clientWithConfig.PostAsync("/v1/artifacts/index", null);

        // Assert: Successfully imports existing structure
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var indexPath = Path.Combine(artifactsDir, "registry-index.json");
        Assert.True(File.Exists(indexPath));
        
        var indexContent = await File.ReadAllTextAsync(indexPath);
        var indexData = JsonSerializer.Deserialize<JsonElement>(indexContent);
        
        var artifacts = indexData.GetProperty("artifacts");
        Assert.Equal(1, artifacts.GetArrayLength());
        
        var artifact = artifacts[0];
        Assert.Equal("run_20250921T161133Z_0bcdbb6f", artifact.GetProperty("id").GetString());
        Assert.Equal("run", artifact.GetProperty("type").GetString());

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    [Fact]
    public async Task Registry_MissingIndex_Rebuilds_Successfully()
    {
        // Arrange: Create artifacts but no index
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_rebuild");
        Directory.CreateDirectory(artifactsDir);

        await CreateTestRun(artifactsDir, "run_20250921T100000Z_rebuild", "Rebuild Test", new[] { "rebuild" });

        var indexPath = Path.Combine(artifactsDir, "registry-index.json");
        Assert.False(File.Exists(indexPath), "Index should not exist initially");

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Act: First API call should trigger index rebuild
        var response = await clientWithConfig.GetAsync("/v1/artifacts");

        // Assert: Index created automatically and query succeeds
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(indexPath), "Index should be created automatically");

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.Equal(1, result.GetProperty("artifacts").GetArrayLength());

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    // =============================================================================
    // PHASE 3: Basic Performance Tests (Ensure Sub-500ms Queries)  
    // =============================================================================

    [Fact]
    public async Task Registry_Query_MultipleArtifacts_Under500ms()
    {
        // Arrange: Create larger dataset (50 artifacts to simulate realistic load)
        var tempDir = Path.GetTempPath();
        var artifactsDir = Path.Combine(tempDir, "test_registry_performance");
        Directory.CreateDirectory(artifactsDir);

        for (int i = 0; i < 50; i++)
        {
            var runId = $"run_20250921T{i:D6}Z_perf{i:D3}";
            var title = $"Performance Test Model {i}";
            var tags = new[] { "performance", i % 2 == 0 ? "even" : "odd", $"batch_{i / 10}" };
            await CreateTestRun(artifactsDir, runId, title, tags);
        }

        var clientWithConfig = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsDir);
        }).CreateClient();

        // Build index first
        await clientWithConfig.PostAsync("/v1/artifacts/index", null);

        // Act: Measure query performance
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await clientWithConfig.GetAsync("/v1/artifacts?limit=20");
        stopwatch.Stop();

        // Assert: Query completes under 500ms
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, 
            $"Query took {stopwatch.ElapsedMilliseconds}ms, should be under 500ms");

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(result.GetProperty("artifacts").GetArrayLength() > 0);

        // Cleanup
        Directory.Delete(artifactsDir, true);
    }

    // =============================================================================
    // Helper Methods for Test Setup
    // =============================================================================

    private static async Task CreateTestRun(string artifactsDir, string runId, string title, string[] tags)
    {
        var runPath = Path.Combine(artifactsDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));
        Directory.CreateDirectory(Path.Combine(runPath, "gold"));

        // Create manifest.json (current structure)
        var manifest = new
        {
            schemaVersion = 1,
            scenarioHash = "sha256:test123",
            rng = new { kind = "pcg32", seed = 12345 },
            seriesHashes = new { },
            eventCount = 0,
            createdUtc = DateTime.UtcNow.ToString("O"),
            modelHash = "sha256:model123"
        };
        await File.WriteAllTextAsync(Path.Combine(runPath, "manifest.json"), 
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Create run.json (current structure)
        var run = new
        {
            schemaVersion = 1,
            runId = runId,
            engineVersion = "0.5.0",
            source = "engine",
            grid = new { bins = 8, binMinutes = 60, timezone = "UTC", align = "left" },
            modelHash = "sha256:model123",
            scenarioHash = "sha256:test123",
            createdUtc = DateTime.UtcNow.ToString("O"),
            warnings = new string[0],
            title = title,
            tags = tags
        };
        await File.WriteAllTextAsync(Path.Combine(runPath, "run.json"),
            JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));

        // Create spec.yaml
        await File.WriteAllTextAsync(Path.Combine(runPath, "spec.yaml"), 
            "grid:\n  bins: 8\n  binMinutes: 60\nnodes:\n  - id: test\n    kind: const\n    values: [1,2,3]");

        // Create minimal series data
        await File.WriteAllTextAsync(Path.Combine(runPath, "series", "index.json"), "{}");
    }

    private static async Task CreateRealWorldTestRun(string artifactsDir, string runId)
    {
        // Create structure that exactly matches current data format
        var runPath = Path.Combine(artifactsDir, runId);
        Directory.CreateDirectory(runPath);
        Directory.CreateDirectory(Path.Combine(runPath, "series"));
        Directory.CreateDirectory(Path.Combine(runPath, "gold"));

        // Real manifest.json structure
        var manifest = @"{
  ""schemaVersion"": 1,
  ""scenarioHash"": ""sha256:4bbc16a4b1d56c50161dd58617ce611802be04f31d093278bd3376a0fa66610e"",
  ""rng"": {
    ""kind"": ""pcg32"",
    ""seed"": 1131659569
  },
  ""seriesHashes"": {
    ""served@SERVED@DEFAULT"": ""sha256:56b8cc47e07cd1792cfb441a55a70da75a4d1d9395b579cc18527e88f927a6b9""
  },
  ""eventCount"": 0,
  ""createdUtc"": ""2025-09-21T16:11:33.5157160Z"",
  ""modelHash"": ""sha256:4bbc16a4b1d56c50161dd58617ce611802be04f31d093278bd3376a0fa66610e""
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "manifest.json"), manifest);

        // Real run.json structure  
        var run = @"{
  ""schemaVersion"": 1,
  ""runId"": ""run_20250921T161133Z_0bcdbb6f"",
  ""engineVersion"": ""0.5.0"",
  ""source"": ""engine"",
  ""grid"": {
    ""bins"": 8,
    ""binMinutes"": 60,
    ""timezone"": ""UTC"",
    ""align"": ""left""
  },
  ""modelHash"": ""sha256:4bbc16a4b1d56c50161dd58617ce611802be04f31d093278bd3376a0fa66610e"",
  ""scenarioHash"": ""sha256:4bbc16a4b1d56c50161dd58617ce611802be04f31d093278bd3376a0fa66610e"",
  ""createdUtc"": ""2025-09-21T16:11:33.4794927Z"",
  ""warnings"": []
}";
        await File.WriteAllTextAsync(Path.Combine(runPath, "run.json"), run);

        // Real spec.yaml
        var spec = @"grid:
  bins: 8
  binMinutes: 60
nodes:
  - id: served
    kind: const
    values: [30, 40, 35, 45, 50, 40, 30, 25]";
        await File.WriteAllTextAsync(Path.Combine(runPath, "spec.yaml"), spec);
    }
}
