using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests embedded provenance in YAML model body.
/// Validates parsing, validation, and storage of provenance section.
/// </summary>
public class ProvenanceEmbeddedTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProvenanceEmbeddedTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRun_ValidEmbeddedProvenance_ParsedCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              model_id: model_20250925T120000Z_abc123def
              template_id: it-system-microservices
              template_version: "1.0"
              generated_at: "2025-09-25T12:00:00Z"
              generator: "flowtime-sim/0.4.0"
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("source", out var source));
        Assert.Equal("flowtime-sim", source.GetString());
        Assert.True(provenance.TryGetProperty("model_id", out var modelId));
        Assert.Equal("model_20250925T120000Z_abc123def", modelId.GetString());
        Assert.True(provenance.TryGetProperty("template_id", out var templateId));
        Assert.Equal("it-system-microservices", templateId.GetString());
        Assert.True(provenance.TryGetProperty("template_version", out var templateVersion));
        Assert.Equal("1.0", templateVersion.GetString());
    }

    [Fact]
    public async Task PostRun_EmbeddedProvenanceWithParameters_PreservesParameters()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              model_id: model_20250925T130000Z_def456ghi
              template_id: manufacturing-line
              parameters:
                bins: 12
                binSize: 1
                binUnit: hours
                productionRate: 150
                failureRate: 0.05
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("parameters", out var parameters));
        Assert.Equal(JsonValueKind.Object, parameters.ValueKind);
        
        Assert.True(parameters.TryGetProperty("productionRate", out var productionRate));
        Assert.Equal(150, productionRate.GetInt32());
        Assert.True(parameters.TryGetProperty("failureRate", out var failureRate));
        Assert.Equal(0.05, failureRate.GetDouble(), precision: 2);
    }

    [Fact]
    public async Task PostRun_MinimalEmbeddedProvenance_Accepted()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              model_id: model_20250925T140000Z_ghi789jkl
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("model_id", out var modelId));
        Assert.Equal("model_20250925T140000Z_ghi789jkl", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_EmptyProvenanceSection_Ignored()
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Empty provenance section should be treated as no provenance
    }

    [Fact]
    public async Task PostRun_InvalidProvenanceStructure_ReturnsError()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance: "invalid - should be object"
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("provenance", errorJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostRun_ProvenanceAtWrongLevel_ReturnsError()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
              provenance:
                model_id: model_20250925T150000Z_jkl012mno
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Provenance must be at root level, not nested in grid
    }

    [Fact]
    public async Task PostRun_EmbeddedProvenance_TimestampParsing()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              model_id: model_20250925T160000Z_mno345pqr
              generated_at: "2025-09-25T16:00:00Z"
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("generated_at", out var generatedAt));
        var timestamp = DateTime.Parse(generatedAt.GetString()!);
        Assert.Equal(new DateTime(2025, 9, 25, 16, 0, 0, DateTimeKind.Utc), timestamp);
    }

    [Theory]
    [InlineData("flowtime-sim")]
    [InlineData("manual")]
    [InlineData("ui-builder")]
    [InlineData("custom-tool-v2")]
    public async Task PostRun_VariousSourceValues_Accepted(string source)
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = $$"""
            schemaVersion: 1
            provenance:
              source: {{source}}
              model_id: model_20250925T170000Z_pqr678stu
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("source", out var storedSource));
        Assert.Equal(source, storedSource.GetString());
    }

    // Helper methods
    private async Task<string?> ExtractRunIdFromResponse(HttpResponseMessage response)
    {
        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);
        if (doc.TryGetProperty("run_id", out var runId))
        {
            return runId.GetString();
        }
        return null;
    }

    private string GetProvenanceFilePath(string runId)
    {
        return Path.Combine("/data", runId, "provenance.json");
    }
}
