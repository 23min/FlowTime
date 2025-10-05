using System.IO;
using System.Text.Json;
using FlowTime.Contracts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Api.Tests;

public class FileSystemArtifactRegistryUnitTests
{
	[Fact]
	public async Task ScanRunDirectory_AddsMetadataTagsFromSpec()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var runId = "run_20250101T010101Z_test";
			var runDir = Path.Combine(tempRoot, runId);
			Directory.CreateDirectory(runDir);

			await File.WriteAllTextAsync(Path.Combine(runDir, "manifest.json"), "{ \"schemaVersion\": 1 }");
			var specContent = """
metadata:
  title: 'Tagged Run'
  description: 'Demo run with metadata tags'
  templateId: 'transportation-basic'
  tags: [demo, featured]
schemaVersion: 1
nodes:
  - id: arrivals
    kind: const
    values: [1, 2, 3]
""";
			await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), specContent);
			await File.WriteAllTextAsync(Path.Combine(runDir, "output.csv"), "t,value\n0,1\n");

			var registry = CreateRegistry(tempRoot);
			var artifact = await registry.ScanRunDirectoryAsync(runDir);

			Assert.NotNull(artifact);
			Assert.Contains("demo", artifact!.Tags);
			Assert.Contains("featured", artifact.Tags);
			Assert.Equal("Tagged Run", artifact.Title);
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

	[Fact]
	public async Task GetArtifactsAsync_ExcludesArchivedByDefault_IncludesWhenRequested()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var registry = CreateRegistry(tempRoot);
			var indexPath = Path.Combine(tempRoot, "registry-index.json");

			var archivedArtifact = new Artifact
			{
				Id = "run_archived",
				Type = "run",
				Title = "Archived Run",
				Created = DateTime.UtcNow,
				Tags = new[] { "run", "archived" },
				Metadata = new(),
				Files = Array.Empty<string>(),
				TotalSize = 0,
				LastModified = DateTime.UtcNow
			};

			var activeArtifact = new Artifact
			{
				Id = "run_active",
				Type = "run",
				Title = "Active Run",
				Created = DateTime.UtcNow,
				Tags = new[] { "run" },
				Metadata = new(),
				Files = Array.Empty<string>(),
				TotalSize = 0,
				LastModified = DateTime.UtcNow
			};

			var index = new RegistryIndex
			{
				SchemaVersion = 1,
				LastUpdated = DateTime.UtcNow,
				Artifacts = new List<Artifact> { archivedArtifact, activeArtifact },
				ArtifactCount = 2
			};

			await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));

			// Act & Assert: default should exclude archived entry
			var defaultList = await registry.GetArtifactsAsync();
			Assert.Single(defaultList.Artifacts);
			Assert.Equal("run_active", defaultList.Artifacts[0].Id);

			var includeArchived = await registry.GetArtifactsAsync(new ArtifactQueryOptions { IncludeArchived = true });
			Assert.Equal(2, includeArchived.Artifacts.Count);
			Assert.Contains(includeArchived.Artifacts, a => a.Id == "run_archived");
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

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
		var path = Path.Combine(Path.GetTempPath(), $"flowtime_registry_tests_{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
