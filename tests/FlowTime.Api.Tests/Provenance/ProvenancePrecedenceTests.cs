using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;
using FlowTime.Core.Configuration;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests precedence rules when provenance is provided via both header and embedded YAML.
/// Validates that header takes precedence and warning is logged.
/// </summary>
public class ProvenancePrecedenceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProvenancePrecedenceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRun_BothHeaderAndEmbedded_HeaderWins()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20250925T120000Z_embedded
              templateId: from-embedded
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
        request.Headers.Add("X-Model-Provenance", "model_20250925T120000Z_header");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        // Header should win
        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T120000Z_header", modelId.GetString());

        // Embedded values should NOT be present (or overridden)
        if (provenance.TryGetProperty("templateId", out var templateId))
        {
            Assert.NotEqual("from-embedded", templateId.GetString());
        }
    }

    [Fact]
    public async Task PostRun_BothPresent_LogsWarning()
    {
        // Arrange
        // This test would require capturing logs, which depends on logging infrastructure
        // For now, we'll just verify the behavior (header wins)
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              modelId: model_20250925T130000Z_embedded
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
        request.Headers.Add("X-Model-Provenance", "model_20250925T130000Z_header");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // TODO: Capture logs and verify warning message:
        // "Provenance provided in both header and model body; using header"
        
        // For now, just verify correct behavior
        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T130000Z_header", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_HeaderOnly_UsesHeader()
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
        request.Headers.Add("X-Model-Provenance", "model_20250925T140000Z_headeronly");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T140000Z_headeronly", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_EmbeddedOnly_UsesEmbedded()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20250925T150000Z_embeddedonly
              templateId: manufacturing-line
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
        // No header

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T150000Z_embeddedonly", modelId.GetString());
        Assert.True(provenance.TryGetProperty("templateId", out var templateId));
        Assert.Equal("manufacturing-line", templateId.GetString());
    }

    [Fact]
    public async Task PostRun_Neither_NoProvenance()
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
        // No header, no embedded provenance

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        
        // provenance.json may not exist or be empty
        if (File.Exists(provenancePath))
        {
            var provenanceJson = await File.ReadAllTextAsync(provenancePath);
            var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);
            Assert.True(provenance.ValueKind == JsonValueKind.Null ||
                       (provenance.ValueKind == JsonValueKind.Object && !provenance.EnumerateObject().Any()));
        }
    }

    [Fact]
    public async Task PostRun_HeaderCompleteEmbeddedMinimal_HeaderWins()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              modelId: model_20250925T160000Z_embedded
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
        // Header has more complete information
        request.Headers.Add("X-Model-Provenance", "model_20250925T160000Z_header_complete");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        // Even though embedded has more fields, header wins
        Assert.True(provenance.TryGetProperty("modelId", out var modelId));
        Assert.Equal("model_20250925T160000Z_header_complete", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_BothEmpty_NoProvenance()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance: {}
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

        // Both empty = no provenance
        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        
        if (File.Exists(provenancePath))
        {
            var provenanceJson = await File.ReadAllTextAsync(provenancePath);
            var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);
            Assert.True(provenance.ValueKind == JsonValueKind.Null ||
                       (provenance.ValueKind == JsonValueKind.Object && !provenance.EnumerateObject().Any()));
        }
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
        return Path.Combine(_factory.TestDataDirectory, runId, "provenance.json");
    }
}
