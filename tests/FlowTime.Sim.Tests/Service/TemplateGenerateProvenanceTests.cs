using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowTime.Sim.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Sim.Tests.Service;

/// <summary>
/// TDD tests for /api/v1/templates/{id}/generate provenance enhancement (SIM-M2.7 Phase 2).
/// Tests written FIRST to define the API contract before implementation.
/// </summary>
public class TemplateGenerateProvenanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TemplateGenerateProvenanceTests(WebApplicationFactory<Program> factory)
    {
        // Set up test environment BEFORE creating client
        var testDataDir = Path.Combine(Path.GetTempPath(), "flow-sim-provenance-tests", Guid.NewGuid().ToString("N"));
        var templatesDir = Path.Combine(testDataDir, "templates");
        Directory.CreateDirectory(templatesDir);
        
        // Set environment variables BEFORE WebApplicationFactory is configured
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", testDataDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", templatesDir);
        
        // NOW create the factory and client after environment is configured
        _factory = factory.WithWebHostBuilder(builder => { });
        _client = _factory.CreateClient();
        
        // Create a simple test template
        var testTemplateYaml = @"schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-template
  title: Test Template
  description: Simple test template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: bins
    type: integer
    description: Number of bins
    default: 10
    min: 1
    max: 100
  - name: binSize
    type: integer
    description: Size of each bin
    default: 1
grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes
topology:
  nodes:
    - id: TestService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 100, 100]
  - id: served
    kind: expr
    expr: ""arrivals""
outputs:
  - series: ""*""
