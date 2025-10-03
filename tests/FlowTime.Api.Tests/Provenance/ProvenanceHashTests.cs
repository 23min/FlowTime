using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests that provenance metadata does NOT affect model_hash calculation.
/// Two models with identical logic but different provenance should have the same hash.
/// This ensures proper deduplication of functionally equivalent models.
/// </summary>
public class ProvenanceHashTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProvenanceHashTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRun_SameModelDifferentProvenance_SameHash()
    {
        // Arrange
        var client = _factory.CreateClient();
        
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
              - id: served
                kind: expr
                expr: "demand * 0.8"
            """;

        // First request with provenance A
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request1.Headers.Add("X-Model-Provenance", "model_20250925T120000Z_provenanceA");

        // Second request with provenance B
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request2.Headers.Add("X-Model-Provenance", "model_20250925T130000Z_provenanceB");

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);

        // Same model logic → same hash (provenance excluded)
        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task PostRun_WithAndWithoutProvenance_SameHash()
    {
        // Arrange
        var client = _factory.CreateClient();
        
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

        // First request with provenance
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request1.Headers.Add("X-Model-Provenance", "model_20250925T140000Z_withprov");

        // Second request without provenance
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);

        // Assert
        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task PostRun_EmbeddedProvenanceDifferentValues_SameHash()
    {
        // Arrange
        var client = _factory.CreateClient();

        var model1 = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20250925T150000Z_first
              templateId: template-A
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var model2 = """
            schemaVersion: 1
            provenance:
              source: manual
              modelId: model_20250925T160000Z_second
              templateId: template-B
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model1, new MediaTypeHeaderValue("application/x-yaml"))
        };

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model2, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);

        // Assert
        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);

        // Different provenance but same execution logic → same hash
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task PostRun_DifferentModel_DifferentHash()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var model1 = """
            schemaVersion: 1
            provenance:
              modelId: model_20250925T170000Z_model1
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var model2 = """
            schemaVersion: 1
            provenance:
              modelId: model_20250925T180000Z_model2
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [200, 220, 250, 230]
            """;

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model1, new MediaTypeHeaderValue("application/x-yaml"))
        };

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model2, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);

        // Assert
        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);

        // Different execution logic → different hash (provenance irrelevant)
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task PostRun_ComplexProvenanceParameters_DoesNotAffectHash()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var model1 = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20250925T190000Z_complex1
              templateId: it-system
              parameters:
                bins: 12
                loadBalancerCount: 3
                replicaCount: 5
                region: us-east-1
            grid:
              bins: 8
              binSize: 1
              binUnit: hours
            nodes:
              - id: requests
                kind: const
                values: [100, 120, 150, 180, 200, 220, 200, 180]
            """;

        var model2 = """
            schemaVersion: 1
            provenance:
              source: manual
              modelId: model_20250925T200000Z_complex2
              templateId: custom-system
              parameters:
                bins: 24
                serverCount: 10
                cacheSize: 1000
                region: eu-west-1
            grid:
              bins: 8
              binSize: 1
              binUnit: hours
            nodes:
              - id: requests
                kind: const
                values: [100, 120, 150, 180, 200, 220, 200, 180]
            """;

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model1, new MediaTypeHeaderValue("application/x-yaml"))
        };

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model2, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);

        // Assert
        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);

        // Complex, different provenance parameters → same hash (execution spec identical)
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task PostRun_ModelDeduplication_WorksWithProvenance()
    {
        // Arrange
        var client = _factory.CreateClient();
        
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

        // Run same model 3 times with different provenance
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request1.Headers.Add("X-Model-Provenance", "model_20250925T210000Z_run1");

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request2.Headers.Add("X-Model-Provenance", "model_20250925T220000Z_run2");

        var request3 = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request3.Headers.Add("X-Model-Provenance", "model_20250925T230000Z_run3");

        // Act
        var response1 = await client.SendAsync(request1);
        var response2 = await client.SendAsync(request2);
        var response3 = await client.SendAsync(request3);

        // Assert
        var hash1 = await ExtractModelHashFromResponse(response1);
        var hash2 = await ExtractModelHashFromResponse(response2);
        var hash3 = await ExtractModelHashFromResponse(response3);

        // All three should have same hash (enables deduplication)
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);

        // But different run_ids (different executions)
        var runId1 = await ExtractRunIdFromResponse(response1);
        var runId2 = await ExtractRunIdFromResponse(response2);
        var runId3 = await ExtractRunIdFromResponse(response3);

        Assert.NotEqual(runId1, runId2);
        Assert.NotEqual(runId2, runId3);
    }

    // Helper methods
    private async Task<string?> ExtractModelHashFromResponse(HttpResponseMessage response)
    {
        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);
        // API returns camelCase
        if (doc.TryGetProperty("modelHash", out var modelHash))
        {
            return modelHash.GetString();
        }
        return null;
    }

    private async Task<string?> ExtractRunIdFromResponse(HttpResponseMessage response)
    {
        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);
        if (doc.TryGetProperty("runId", out var runId))  // API returns camelCase
        {
            return runId.GetString();
        }
        return null;
    }
}
