using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FlowTime.Core.Configuration;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Tests that provenance metadata is correctly stored in run artifacts.
/// Validates provenance.json creation, structure, and manifest.json reference.
/// </summary>
public class ProvenanceStorageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProvenanceStorageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRun_WithProvenanceHeader_CreatesProvenanceJson()
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

        var runId = await ExtractRunIdFromResponse(response);
        Assert.NotNull(runId);

        // Verify provenance.json exists
        var provenancePath = GetProvenanceFilePath(runId);
        Assert.True(File.Exists(provenancePath), "provenance.json should exist");

        // Verify provenance.json structure
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("model_id", out var modelId));
        Assert.Equal("model_20250925T120000Z_abc123def", modelId.GetString());
    }

    [Fact]
    public async Task PostRun_WithEmbeddedProvenance_CreatesProvenanceJson()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              model_id: model_20250925T130000Z_def456ghi
              template_id: it-system-microservices
              template_version: "1.0"
              generated_at: "2025-09-25T13:00:00Z"
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
        Assert.NotNull(runId);

        // Verify provenance.json exists
        var provenancePath = GetProvenanceFilePath(runId);
        Assert.True(File.Exists(provenancePath), "provenance.json should exist");

        // Verify provenance.json structure
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        Assert.True(provenance.TryGetProperty("source", out var source));
        Assert.Equal("flowtime-sim", source.GetString());
        Assert.True(provenance.TryGetProperty("model_id", out var modelId));
        Assert.Equal("model_20250925T130000Z_def456ghi", modelId.GetString());
        Assert.True(provenance.TryGetProperty("template_id", out var templateId));
        Assert.Equal("it-system-microservices", templateId.GetString());
    }

    [Fact]
    public async Task PostRun_WithoutProvenance_NoProvenanceJson()
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

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        Assert.NotNull(runId);

        // Verify provenance.json does NOT exist (or is empty/null)
        var provenancePath = GetProvenanceFilePath(runId!);
        if (File.Exists(provenancePath))
        {
            var provenanceJson = await File.ReadAllTextAsync(provenancePath);
            // Should be empty object or null
            var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);
            Assert.True(provenance.ValueKind == JsonValueKind.Null ||
                       (provenance.ValueKind == JsonValueKind.Object && provenance.EnumerateObject().Count() == 0));
        }
        // Else: file doesn't exist, which is also acceptable
    }

    [Fact]
    public async Task PostRun_WithProvenance_ManifestReferencesProvenance()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              model_id: model_20250925T140000Z_ghi789jkl
              template_id: manufacturing-line
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
        Assert.NotNull(runId);

        // Verify manifest.json includes provenance reference
        var manifestPath = GetManifestFilePath(runId);
        Assert.True(File.Exists(manifestPath), "manifest.json should exist");

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<JsonElement>(manifestJson);

        Assert.True(manifest.TryGetProperty("provenance", out var provenanceRef));
        Assert.True(provenanceRef.TryGetProperty("has_provenance", out var hasProvenance));
        Assert.True(hasProvenance.GetBoolean());
        Assert.True(provenanceRef.TryGetProperty("model_id", out var modelId));
        Assert.Equal("model_20250925T140000Z_ghi789jkl", modelId.GetString());
        Assert.True(provenanceRef.TryGetProperty("template_id", out var templateId));
        Assert.Equal("manufacturing-line", templateId.GetString());
    }

    [Fact]
    public async Task PostRun_EmbeddedProvenance_StrippedFromSpecYaml()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              model_id: model_20250925T150000Z_jkl012mno
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
        Assert.NotNull(runId);

        // Verify spec.yaml does NOT contain provenance section
        var specPath = GetSpecFilePath(runId);
        Assert.True(File.Exists(specPath), "spec.yaml should exist");

        var specYaml = await File.ReadAllTextAsync(specPath);
        Assert.DoesNotContain("provenance:", specYaml);
        Assert.DoesNotContain("model_id:", specYaml);
        Assert.DoesNotContain("flowtime-sim", specYaml);

        // Verify spec.yaml still has valid execution spec
        Assert.Contains("schemaVersion:", specYaml);
        Assert.Contains("grid:", specYaml);
        Assert.Contains("nodes:", specYaml);
    }

    [Fact]
    public async Task PostRun_ProvenanceJson_HasReceivedAtTimestamp()
    {
        // Arrange
        var client = _factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
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
        var beforeRequest = DateTime.UtcNow;
        var response = await client.SendAsync(request);
        var afterRequest = DateTime.UtcNow;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runId = await ExtractRunIdFromResponse(response);
        var provenancePath = GetProvenanceFilePath(runId!);
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonSerializer.Deserialize<JsonElement>(provenanceJson);

        // Should have received_at timestamp
        Assert.True(provenance.TryGetProperty("received_at", out var receivedAt));
        var receivedAtTime = DateTime.Parse(receivedAt.GetString()!);

        // Verify it's within reasonable time window
        Assert.True(receivedAtTime >= beforeRequest);
        Assert.True(receivedAtTime <= afterRequest.AddSeconds(5)); // Allow 5 second buffer
    }

    // Helper methods (these would need to be implemented based on actual API structure)
    private async Task<string?> ExtractRunIdFromResponse(HttpResponseMessage response)
    {
        // TODO: Extract run_id from response JSON
        // This depends on actual API response structure
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
        var dataDir = DirectoryProvider.GetDefaultDataDirectory();
        return Path.Combine(dataDir, runId, "provenance.json");
    }

    private string GetManifestFilePath(string runId)
    {
        var dataDir = DirectoryProvider.GetDefaultDataDirectory();
        return Path.Combine(dataDir, runId, "manifest.json");
    }

    private string GetSpecFilePath(string runId)
    {
        var dataDir = DirectoryProvider.GetDefaultDataDirectory();
        return Path.Combine(dataDir, runId, "spec.yaml");
    }
}
