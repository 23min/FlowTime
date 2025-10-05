using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests for provenance-based artifact querying via API.
/// Validates templateId and modelId query parameters on /v1/artifacts endpoint.
/// </summary>
public class ProvenanceQueryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly TestWebApplicationFactory factory;

    public ProvenanceQueryTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateId_ReturnsMatchingArtifacts()
    {
        // Arrange: Create test artifacts with different templateIds
        var runA = await CreateTestRunWithProvenance(templateId: "transportation-basic", modelId: "model_123");
        var runB = await CreateTestRunWithProvenance(templateId: "manufacturing-line", modelId: "model_456");
        var runC = await CreateTestRunWithoutProvenance();
        await RebuildIndex();

        // Act: Query by templateId
        var response = await client.GetAsync("/v1/artifacts?templateId=transportation-basic");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var artifacts = result.GetProperty("artifacts");
        var count = result.GetProperty("count").GetInt32();
        
        // Should return only Run A
        Assert.Equal(1, count);
        Assert.Equal(runA, artifacts[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetArtifacts_WithModelId_ReturnsExactMatch()
    {
        // Arrange: Create test artifacts with different modelIds
        var runA = await CreateTestRunWithProvenance(templateId: "test2-template-1", modelId: "test2-model_123");
        var runB = await CreateTestRunWithProvenance(templateId: "test2-template-2", modelId: "test2-model_456");
        await RebuildIndex();

        // Act: Query by modelId
        var response = await client.GetAsync("/v1/artifacts?modelId=test2-model_123");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var artifacts = result.GetProperty("artifacts");
        var count = result.GetProperty("count").GetInt32();
        
        // Should return only Run A
        Assert.Equal(1, count);
        Assert.Equal(runA, artifacts[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateIdAndModelId_ReturnsBothMatches()
    {
        // Arrange: Create test artifacts with different combinations
        var runA = await CreateTestRunWithProvenance(templateId: "test3-template-1", modelId: "test3-model_123");
        var runB = await CreateTestRunWithProvenance(templateId: "test3-template-2", modelId: "test3-model_123");
        var runC = await CreateTestRunWithProvenance(templateId: "test3-template-1", modelId: "test3-model_456");
        await RebuildIndex();

        // Act: Query by both templateId and modelId
        var response = await client.GetAsync("/v1/artifacts?templateId=test3-template-1&modelId=test3-model_123");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var artifacts = result.GetProperty("artifacts");
        var count = result.GetProperty("count").GetInt32();
        
        // Should return only Run A (both filters match)
        Assert.Equal(1, count);
        Assert.Equal(runA, artifacts[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetArtifacts_WithNonExistentTemplateId_ReturnsEmptyResult()
    {
        // Arrange: Create test artifacts with different templateIds
        await CreateTestRunWithProvenance(templateId: "test4-template-1", modelId: "test4-model_123");
        await CreateTestRunWithProvenance(templateId: "test4-template-2", modelId: "test4-model_456");
        await RebuildIndex();

        // Act: Query for non-existent templateId
        var response = await client.GetAsync("/v1/artifacts?templateId=test4-non-existent");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var count = result.GetProperty("count").GetInt32();
        var total = result.GetProperty("total").GetInt32();
        
        // Should return empty results
        Assert.Equal(0, count);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task GetArtifacts_TemplateIdCaseSensitive_NoMatch()
    {
        // Arrange: Run with specific templateId casing
        await CreateTestRunWithProvenance(templateId: "test5-Transportation-Basic", modelId: "test5-model_123");
        await RebuildIndex();

        // Act: Query with different casing
        var response = await client.GetAsync("/v1/artifacts?templateId=test5-transportation-basic");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var count = result.GetProperty("count").GetInt32();
        
        // Should return empty results (case mismatch)
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateIdAndPagination_ReturnsPagedResults()
    {
        // Arrange: Create 5 runs with same templateId
        for (int i = 0; i < 5; i++)
        {
            await CreateTestRunWithProvenance(templateId: "test6-template-1", modelId: $"test6-model_{i}");
            // Small delay to ensure different timestamps
            await Task.Delay(10);
        }
        await RebuildIndex();

        // Act: Query with pagination
        var response = await client.GetAsync("/v1/artifacts?templateId=test6-template-1&limit=2&skip=0");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var count = result.GetProperty("count").GetInt32();
        var total = result.GetProperty("total").GetInt32();
        
        // Should return 2 artifacts but total should be 5
        Assert.Equal(2, count);
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateIdAndSorting_ReturnsSortedResults()
    {
        // Arrange: Create 3 runs with same templateId at different times
        var runIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            runIds.Add(await CreateTestRunWithProvenance(templateId: "test7-template-1", modelId: $"test7-model_{i}"));
            // Rebuild index after each creation to ensure distinct timestamps
            await RebuildIndex();
            await Task.Delay(1100); // Ensure distinct second-level timestamps (run_YYYYMMDDTHHMMSSZ format)
        }

        // Act: Query with sorting by created descending
        var response = await client.GetAsync("/v1/artifacts?templateId=test7-template-1&sortBy=created&sortOrder=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var artifacts = result.GetProperty("artifacts");
        
        // Results should be ordered by created timestamp descending (newest first)
        // The last created run should be first in results
        Assert.Equal(runIds[2], artifacts[0].GetProperty("id").GetString());
        Assert.Equal(runIds[0], artifacts[2].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateId_ExcludesArtifactsWithoutProvenance()
    {
        // Arrange: Mix of artifacts with and without provenance
        var runA = await CreateTestRunWithProvenance(templateId: "test8-template-1", modelId: "test8-model_123");
        var runB = await CreateTestRunWithoutProvenance();
        
        // Also test artifact with provenance.json but no templateId field (malformed)
        var runC = await CreateTestRunWithProvenance(templateId: null, modelId: "test8-model_456");

        // Act: Query by templateId
        var response = await client.GetAsync("/v1/artifacts?templateId=test8-template-1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        var artifacts = result.GetProperty("artifacts");
        var count = result.GetProperty("count").GetInt32();
        
        // Should return only Run A (has matching templateId)
        // Run B excluded (no provenance)
        // Run C excluded (no templateId in provenance)
        Assert.Equal(1, count);
        Assert.Equal(runA, artifacts[0].GetProperty("id").GetString());
    }

    // Helper methods

    private async Task<string> CreateTestRunWithProvenance(string? templateId = null, string? modelId = null)
    {
        var model = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Create provenance metadata if either templateId or modelId provided
        if (modelId != null || templateId != null)
        {
            var provenance = new
            {
                source = "flowtime-sim",
                templateId = templateId,
                modelId = modelId
            };
            request.Headers.Add("X-Model-Provenance", JsonSerializer.Serialize(provenance));
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        return result.GetProperty("runId").GetString()!;
    }

    private async Task<string> CreateTestRunWithoutProvenance()
    {
        var model = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        // No X-Model-Provenance header

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        return result.GetProperty("runId").GetString()!;
    }

    private async Task RebuildIndex()
    {
        // Rebuild artifact registry index to ensure all runs are registered
        // This is necessary because run registration is fire-and-forget in the API
        var response = await client.PostAsync("/v1/artifacts/index", null);
        response.EnsureSuccessStatusCode();
    }

    // M2.10: Stable sort tests - verify secondary sort key (Id) provides deterministic ordering
    
    [Fact]
    public async Task GetArtifacts_WithIdenticalTimestamps_SortsStablyById()
    {
        // Arrange: Create artifacts with identical timestamps by using very fast operations
        // All will be created within the same second, resulting in identical Created timestamps
        var runIds = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            runIds.Add(await CreateTestRunWithProvenance(templateId: "test-stable-1", modelId: $"model_{i}"));
        }
        await RebuildIndex();

        // Act: Query sorted by created descending
        var response = await client.GetAsync("/v1/artifacts?templateId=test-stable-1&sortBy=created&sortOrder=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var artifacts = result.GetProperty("artifacts");
        
        // All artifacts should have identical Created timestamps (same second)
        // But order should be stable and consistent (sorted by ID as secondary key)
        var artifactIds = new List<string>();
        for (int i = 0; i < artifacts.GetArrayLength(); i++)
        {
            artifactIds.Add(artifacts[i].GetProperty("id").GetString()!);
        }
        
        // Verify we got all 5 artifacts
        Assert.Equal(5, artifactIds.Count);
        
        // Verify IDs are in descending order (stable secondary sort)
        var sortedIds = artifactIds.OrderByDescending(id => id).ToList();
        Assert.Equal(sortedIds, artifactIds);
        
        // Run query again to verify consistency (should get same order)
        var response2 = await client.GetAsync("/v1/artifacts?templateId=test-stable-1&sortBy=created&sortOrder=desc");
        var responseJson2 = await response2.Content.ReadAsStringAsync();
        var result2 = JsonSerializer.Deserialize<JsonElement>(responseJson2);
        var artifacts2 = result2.GetProperty("artifacts");
        
        var artifactIds2 = new List<string>();
        for (int i = 0; i < artifacts2.GetArrayLength(); i++)
        {
            artifactIds2.Add(artifacts2[i].GetProperty("id").GetString()!);
        }
        
        // Order should be identical on repeated query
        Assert.Equal(artifactIds, artifactIds2);
    }

    [Fact]
    public async Task GetArtifacts_SortByCreated_AscendingIsStable()
    {
        // Arrange: Create artifacts with same timestamp
        var runIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            runIds.Add(await CreateTestRunWithProvenance(templateId: "test-stable-asc", modelId: $"model_{i}"));
        }
        await RebuildIndex();

        // Act: Query sorted by created ascending (oldest first)
        var response = await client.GetAsync("/v1/artifacts?templateId=test-stable-asc&sortBy=created&sortOrder=asc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var artifacts = result.GetProperty("artifacts");
        
        var artifactIds = new List<string>();
        for (int i = 0; i < artifacts.GetArrayLength(); i++)
        {
            artifactIds.Add(artifacts[i].GetProperty("id").GetString()!);
        }
        
        // Verify IDs are in ascending order (stable secondary sort)
        var sortedIds = artifactIds.OrderBy(id => id).ToList();
        Assert.Equal(sortedIds, artifactIds);
    }

    [Fact]
    public async Task GetArtifacts_SortBySizeWithEqualValues_IsStable()
    {
        // Arrange: Create multiple runs (they'll likely have same size since same model)
        var runIds = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            runIds.Add(await CreateTestRunWithProvenance(templateId: "test-stable-size", modelId: $"model_{i}"));
        }
        await RebuildIndex();

        // Act: Query sorted by size descending
        var response = await client.GetAsync("/v1/artifacts?templateId=test-stable-size&sortBy=size&sortOrder=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var artifacts = result.GetProperty("artifacts");
        
        // Multiple queries should return same order
        var firstOrderIds = new List<string>();
        for (int i = 0; i < artifacts.GetArrayLength(); i++)
        {
            firstOrderIds.Add(artifacts[i].GetProperty("id").GetString()!);
        }
        
        // Query again
        var response2 = await client.GetAsync("/v1/artifacts?templateId=test-stable-size&sortBy=size&sortOrder=desc");
        var responseJson2 = await response2.Content.ReadAsStringAsync();
        var result2 = JsonSerializer.Deserialize<JsonElement>(responseJson2);
        var artifacts2 = result2.GetProperty("artifacts");
        
        var secondOrderIds = new List<string>();
        for (int i = 0; i < artifacts2.GetArrayLength(); i++)
        {
            secondOrderIds.Add(artifacts2[i].GetProperty("id").GetString()!);
        }
        
        // Order must be identical (stable sort)
        Assert.Equal(firstOrderIds, secondOrderIds);
    }
}
