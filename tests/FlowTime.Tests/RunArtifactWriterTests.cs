using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using System.Text.Json;
using Xunit;

namespace FlowTime.Tests;

public class RunArtifactWriterTests
{
    [Fact]
    public async Task WriteArtifacts_DeterministicRunId_ProducesSameOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_deterministic");
        Directory.CreateDirectory(tempDir);

        var model = CreateTestModel();
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 10.0, 20.0, 30.0 },
            [new NodeId("served")] = new[] { 8.0, 16.0, 24.0 }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = "test model yaml",
            DeterministicRunId = true,
            OutputDirectory = tempDir
        };

        // Write artifacts twice with same request
        var result1 = await RunArtifactWriter.WriteArtifactsAsync(request);
        var result2 = await RunArtifactWriter.WriteArtifactsAsync(request);

        // Should produce identical results
        Assert.Equal(result1.RunId, result2.RunId);
        Assert.Equal(result1.ScenarioHash, result2.ScenarioHash);

        // Verify files exist
        Assert.True(Directory.Exists(result1.RunDirectory));
        Assert.True(File.Exists(Path.Combine(result1.RunDirectory, "run.json")));
        Assert.True(File.Exists(Path.Combine(result1.RunDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(result1.RunDirectory, "series", "index.json")));

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_GeneratesCompliantManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_compliant");
        Directory.CreateDirectory(tempDir);

        var model = CreateTestModel();
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 10.0, 20.0, 30.0 },
            [new NodeId("served")] = new[] { 8.0, 16.0, 24.0 }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = "test model yaml",
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);

        // Verify basic result structure
        Assert.NotNull(result.RunId);
        Assert.NotNull(result.ScenarioHash);
        Assert.True(Directory.Exists(result.RunDirectory));

        // Verify run.json structure
        var runJsonPath = Path.Combine(result.RunDirectory, "run.json");
        Assert.True(File.Exists(runJsonPath));

        var runContent = await File.ReadAllTextAsync(runJsonPath);
        var runJson = JsonDocument.Parse(runContent);
        Assert.True(runJson.RootElement.TryGetProperty("runId", out _));
        Assert.True(runJson.RootElement.TryGetProperty("scenarioHash", out _));
        Assert.True(runJson.RootElement.TryGetProperty("series", out _));

        // Verify index.json structure
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        Assert.True(File.Exists(indexPath));

        var indexContent = await File.ReadAllTextAsync(indexPath);
        var indexJson = JsonDocument.Parse(indexContent);
        Assert.True(indexJson.RootElement.TryGetProperty("series", out _));
        Assert.True(indexJson.RootElement.TryGetProperty("grid", out _));

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_DifferentScenarios_ProduceDifferentHashes()
    {
        var tempDir1 = Path.Combine(Path.GetTempPath(), "test_artifacts_hash1");
        var tempDir2 = Path.Combine(Path.GetTempPath(), "test_artifacts_hash2");
        Directory.CreateDirectory(tempDir1);
        Directory.CreateDirectory(tempDir2);

        var model = CreateTestModel();
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);
        var context1 = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 10.0, 20.0, 30.0 }
        };
        var context2 = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 15.0, 25.0, 35.0 }
        };

        var request1 = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context1,
            SpecText = "test model yaml scenario 1",
            OutputDirectory = tempDir1
        };

        var request2 = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context2,
            SpecText = "test model yaml scenario 2",
            OutputDirectory = tempDir2
        };

        var result1 = await RunArtifactWriter.WriteArtifactsAsync(request1);
        var result2 = await RunArtifactWriter.WriteArtifactsAsync(request2);

        // Different spec text should produce different scenario hashes
        Assert.NotEqual(result1.ScenarioHash, result2.ScenarioHash);

        // Cleanup
        Directory.Delete(tempDir1, true);
        Directory.Delete(tempDir2, true);
    }

    [Fact]
    public async Task WriteArtifacts_AutoGeneratesRequiredOutputs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_auto");
        Directory.CreateDirectory(tempDir);

        var model = CreateTestModel();
        var grid = new TimeGrid(2, 30, TimeUnit.Minutes);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 5.0, 15.0 }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = "test model yaml",
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);

        // Should auto-generate standard outputs
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "run.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "series", "index.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "spec.yaml")));

        // Should generate CSV file for the demand series
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var csvFiles = Directory.GetFiles(seriesDir, "*.csv");
        Assert.NotEmpty(csvFiles);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_EmptyContext_HandlesGracefully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_empty");
        Directory.CreateDirectory(tempDir);

        var model = CreateTestModel();
        var grid = new TimeGrid(1, 60, TimeUnit.Minutes);
        var context = new Dictionary<NodeId, double[]>();

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = "test model yaml",
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);

        // Should still generate valid artifacts with minimal content
        Assert.NotNull(result.RunId);
        Assert.NotNull(result.ScenarioHash);
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "run.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "series", "index.json")));

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    private static TestModelDto CreateTestModel()
    {
        // Create a model that will trigger the auto-output generation branch
        // This relies on the code that creates outputs for all series in context when Outputs is null/empty
        return new TestModelDto
        {
            Grid = new TestGridDto { Bins = 3, BinMinutes = 60 },
            Nodes = new List<TestNodeDto>
            {
                new() { Id = "demand", Kind = "const", Values = new[] { 10.0, 20.0, 30.0 } },
                new() { Id = "served", Kind = "const", Values = new[] { 8.0, 16.0, 24.0 } }
            },
            Outputs = new List<TestOutputDto>() // Empty list to trigger auto-generation
        };
    }

    // Test DTOs that mimic the CLI ModelDto structure
    public class TestModelDto
    {
        public TestGridDto Grid { get; set; } = new();
        public List<TestNodeDto> Nodes { get; set; } = new();
        public List<TestOutputDto> Outputs { get; set; } = new();
    }

    public class TestGridDto
    {
        public int Bins { get; set; }
        public int BinMinutes { get; set; }
    }

    public class TestNodeDto
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "const";
        public double[]? Values { get; set; }
        public string? Expr { get; set; }
    }

    public class TestOutputDto
    {
        public string Series { get; set; } = "";
        public string As { get; set; } = "out.csv";
    }
}
