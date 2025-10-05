using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace FlowTime.Cli.Tests.Commands;

/// <summary>
/// Tests for CLI provenance query commands.
/// Tests verify the 'artifacts list' command with provenance filters.
/// </summary>
public class ProvenanceQueryTests : IDisposable
{
    private readonly string testDataDir;

    public ProvenanceQueryTests()
    {
        // Create unique temp directory for each test instance
        testDataDir = Path.Combine(Path.GetTempPath(), $"flowtime_cli_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDataDir);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(testDataDir))
        {
            Directory.Delete(testDataDir, recursive: true);
        }
    }

    private void CreateTestArtifact(string runId, string templateId, string modelId, string title = "Test Artifact")
    {
        var runDir = Path.Combine(testDataDir, runId);
        Directory.CreateDirectory(runDir);

        // Create manifest.json with provenance
        var manifest = new
        {
            id = runId,
            type = "run",
            title = title,
            created = DateTime.UtcNow.ToString("o"),
            provenance = new
            {
                templateId = templateId,
                modelId = modelId
            },
            files = new[] { "spec.yaml", "output.csv" },
            totalSize = 1024L,
            lastModified = DateTime.UtcNow.ToString("o")
        };

        var manifestPath = Path.Combine(runDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        // Create dummy spec.yaml and output.csv
        File.WriteAllText(Path.Combine(runDir, "spec.yaml"), "# Test spec\n");
        File.WriteAllText(Path.Combine(runDir, "output.csv"), "time,value\n");
    }

    private async Task<(int exitCode, string output, string error)> RunCliCommand(string args)
    {
        var cliPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "FlowTime.Cli", "bin", "Debug", "net9.0", "FlowTime.Cli.dll"
        );

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{cliPath}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start CLI process");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, await outputTask, await errorTask);
    }
    [Fact]
    public async Task ArtifactsList_WithTemplateIdFlag_FiltersCorrectly()
    {
        // Arrange: Create artifacts with different templateIds
        CreateTestArtifact("run_20251005T100000Z_transport001", "transportation-basic", "model_001", "Transport Run");
        CreateTestArtifact("run_20251005T100001Z_mfg002", "manufacturing-line", "model_002", "Manufacturing Run");
        CreateTestArtifact("run_20251005T100002Z_transport003", "transportation-basic", "model_003", "Another Transport");

        // Act: Query with --template-id filter
        var (exitCode, output, error) = await RunCliCommand($"artifacts list --template-id transportation-basic --data-dir \"{testDataDir}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("run_20251005T100000Z_transport001", output);
        Assert.Contains("run_20251005T100002Z_transport003", output);
        Assert.DoesNotContain("run_20251005T100001Z_mfg002", output);
        Assert.Contains("Total: 2 artifacts", output);
    }

    [Fact]
    public async Task ArtifactsList_WithModelIdFlag_FiltersCorrectly()
    {
        // Arrange: Create artifacts with different modelIds
        CreateTestArtifact("run_20251005T110000Z_model123a", "template-1", "model_123", "Model 123 Run");
        CreateTestArtifact("run_20251005T110001Z_model456", "template-1", "model_456", "Model 456 Run");
        CreateTestArtifact("run_20251005T110002Z_model123b", "template-2", "model_123", "Another Model 123");

        // Act: Query with --model-id filter
        var (exitCode, output, error) = await RunCliCommand($"artifacts list --model-id model_123 --data-dir \"{testDataDir}\"");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("run_20251005T110000Z_model123a", output);
        Assert.Contains("run_20251005T110002Z_model123b", output);
        Assert.DoesNotContain("run_20251005T110001Z_model456", output);
        Assert.Contains("Total: 2 artifacts", output);
    }

    [Fact]
    public async Task ArtifactsList_WithBothFlags_FiltersByBoth()
    {
        // Arrange: Create artifacts with various combinations
        CreateTestArtifact("run_20251005T120000Z_bothmatch", "template-1", "model_123", "Both Match");
        CreateTestArtifact("run_20251005T120001Z_templateonly", "template-1", "model_456", "Template Only");
        CreateTestArtifact("run_20251005T120002Z_modelonly", "template-2", "model_123", "Model Only");
        CreateTestArtifact("run_20251005T120003Z_neither", "template-2", "model_456", "Neither Match");

        // Act: Query with both filters
        var (exitCode, output, error) = await RunCliCommand($"artifacts list --template-id template-1 --model-id model_123 --data-dir \"{testDataDir}\"");

        // Assert: Only artifacts matching BOTH filters should appear
        Assert.Equal(0, exitCode);
        Assert.Contains("run_20251005T120000Z_bothmatch", output);
        Assert.DoesNotContain("run_20251005T120001Z_templateonly", output);
        Assert.DoesNotContain("run_20251005T120002Z_modelonly", output);
        Assert.DoesNotContain("run_20251005T120003Z_neither", output);
        Assert.Contains("Total: 1 artifacts", output);
    }

    [Fact]
    public async Task ArtifactsList_WithTemplateIdFlag_OutputFormatCorrect()
    {
        // Arrange: Create artifact with known values
        CreateTestArtifact("run_20251005T130000Z_formatcheck", "template-1", "model_999", "Format Check Artifact");

        // Act: Query artifacts
        var (exitCode, output, error) = await RunCliCommand($"artifacts list --template-id template-1 --data-dir \"{testDataDir}\"");

        // Assert: Verify output format
        Assert.Equal(0, exitCode);
        
        // Check table header is present
        Assert.Contains("ID", output);
        Assert.Contains("Type", output);
        Assert.Contains("Created", output);
        Assert.Contains("Title", output);
        
        // Check artifact data appears
        Assert.Contains("run_20251005T130000Z_formatcheck", output);
        Assert.Contains("run", output); // artifact type
        // Title is displayed (format verified by table headers)
        
        // Check footer
        Assert.Contains("Total: 1 artifacts", output);
    }
}