";
        File.WriteAllText(Path.Combine(templatesDir, "test-template.yaml"), testTemplateYaml);
    }

    [Fact]
    public async Task Generate_ReturnsModelAndProvenance_Separately()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseBody);
        
        // Should have model, provenance, and metadata fields
        Assert.True(json.RootElement.TryGetProperty("model", out var modelElement));
        Assert.True(json.RootElement.TryGetProperty("provenance", out var provenanceElement));
        Assert.True(json.RootElement.TryGetProperty("metadata", out var metadataElement));
        
        // Model should be non-empty YAML string
        var modelYaml = modelElement.GetString();
        Assert.NotNull(modelYaml);
        Assert.Contains("schemaVersion:", modelYaml);
        
        // Provenance should have all required fields
        Assert.True(provenanceElement.TryGetProperty("source", out _));
        Assert.True(provenanceElement.TryGetProperty("modelId", out _));
        Assert.True(provenanceElement.TryGetProperty("templateId", out _));
        Assert.True(provenanceElement.TryGetProperty("templateVersion", out _));
        Assert.True(provenanceElement.TryGetProperty("parameters", out _));
        Assert.True(provenanceElement.TryGetProperty("generatedAt", out _));
        Assert.True(provenanceElement.TryGetProperty("generator", out _));
        Assert.True(provenanceElement.TryGetProperty("schemaVersion", out _));
        
        Assert.Equal("flowtime-sim", provenanceElement.GetProperty("source").GetString());
        Assert.Equal("test-template", provenanceElement.GetProperty("templateId").GetString());
        Assert.Matches(@"[a-f0-9]{64}", provenanceElement.GetProperty("modelId").GetString());

        // Metadata summary should surface key attributes
        Assert.Equal("test-template", metadataElement.GetProperty("templateId").GetString());
        Assert.Equal("Test Template", metadataElement.GetProperty("templateTitle").GetString());
        Assert.Equal("1.0.0", metadataElement.GetProperty("templateVersion").GetString());
        Assert.Equal(1, metadataElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("simulation", metadataElement.GetProperty("mode").GetString());
        Assert.True(metadataElement.GetProperty("hasWindow").GetBoolean());
        Assert.True(metadataElement.GetProperty("hasTopology").GetBoolean());
        Assert.False(metadataElement.GetProperty("hasTelemetrySources").GetBoolean());
        Assert.StartsWith("sha256:", metadataElement.GetProperty("modelHash").GetString());
    }

    [Fact]
    public async Task Generate_WithEmbedProvenanceTrue_ReturnsEmbeddedModel()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act - Include ?embed_provenance=true query parameter
        var response = await _client.PostAsync(
            "/api/v1/templates/test-template/generate?embed_provenance=true", 
            content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseBody);
        
        // Should still have both model and provenance for consistency
        Assert.True(json.RootElement.TryGetProperty("model", out var modelElement));
        Assert.True(json.RootElement.TryGetProperty("provenance", out _));
        Assert.True(json.RootElement.TryGetProperty("metadata", out _));
        
        var modelYaml = modelElement.GetString();
        Assert.NotNull(modelYaml);
        
        // Model YAML should contain embedded provenance section
        Assert.Contains("provenance:", modelYaml);
        Assert.Contains("source: flowtime-sim", modelYaml);
        Assert.Matches(@"modelId: [a-f0-9]{64}", modelYaml);
        Assert.Contains("templateId: test-template", modelYaml);
        
        // Provenance should appear AFTER schemaVersion but BEFORE grid/nodes
        var schemaVersionIndex = modelYaml.IndexOf("schemaVersion:");
        var provenanceIndex = modelYaml.IndexOf("provenance:");
        var outputsIndex = modelYaml.IndexOf("outputs:");

        Assert.True(schemaVersionIndex >= 0);
        Assert.True(provenanceIndex > schemaVersionIndex);
        Assert.True(outputsIndex >= 0);
        Assert.True(provenanceIndex > outputsIndex);
    }

    [Fact]
    public async Task Generate_WithEmbedProvenanceFalse_ReturnsSeparateProvenance()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act - Explicitly set embed_provenance=false
        var response = await _client.PostAsync(
            "/api/v1/templates/test-template/generate?embed_provenance=false", 
            content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseBody);
        
        var modelYaml = json.RootElement.GetProperty("model").GetString();
        Assert.NotNull(modelYaml);
        
        // Model YAML includes embedded provenance by default for observability
        Assert.Contains("provenance:", modelYaml);
        
        // Provenance remains available in the separate field for backward compatibility
        Assert.True(json.RootElement.TryGetProperty("provenance", out _));
        Assert.True(json.RootElement.TryGetProperty("metadata", out _));
    }

    [Fact]
    public async Task Generate_WithModeOverride_UsesTelemetryMode()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate?mode=telemetry", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = json.RootElement.GetProperty("metadata");

        Assert.Equal("telemetry", metadata.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Generate_WithInvalidMode_ReturnsBadRequest()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate?mode=invalid", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_ProvenanceModelId_IsDeterministic()
    {
        // Arrange
        var parameters = new { bins = 12, binSize = 1 };
        var content1 = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");
        var content2 = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act - Generate twice with same parameters, ensure different timestamps
        var response1 = await _client.PostAsync("/api/v1/templates/test-template/generate", content1);
        await Task.Delay(1100); // Ensure timestamp differs (second precision)
        var response2 = await _client.PostAsync("/api/v1/templates/test-template/generate", content2);

        // Assert
        var json1 = JsonDocument.Parse(await response1.Content.ReadAsStringAsync());
        var json2 = JsonDocument.Parse(await response2.Content.ReadAsStringAsync());
        
        var modelId1 = json1.RootElement.GetProperty("provenance").GetProperty("modelId").GetString();
        var modelId2 = json2.RootElement.GetProperty("provenance").GetProperty("modelId").GetString();
        
        Assert.NotNull(modelId1);
        Assert.NotNull(modelId2);
        
        // Model IDs should be deterministic for identical inputs
        Assert.Equal(modelId1, modelId2);
        Assert.Matches(@"[a-f0-9]{64}", modelId1!);
    }

    [Fact]
    public async Task Generate_ProvenanceIncludesAllParameters()
    {
        // Arrange
        var parameters = new { bins = 24, binSize = 2 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate", content);

        // Assert
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provenanceParams = json.RootElement.GetProperty("provenance").GetProperty("parameters");
        
        Assert.Equal(24, GetInt32(provenanceParams.GetProperty("bins")));
        Assert.Equal(2, GetInt32(provenanceParams.GetProperty("binSize")));
    }

    [Fact]
    public async Task Generate_InvalidTemplate_Returns404()
    {
        // Arrange
        var parameters = new { bins = 12 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync(
            "/api/v1/templates/non-existent-template/generate", 
            content);

        // Assert - Negative test case (per copilot instructions)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_EmptyParameters_ReturnsProvenanceWithEmptyParams()
    {
        // Arrange - No parameters
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provenanceParams = json.RootElement.GetProperty("provenance").GetProperty("parameters");
        
        // Should have empty parameters object
        Assert.Equal(JsonValueKind.Object, provenanceParams.ValueKind);
    }

    [Fact]
    public async Task Generate_ProvenanceTimestamp_IsIso8601Utc()
    {
        // Arrange
        var parameters = new { bins = 12 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate", content);

        // Assert
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var timestamp = json.RootElement.GetProperty("provenance").GetProperty("generatedAt").GetString();
        
        Assert.NotNull(timestamp);
        Assert.True(DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
    }

    [Fact]
    public async Task Generate_BackwardCompatible_ExistingClientsWork()
    {
        // Arrange - Old-style request expecting just YAML back
        var parameters = new { bins = 12 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/templates/test-template/generate", content);

        // Assert - Should still return 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Response should be valid JSON (new format)
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(IsValidJson(responseBody));
        
        // Old clients can ignore provenance field if they want
        var json = JsonDocument.Parse(responseBody);
        Assert.True(json.RootElement.TryGetProperty("model", out _));
    }

    [Fact]
    public async Task Generate_EmbeddedProvenance_ValidYamlFormat()
    {
        // Arrange
        var parameters = new { bins = 12 };
        var content = new StringContent(
            JsonSerializer.Serialize(parameters),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync(
            "/api/v1/templates/test-template/generate?embed_provenance=true", 
            content);

        // Assert
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var modelYaml = json.RootElement.GetProperty("model").GetString();
        
        Assert.NotNull(modelYaml);
        
        // Should be valid YAML (basic check - no parse errors when split by lines)
        var lines = modelYaml!.Split('\n');
        Assert.NotEmpty(lines);
        
        // Provenance section should be properly indented
        var provenanceLine = lines.FirstOrDefault(l => l.Contains("provenance:"));
        Assert.NotNull(provenanceLine);
        Assert.False(provenanceLine!.StartsWith(" ")); // No leading whitespace for top-level key
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetInt32(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetInt32(),
        JsonValueKind.String => int.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Unsupported JSON value kind {element.ValueKind} for numeric conversion")
    };
}
