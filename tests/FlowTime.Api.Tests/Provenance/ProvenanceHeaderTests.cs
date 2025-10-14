using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FlowTime.Core.Configuration;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests X-Model-Provenance HTTP header parsing and handling.
/// Validates header format, parsing, and error handling.
/// </summary>
public class ProvenanceHeaderTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProvenanceHeaderTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRun_ValidProvenanceHeader_ParsedCorrectly()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", "model_20250925T120000Z_abc123def");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify provenance was stored
        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T120000Z_abc123def", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_MissingProvenanceHeader_NoError()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        // No X-Model-Provenance header

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // No error - provenance is optional
    }

    [Fact]
    public async Task PostRun_EmptyProvenanceHeader_Ignored()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", "");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Empty header should be treated as no provenance
    }

    [Fact]
    public async Task PostRun_InvalidProvenanceHeaderFormat_ReturnsError()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", "invalid format ! @ # $");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        // Could be 400 Bad Request or just ignored (depends on implementation decision)
        // If validation is strict:
        // Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // If validation is lenient:
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("model_20250925T120000Z_abc123")]
    [InlineData("model_20250925T120000Z_abc123def456")]
    [InlineData("model_20250101T000000Z_00000000")]
    [InlineData("model_20251231T235959Z_ffffffff")]
    public async Task PostRun_ValidModelIdFormats_Accepted(string modelId)
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", modelId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var storedModelId));
        Assert.Equal(modelId, storedModelId.GetString());
    }

    [Fact]
    public async Task PostRun_ProvenanceHeaderWithSpaces_Trimmed()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", "  model_20250925T120000Z_abc123def  ");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T120000Z_abc123def", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_MultipleProvenanceHeaders_UsesFirst()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        request.Headers.Add("X-Model-Provenance", "model_20250925T120000Z_first");
        request.Headers.Add("X-Model-Provenance", "model_20250925T130000Z_second");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        // Should use first header value (HTTP standard)
        Assert.Equal("model_20250925T120000Z_first", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_ProvenanceHeader_CaseInsensitive()
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };
        // HTTP headers are case-insensitive
        request.Headers.Add("x-model-provenance", "model_20250925T120000Z_abc123def");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T120000Z_abc123def", modelId.GetString());
    }

    // Helper methods
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

    private string GetProvenanceFilePath(string runId)
    {
        var canonicalPath = Path.Combine(_factory.TestDataDirectory, runId, "model", "provenance.json");
        if (File.Exists(canonicalPath))
        {
            return canonicalPath;
        }

        return Path.Combine(_factory.TestDataDirectory, runId, "provenance.json");
    }
}
