using System.Text.Json;
using FlowTime.Contracts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Api.Tests.Registry;

/// <summary>
/// Tests for provenance-based filtering in the FileSystemArtifactRegistry.
/// Validates that templateId and modelId filters work correctly.
/// </summary>
public class ProvenanceFilterTests
{
    [Fact]
    public async Task Registry_ExtractsProvenanceFromManifest()
    {
        // Arrange: Create test run directory with manifest.json containing provenance
        var tempRoot = CreateTempDirectory();
        try
        {
            var runId = "run_20250101T010101Z_test";
            var runDir = Path.Combine(tempRoot, runId);
            Directory.CreateDirectory(runDir);

            // Create manifest with provenance reference
            var manifest = new
            {
                schemaVersion = 1,
                runId = runId,
                provenance = new
                {
                    hasProvenance = true,
                    modelId = "model_123",
                    source = "flowtime-sim"
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(runDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true })
            );

            // Create minimal spec.yaml
            await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), "schemaVersion: 1\nnodes:\n  - id: test\n    kind: const\n    values: [1]");

            // Create provenance.json
            var provenance = new
            {
                source = "flowtime-sim",
                modelId = "model_123",
                templateId = "transportation-basic"
            };
            await File.WriteAllTextAsync(
                Path.Combine(runDir, "provenance.json"),
                JsonSerializer.Serialize(provenance, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true })
            );

            var registry = CreateRegistry(tempRoot);

            // Act: Scan the run directory
            var artifact = await registry.ScanRunDirectoryAsync(runDir);

            // Assert: Provenance metadata should be extracted
            Assert.NotNull(artifact);
            Assert.True(artifact!.Metadata.ContainsKey("provenance"));
            
            var provenanceMetadata = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(artifact.Metadata["provenance"])
            );
            Assert.True(provenanceMetadata.TryGetProperty("hasProvenance", out var hasProvenance));
            Assert.True(hasProvenance.GetBoolean());
            Assert.Equal("model_123", provenanceMetadata.GetProperty("modelId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Registry_IndexesProvenanceFields()
    {
        // Arrange: Create run with provenance
        var tempRoot = CreateTempDirectory();
        try
        {
            var runId = "run_20250101T010101Z_test";
            var runDir = Path.Combine(tempRoot, runId);
            Directory.CreateDirectory(runDir);

            // Create manifest with provenance
            var manifest = new
            {
                schemaVersion = 1,
                runId = runId,
                provenance = new
                {
                    hasProvenance = true,
                    modelId = "model_123",
                    templateId = "transportation-basic",
                    source = "flowtime-sim"
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(runDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true })
            );

            await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), "schemaVersion: 1\nnodes:\n  - id: test\n    kind: const\n    values: [1]");

            var registry = CreateRegistry(tempRoot);

            // Act: Rebuild registry index
            var index = await registry.RebuildIndexAsync();

            // Assert: Registry index should contain provenance metadata
            Assert.NotNull(index);
            Assert.Single(index.Artifacts);
            
            var artifact = index.Artifacts.First();
            Assert.True(artifact.Metadata.ContainsKey("provenance"));
            
            var provenanceMetadata = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(artifact.Metadata["provenance"])
            );
            Assert.True(provenanceMetadata.GetProperty("hasProvenance").GetBoolean());
            Assert.Equal("model_123", provenanceMetadata.GetProperty("modelId").GetString());
            Assert.Equal("transportation-basic", provenanceMetadata.GetProperty("templateId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetArtifacts_FiltersByTemplateId()
    {
        // Arrange: Create registry with artifacts
        var tempRoot = CreateTempDirectory();
        try
        {
            var registry = CreateRegistry(tempRoot);
            var indexPath = Path.Combine(tempRoot, "registry-index.json");

            var artifact1 = CreateTestArtifact("run_1", "transportation-basic", "model_123");
            var artifact2 = CreateTestArtifact("run_2", "manufacturing-line", "model_456");
            var artifact3 = CreateTestArtifact("run_3", "transportation-basic", "model_789");

            var index = new RegistryIndex
            {
                SchemaVersion = 1,
                LastUpdated = DateTime.UtcNow,
                Artifacts = new List<Artifact> { artifact1, artifact2, artifact3 },
                ArtifactCount = 3
            };

            await File.WriteAllTextAsync(
                indexPath,
                JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true })
            );

            // Act: Query with templateId filter
            var result = await registry.GetArtifactsAsync(new ArtifactQueryOptions 
            { 
                TemplateId = "transportation-basic" 
            });

            // Assert: Should return only matching artifacts
            Assert.Equal(2, result.Artifacts.Count);
            Assert.All(result.Artifacts, artifact =>
            {
                Assert.True(artifact.Metadata.ContainsKey("provenance"));
                var provenanceMetadata = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(artifact.Metadata["provenance"])
                );
                Assert.Equal("transportation-basic", provenanceMetadata.GetProperty("templateId").GetString());
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetArtifacts_WithTemplateIdFilter_ExcludesNullProvenance()
    {
        // Arrange: Mix of artifacts with and without provenance
        var tempRoot = CreateTempDirectory();
        try
        {
            var registry = CreateRegistry(tempRoot);
            var indexPath = Path.Combine(tempRoot, "registry-index.json");

            var artifactWithProvenance = CreateTestArtifact("run_1", "transportation-basic", "model_123");
            var artifactWithoutProvenance = new Artifact
            {
                Id = "run_2",
                Type = "run",
                Title = "Run Without Provenance",
                Created = DateTime.UtcNow,
                Tags = new[] { "run" },
                Metadata = new Dictionary<string, object>(), // No provenance
                Files = Array.Empty<string>(),
                TotalSize = 0,
                LastModified = DateTime.UtcNow
            };
            var artifactWithMalformedProvenance = new Artifact
            {
                Id = "run_3",
                Type = "run",
                Title = "Run With Malformed Provenance",
                Created = DateTime.UtcNow,
                Tags = new[] { "run" },
                Metadata = new Dictionary<string, object>
                {
                    ["provenance"] = new { hasProvenance = true, modelId = "model_456" } // Missing templateId
                },
                Files = Array.Empty<string>(),
                TotalSize = 0,
                LastModified = DateTime.UtcNow
            };

            var index = new RegistryIndex
            {
                SchemaVersion = 1,
                LastUpdated = DateTime.UtcNow,
                Artifacts = new List<Artifact> { artifactWithProvenance, artifactWithoutProvenance, artifactWithMalformedProvenance },
                ArtifactCount = 3
            };

            await File.WriteAllTextAsync(
                indexPath,
                JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true })
            );

            // Act: Query with templateId filter
            var result = await registry.GetArtifactsAsync(new ArtifactQueryOptions 
            { 
                TemplateId = "transportation-basic" 
            });

            // Assert: Should return only artifact with matching provenance
            // Artifacts with null/missing provenance should be excluded gracefully
            Assert.Single(result.Artifacts);
            Assert.Equal("run_1", result.Artifacts[0].Id);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    // Helper methods

    private static FileSystemArtifactRegistry CreateRegistry(string dataDirectory)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = dataDirectory
            })
            .Build();

        return new FileSystemArtifactRegistry(config, NullLogger<FileSystemArtifactRegistry>.Instance);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowtime_provenance_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static Artifact CreateTestArtifact(string id, string templateId, string modelId)
    {
        return new Artifact
        {
            Id = id,
            Type = "run",
            Title = $"Test Run {id}",
            Created = DateTime.UtcNow,
            Tags = new[] { "run", "test" },
            Metadata = new Dictionary<string, object>
            {
                ["provenance"] = new
                {
                    hasProvenance = true,
                    modelId = modelId,
                    templateId = templateId,
                    source = "flowtime-sim"
                }
            },
            Files = Array.Empty<string>(),
            TotalSize = 0,
            LastModified = DateTime.UtcNow
        };
    }
}
