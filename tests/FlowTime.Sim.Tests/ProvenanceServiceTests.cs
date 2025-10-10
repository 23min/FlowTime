using System.Text.Json;
using FlowTime.Sim.Core.Models;
using FlowTime.Sim.Core.Services;
using Xunit;

namespace FlowTime.Sim.Tests;

/// <summary>
/// TDD tests for provenance service (SIM-M2.7 Phase 1).
/// Tests written FIRST to define the contract before implementation.
/// </summary>
public class ProvenanceServiceTests
{
    private readonly IProvenanceService _service;

    public ProvenanceServiceTests()
    {
        _service = new ProvenanceService();
    }

    [Fact]
    public void ModelIdGeneration_IsUnique_ForDifferentTimestamps()
    {
        // Arrange
        var templateId = "it-system-microservices";
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["binSize"] = 1,
            ["binUnit"] = "hours"
        };

        // Act
        var provenance1 = _service.CreateProvenance(templateId, "1.0", "IT System", parameters);
        Thread.Sleep(1100); // Ensure different second in timestamp
        var provenance2 = _service.CreateProvenance(templateId, "1.0", "IT System", parameters);

        // Assert
        Assert.NotEqual(provenance1.ModelId, provenance2.ModelId);
        Assert.StartsWith("model_", provenance1.ModelId);
        Assert.StartsWith("model_", provenance2.ModelId);
    }

    [Fact]
    public void ModelIdGeneration_HashPortion_IsDeterministic()
    {
        // Arrange
        var templateId = "it-system-microservices";
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["binSize"] = 1,
            ["binUnit"] = "hours"
        };

        // Act - Generate multiple times with same inputs
        var provenance1 = _service.CreateProvenance(templateId, "1.0", "IT System", parameters);
        Thread.Sleep(10);
        var provenance2 = _service.CreateProvenance(templateId, "1.0", "IT System", parameters);

        // Extract hash portions (format: model_TIMESTAMP_HASH)
        var hash1 = provenance1.ModelId.Split('_')[2];
        var hash2 = provenance2.ModelId.Split('_')[2];

        // Assert - Hash portion should be same for same inputs
        Assert.Equal(hash1, hash2);
        Assert.Equal(8, hash1.Length); // 8 hex characters
    }

    [Fact]
    public void ModelIdGeneration_DifferentParameters_ProduceDifferentHashes()
    {
        // Arrange
        var templateId = "it-system-microservices";
        var params1 = new Dictionary<string, object> { ["bins"] = 12 };
        var params2 = new Dictionary<string, object> { ["bins"] = 24 };

        // Act
        var provenance1 = _service.CreateProvenance(templateId, "1.0", "IT System", params1);
        var provenance2 = _service.CreateProvenance(templateId, "1.0", "IT System", params2);

        // Extract hash portions
        var hash1 = provenance1.ModelId.Split('_')[2];
        var hash2 = provenance2.ModelId.Split('_')[2];

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CreateProvenance_IncludesAllRequiredFields()
    {
        // Arrange
        var templateId = "it-system-microservices";
        var templateVersion = "1.0";
        var templateTitle = "IT System - Microservices";
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["binSize"] = 1,
            ["binUnit"] = "hours",
            ["loadBalancerCapacity"] = 300
        };

        // Act
        var provenance = _service.CreateProvenance(templateId, templateVersion, templateTitle, parameters);

        // Assert - All required fields present
        Assert.Equal("flowtime-sim", provenance.Source);
        Assert.NotNull(provenance.ModelId);
        Assert.Equal(templateId, provenance.TemplateId);
        Assert.Equal(templateVersion, provenance.TemplateVersion);
        Assert.Equal(templateTitle, provenance.TemplateTitle);
        Assert.NotNull(provenance.Parameters);
        Assert.Equal(4, provenance.Parameters.Count);
        Assert.NotNull(provenance.GeneratedAt);
        Assert.NotNull(provenance.Generator);
        Assert.StartsWith("flowtime-sim/", provenance.Generator);
        Assert.Equal("1", provenance.SchemaVersion);
    }

    [Fact]
    public void CreateProvenance_GeneratedAt_IsIso8601Format()
    {
        // Arrange
        var templateId = "test-template";
        var parameters = new Dictionary<string, object>();

        // Act
        var provenance = _service.CreateProvenance(templateId, "1.0", "Test", parameters);

        // Assert - ISO8601 format with timezone
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", provenance.GeneratedAt);
    }

    [Fact]
    public void CreateProvenance_SerializesToValidJson()
    {
        // Arrange
        var templateId = "it-system-microservices";
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["binSize"] = 1.5,
            ["binUnit"] = "hours",
            ["nested"] = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var provenance = _service.CreateProvenance(templateId, "1.0", "IT System", parameters);
        var json = JsonSerializer.Serialize(provenance, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        // Assert - Valid JSON that can be deserialized
        Assert.NotNull(json);
        var deserialized = JsonSerializer.Deserialize<ProvenanceMetadata>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(provenance.ModelId, deserialized!.ModelId);
        Assert.Equal(provenance.TemplateId, deserialized.TemplateId);
    }

    [Fact]
    public void CreateProvenance_UsesInvariantCulture()
    {
        // Arrange - Use parameters with decimal values
        var parameters = new Dictionary<string, object>
        {
            ["rate"] = 3.14159,
            ["capacity"] = 1000.5
        };

        // Act
        var provenance = _service.CreateProvenance("test", "1.0", "Test", parameters);

        // Assert - Decimal formatting should be invariant (not culture-dependent)
        // This ensures hash determinism across different locales
        Assert.NotNull(provenance.ModelId);
        
        // Hash should be reproducible regardless of thread culture
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var provenance2 = _service.CreateProvenance("test", "1.0", "Test", parameters);
            
            // Hash portions should match (deterministic)
            var hash1 = provenance.ModelId.Split('_')[2];
            var hash2 = provenance2.ModelId.Split('_')[2];
            Assert.Equal(hash1, hash2);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void CreateProvenance_EmptyParameters_IsValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var provenance = _service.CreateProvenance("simple-template", "1.0", "Simple", parameters);

        // Assert
        Assert.NotNull(provenance);
        Assert.NotNull(provenance.ModelId);
        Assert.Empty(provenance.Parameters);
    }

    [Fact]
    public void CreateProvenance_NullParameters_ThrowsArgumentException()
    {
        // Arrange
        Dictionary<string, object>? parameters = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.CreateProvenance("template", "1.0", "Title", parameters!));
    }

    [Fact]
    public void ModelIdGeneration_Format_IsCorrect()
    {
        // Arrange & Act
        var provenance = _service.CreateProvenance("test", "1.0", "Test", new Dictionary<string, object>());

        // Assert - Format: model_YYYYMMDDTHHMMSSZ_<8hex>
        var parts = provenance.ModelId.Split('_');
        Assert.Equal(3, parts.Length);
        Assert.Equal("model", parts[0]);
        Assert.Matches(@"^\d{8}T\d{6}Z$", parts[1]); // Timestamp: YYYYMMDDTHHmmssZ
        Assert.Matches(@"^[0-9a-f]{8}$", parts[2]); // Hash: 8 lowercase hex chars
    }

    [Fact]
    public void CreateProvenance_IsFast()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["rate"] = 3.14
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Generate 100 provenance metadata objects
        for (int i = 0; i < 100; i++)
        {
            _service.CreateProvenance("test", "1.0", "Test", parameters);
        }
        stopwatch.Stop();

        // Assert - Should be very fast (< 100ms for 100 generations)
        Assert.True(stopwatch.ElapsedMilliseconds < 100, 
            $"Provenance generation too slow: {stopwatch.ElapsedMilliseconds}ms for 100 calls");
    }
}
