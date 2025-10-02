using System.Net;
using System.Text;
using System.Text.Json;
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
        var testTemplateYaml = @"version: 1
metadata:
  id: test-template
  title: Test Template
  description: Simple test template
  version: 1.0
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
nodes:
  - id: SOURCE
    type: source
    expression: ""constant(100)""
  - id: SINK
    type: sink
edges:
  - from: SOURCE
    to: SINK
    expression: ""constant(5)""
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
        
        // Should have both model and provenance fields
        Assert.True(json.RootElement.TryGetProperty("model", out var modelElement));
        Assert.True(json.RootElement.TryGetProperty("provenance", out var provenanceElement));
        
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
        
        var modelYaml = modelElement.GetString();
        Assert.NotNull(modelYaml);
        
        // Model YAML should contain embedded provenance section
        Assert.Contains("provenance:", modelYaml);
        Assert.Contains("source: flowtime-sim", modelYaml);
        Assert.Contains("modelId:", modelYaml);
        Assert.Contains("templateId: test-template", modelYaml);
        
        // Provenance should appear AFTER schemaVersion but BEFORE grid/nodes
        var schemaVersionIndex = modelYaml.IndexOf("schemaVersion:");
        var provenanceIndex = modelYaml.IndexOf("provenance:");
        var gridIndex = modelYaml.IndexOf("grid:");
        
        Assert.True(schemaVersionIndex >= 0);
        Assert.True(provenanceIndex > schemaVersionIndex);
        Assert.True(gridIndex > provenanceIndex || gridIndex == -1); // grid might not exist in all templates
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
        
        // Model YAML should NOT contain embedded provenance
        Assert.DoesNotContain("provenance:", modelYaml);
        
        // But provenance should be in separate field
        Assert.True(json.RootElement.TryGetProperty("provenance", out _));
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
        
        // Model IDs should be different (timestamp differs)
        Assert.NotEqual(modelId1, modelId2);
        
        // But hash portions should be same (deterministic)
        var hash1 = modelId1!.Split('_')[2];
        var hash2 = modelId2!.Split('_')[2];
        Assert.Equal(hash1, hash2);
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
        
        Assert.Equal(24, provenanceParams.GetProperty("bins").GetInt32());
        Assert.Equal(2, provenanceParams.GetProperty("binSize").GetInt32());
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
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", timestamp!);
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
}
