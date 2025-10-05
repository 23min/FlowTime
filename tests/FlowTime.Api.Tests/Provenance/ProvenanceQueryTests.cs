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
        var runA = await CreateTestRunWithProvenance(templateId: "template-1", modelId: "model_123");
        var runB = await CreateTestRunWithProvenance(templateId: "template-2", modelId: "model_456");

        // Act: Query by modelId
        var response = await client.GetAsync("/v1/artifacts?modelId=model_123");

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
        var runA = await CreateTestRunWithProvenance(templateId: "template-1", modelId: "model_123");
        var runB = await CreateTestRunWithProvenance(templateId: "template-2", modelId: "model_123");
        var runC = await CreateTestRunWithProvenance(templateId: "template-1", modelId: "model_456");

        // Act: Query by both templateId and modelId
        var response = await client.GetAsync("/v1/artifacts?templateId=template-1&modelId=model_123");

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
        await CreateTestRunWithProvenance(templateId: "template-1", modelId: "model_123");
        await CreateTestRunWithProvenance(templateId: "template-2", modelId: "model_456");

        // Act: Query for non-existent templateId
        var response = await client.GetAsync("/v1/artifacts?templateId=non-existent");

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
        await CreateTestRunWithProvenance(templateId: "Transportation-Basic", modelId: "model_123");

        // Act: Query with different casing
        var response = await client.GetAsync("/v1/artifacts?templateId=transportation-basic");

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
            await CreateTestRunWithProvenance(templateId: "template-1", modelId: $"model_{i}");
            // Small delay to ensure different timestamps
            await Task.Delay(10);
        }

        // Act: Query with pagination
        var response = await client.GetAsync("/v1/artifacts?templateId=template-1&limit=2&skip=0");

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
            runIds.Add(await CreateTestRunWithProvenance(templateId: "template-1", modelId: $"model_{i}"));
            await Task.Delay(100); // Ensure different timestamps
        }

        // Act: Query with sorting by created descending
        var response = await client.GetAsync("/v1/artifacts?templateId=template-1&sortBy=created&sortOrder=desc");

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
        var runA = await CreateTestRunWithProvenance(templateId: "template-1", modelId: "model_123");
        var runB = await CreateTestRunWithoutProvenance();
        
        // Also test artifact with provenance.json but no templateId field (malformed)
        var runC = await CreateTestRunWithProvenance(templateId: null, modelId: "model_456");

        // Act: Query by templateId
        var response = await client.GetAsync("/v1/artifacts?templateId=template-1");

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
}
