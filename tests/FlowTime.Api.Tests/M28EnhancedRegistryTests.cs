using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FlowTime.API;
using FlowTime.Contracts.Services;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for M2.8 enhanced artifacts registry features
/// </summary>
public class M28EnhancedRegistryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;
    
    public M28EnhancedRegistryTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task EnhancedQueryOptions_ShouldSupportDateRangeFiltering()
    {
        // Arrange: Setup test data directory with artifacts from different dates
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_date_filter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create artifacts with different dates
            var oldDate = DateTime.UtcNow.AddDays(-10);
            var recentDate = DateTime.UtcNow.AddDays(-1);
            
            await CreateTestArtifact(testDir, oldDate, "old");
            await CreateTestArtifact(testDir, recentDate, "recent");

            using var testClient = CreateTestClient(testDir);

            // Rebuild index
            await testClient.PostAsync("/v1/artifacts/index", null);

            // Test: Filter for recent artifacts only
            var response = await testClient.GetAsync($"/v1/artifacts?createdAfter={DateTime.UtcNow.AddDays(-2):yyyy-MM-ddTHH:mm:ssZ}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ArtifactListResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            Assert.Contains("recent", result.Artifacts[0].Id);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnhancedQueryOptions_ShouldSupportFileSizeFiltering()
    {
        // Arrange: Setup test data directory
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_size_filter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create artifacts with different file sizes
            await CreateTestArtifactWithSize(testDir, "small", 100);
            await CreateTestArtifactWithSize(testDir, "large", 5000);

            using var testClient = CreateTestClient(testDir);

            // Rebuild index
            await testClient.PostAsync("/v1/artifacts/index", null);

            // Test: Filter for large files only
            var response = await testClient.GetAsync("/v1/artifacts?minSize=1000");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ArtifactListResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            Assert.Contains("large", result.Artifacts[0].Id);
            Assert.True(result.Artifacts[0].TotalSize >= 1000);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnhancedQueryOptions_ShouldSupportFullTextSearch()
    {
        // Arrange: Setup test data directory
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_fulltext_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create artifacts with specific content
            await CreateTestArtifactWithMetadata(testDir, "searchable", new Dictionary<string, object>
            {
                ["description"] = "This artifact contains unique_keyword for testing",
                ["category"] = "simulation"
            });
            
            await CreateTestArtifactWithMetadata(testDir, "regular", new Dictionary<string, object>
            {
                ["description"] = "Regular artifact without matching content",
                ["category"] = "analysis"
            });

            using var testClient = CreateTestClient(testDir);

            // Rebuild index
            await testClient.PostAsync("/v1/artifacts/index", null);

            // Test: Full-text search for specific keywords
            var response = await testClient.GetAsync("/v1/artifacts?fullText=unique_keyword");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ArtifactListResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            Assert.Contains("searchable", result.Artifacts[0].Id);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task ArtifactRelationships_ShouldReturnRelatedArtifacts()
    {
        // Arrange: Setup test data directory with multiple artifacts
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_relationships_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create several related artifacts
            var baseTime = DateTime.UtcNow;
            await CreateTestArtifact(testDir, baseTime, "base", new[] { "simulation", "test" });
            await CreateTestArtifact(testDir, baseTime.AddMinutes(10), "related1", new[] { "simulation", "analysis" });
            await CreateTestArtifact(testDir, baseTime.AddMinutes(20), "related2", new[] { "test", "validation" });
            await CreateTestArtifact(testDir, baseTime.AddDays(1), "unrelated", new[] { "other" });

            using var testClient = CreateTestClient(testDir);

            // Rebuild index
            await testClient.PostAsync("/v1/artifacts/index", null);

            // Get the base artifact ID
            var artifactsResponse = await testClient.GetAsync("/v1/artifacts");
            var artifactsContent = await artifactsResponse.Content.ReadAsStringAsync();
            var artifacts = JsonSerializer.Deserialize<ArtifactListResponse>(artifactsContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var baseArtifact = artifacts?.Artifacts.FirstOrDefault(a => a.Id.Contains("base"));
            
            Assert.NotNull(baseArtifact);

            // Test: Get relationships for base artifact
            var response = await testClient.GetAsync($"/v1/artifacts/{baseArtifact.Id}/relationships");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ArtifactRelationships>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(result);
            Assert.Equal(baseArtifact.Id, result.ArtifactId);
            Assert.True(result.Related.Count > 0);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnhancedPagination_ShouldSupportUpTo1000Artifacts()
    {
        // Arrange: Setup test data directory
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_pagination_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            using var testClient = CreateTestClient(testDir);

            // Test: Request limit above 1000 should be capped
            var response = await testClient.GetAsync("/v1/artifacts?limit=1500");
            
            // Should not fail (even with no artifacts)
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public async Task ArtifactRelationships_ShouldReturn404ForNonexistentArtifact()
    {
        // Arrange: Setup empty test directory
        var testDir = Path.Combine(Path.GetTempPath(), $"test_registry_404_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            using var testClient = CreateTestClient(testDir);

            // Rebuild empty index
            await testClient.PostAsync("/v1/artifacts/index", null);

            // Test: Request relationships for nonexistent artifact
            var response = await testClient.GetAsync("/v1/artifacts/nonexistent_id/relationships");
            
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, recursive: true);
        }
    }

    private HttpClient CreateTestClient(string dataDirectory)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", dataDirectory);
        }).CreateClient();
    }

    private static async Task CreateTestArtifact(string baseDir, DateTime created, string suffix, string[]? tags = null)
    {
        var timestamp = created.ToString("yyyyMMddTHHmmssZ");
        var runDir = Path.Combine(baseDir, $"run_{timestamp}_{suffix}");
        Directory.CreateDirectory(runDir);

        // Create manifest.json
        var manifest = new
        {
            runId = $"run_{timestamp}_{suffix}",
            created = created.ToString("O"),
            tags = tags ?? new[] { "test" }
        };
        
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Create a small output file
        await File.WriteAllTextAsync(Path.Combine(runDir, "output.csv"), "time,value\n0,1\n1,2\n");
    }

    private static async Task CreateTestArtifactWithSize(string baseDir, string suffix, int sizeBytes)
    {
        var created = DateTime.UtcNow;
        var timestamp = created.ToString("yyyyMMddTHHmmssZ");
        var runDir = Path.Combine(baseDir, $"run_{timestamp}_{suffix}");
        Directory.CreateDirectory(runDir);

        // Create manifest.json
        var manifest = new { runId = $"run_{timestamp}_{suffix}", created = created.ToString("O") };
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "manifest.json"),
            JsonSerializer.Serialize(manifest));

        // Create file with specific size
        var content = new string('X', Math.Max(sizeBytes - 100, 10)); // Account for manifest size
        await File.WriteAllTextAsync(Path.Combine(runDir, "data.txt"), content);
    }

    private static async Task CreateTestArtifactWithMetadata(string baseDir, string suffix, Dictionary<string, object> metadata)
    {
        var created = DateTime.UtcNow;
        var timestamp = created.ToString("yyyyMMddTHHmmssZ");
        var runDir = Path.Combine(baseDir, $"run_{timestamp}_{suffix}");
        Directory.CreateDirectory(runDir);

        // Create manifest.json with metadata
        var manifest = new
        {
            runId = $"run_{timestamp}_{suffix}",
            created = created.ToString("O"),
            metadata = metadata
        };
        
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Create output file
        await File.WriteAllTextAsync(Path.Combine(runDir, "output.csv"), "time,value\n0,1\n");
    }
}
