using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Execution;
using FlowTime.Contracts.Services;
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
        Assert.True(File.Exists(Path.Combine(result1.RunDirectory, "model", "model.yaml")));
        Assert.True(File.Exists(Path.Combine(result1.RunDirectory, "model", "metadata.json")));

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

        using var runJson = JsonDocument.Parse(await File.ReadAllTextAsync(runJsonPath));
        Assert.True(runJson.RootElement.TryGetProperty("runId", out _));
        Assert.True(runJson.RootElement.TryGetProperty("scenarioHash", out _));
        Assert.True(runJson.RootElement.TryGetProperty("series", out _));
        var runModelHash = runJson.RootElement.TryGetProperty("modelHash", out var modelHashElement)
            ? modelHashElement.GetString()
            : null;
        Assert.False(string.IsNullOrWhiteSpace(runModelHash));

        var metadataPath = Path.Combine(result.RunDirectory, "model", "metadata.json");
        Assert.True(File.Exists(metadataPath));
        using var metadataJson = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
        var metadataModelHash = metadataJson.RootElement.GetProperty("modelHash").GetString();
        Assert.False(string.IsNullOrWhiteSpace(metadataModelHash));
        Assert.Equal(runModelHash, metadataModelHash);

        var manifestPath = Path.Combine(result.RunDirectory, "manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var manifestJson = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(runModelHash, manifestJson.RootElement.GetProperty("modelHash").GetString());

        // Verify index.json structure
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        Assert.True(File.Exists(indexPath));

        using var indexJson = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
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
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "model", "metadata.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "spec.yaml")));

        // Should generate CSV file for the demand series
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var csvFiles = Directory.GetFiles(seriesDir, "*.csv");
        Assert.NotEmpty(csvFiles);

        var csvSample = await File.ReadAllLinesAsync(csvFiles[0]);
        Assert.True(csvSample.Length >= 2);
        Assert.Equal("bin_index,value", csvSample[0]);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_WritesClassSeriesWhenPresent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_classes");
        Directory.CreateDirectory(tempDir);

        var spec = CreateClassAwareSpec();
        var model = ModelService.ParseAndConvert(spec);
        var (grid, graph) = ModelParser.ParseModel(model);
        var evaluation = graph.Evaluate(grid);
        var context = evaluation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        using var indexJson = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        var seriesArray = indexJson.RootElement.GetProperty("series");
        Assert.Contains(seriesArray.EnumerateArray(), element =>
            element.TryGetProperty("class", out var classProp) &&
            classProp.GetString() is { } value &&
            !string.Equals(value, "DEFAULT", StringComparison.OrdinalIgnoreCase));

        var runJsonPath = Path.Combine(result.RunDirectory, "run.json");
        using var runJson = JsonDocument.Parse(await File.ReadAllTextAsync(runJsonPath));
        Assert.Equal("full", runJson.RootElement.GetProperty("classCoverage").GetString());

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_ClassAssignmentsOnExpressionNodes_AreRespected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_classes_expr");
        Directory.CreateDirectory(tempDir);

        var spec = CreateExpressionClassSpec();
        var model = ModelService.ParseAndConvert(spec);
        var (grid, graph) = ModelParser.ParseModel(model);
        var evaluation = graph.Evaluate(grid);
        var context = evaluation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        using var indexJson = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        var seriesArray = indexJson.RootElement.GetProperty("series");
        Assert.Contains(seriesArray.EnumerateArray(), element =>
            element.TryGetProperty("class", out var classProp) &&
            classProp.GetString() is { } value &&
            !string.Equals(value, "DEFAULT", StringComparison.OrdinalIgnoreCase));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_ClassSeriesSurviveCapacityLimits()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_classes_capacity");
        Directory.CreateDirectory(tempDir);

        var spec = CreateCapacityLimitedClassSpec();
        var model = ModelService.ParseAndConvert(spec);
        var (grid, graph) = ModelParser.ParseModel(model);
        var evaluation = graph.Evaluate(grid);
        var context = evaluation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var indexPath = Path.Combine(result.RunDirectory, "series", "index.json");
        using var indexJson = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        var seriesArray = indexJson.RootElement.GetProperty("series");

        Assert.Contains(seriesArray.EnumerateArray(), element =>
            element.GetProperty("id").GetString()!.StartsWith("served@") &&
            element.GetProperty("class").GetString() == "Alpha");

        Assert.Contains(seriesArray.EnumerateArray(), element =>
            element.GetProperty("id").GetString()!.StartsWith("processed@") &&
            element.GetProperty("class").GetString() == "Beta");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_classes_scaling");
        Directory.CreateDirectory(tempDir);

        var spec = CreateClassScalingSpec();
        var model = ModelService.ParseAndConvert(spec);
        var (grid, graph) = ModelParser.ParseModel(model);
        var evaluation = graph.Evaluate(grid);
        var context = evaluation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");

        var alphaLoss = ReadFirstValue(Path.Combine(seriesDir, "losses@LOSSES@Alpha.csv"));
        var betaLoss = ReadFirstValue(Path.Combine(seriesDir, "losses@LOSSES@Beta.csv"));
        Assert.Equal(2.5, alphaLoss, 5);
        Assert.Equal(5.0, betaLoss, 5);

        var alphaRemaining = ReadFirstValue(Path.Combine(seriesDir, "available@AVAILABLE@Alpha.csv"));
        var betaRemaining = ReadFirstValue(Path.Combine(seriesDir, "available@AVAILABLE@Beta.csv"));
        Assert.Equal(7.5, alphaRemaining, 5);
        Assert.Equal(15.0, betaRemaining, 5);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_ClassSeriesDistributeClasslessAdditiveTerms()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_classes_add_sub");
        Directory.CreateDirectory(tempDir);

        var spec = CreateClassAdditionSubtractionSpec();
        var model = ModelService.ParseAndConvert(spec);
        var (grid, graph) = ModelParser.ParseModel(model);
        var evaluation = graph.Evaluate(grid);
        var context = evaluation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var seriesDir = Path.Combine(result.RunDirectory, "series");
        var total = ReadSeriesValues(Path.Combine(seriesDir, "net_output@NET_OUTPUT@DEFAULT.csv"));
        var alpha = ReadSeriesValues(Path.Combine(seriesDir, "net_output@NET_OUTPUT@Alpha.csv"));
        var beta = ReadSeriesValues(Path.Combine(seriesDir, "net_output@NET_OUTPUT@Beta.csv"));

        for (var i = 0; i < total.Length; i++)
        {
            Assert.Equal(total[i], alpha[i] + beta[i], 5);
        }

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
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "model", "metadata.json")));

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_NormalizesTopologySemantics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_semantics");
        Directory.CreateDirectory(tempDir);

        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 5, BinUnit = "minutes" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "arrivals_series", Kind = "const", Values = new[] { 10.0, 12.0 } },
                new() { Id = "served_series", Kind = "const", Values = new[] { 9.0, 11.0 } },
                new() { Id = "error_series", Kind = "const", Values = new[] { 1.0, 1.0 } }
            },
            Outputs = new List<OutputDefinition> { new() { Series = "*", As = "out.csv" } }
        };

        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals_series")] = new[] { 10.0, 12.0 },
            [new NodeId("served_series")] = new[] { 9.0, 11.0 },
            [new NodeId("error_series")] = new[] { 1.0, 1.0 }
        };

        const string spec = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
topology:
  nodes:
  - id: Edge
    semantics:
      arrivals: arrivals_series
      served: served_series
      errors: error_series
nodes:
- id: arrivals_series
  kind: const
  values: [10, 12]
- id: served_series
  kind: const
  values: [9, 11]
- id: error_series
  kind: const
  values: [1, 1]
outputs:
- series: "*"
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = new TimeGrid(2, 5, TimeUnit.Minutes),
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var modelYamlPath = Path.Combine(result.RunDirectory, "model", "model.yaml");
        var modelYaml = await File.ReadAllTextAsync(modelYamlPath);

        Assert.Contains("file:../series/arrivals_series@ARRIVALS_SERIES@DEFAULT.csv", modelYaml);
        Assert.Contains("file:../series/served_series@SERVED_SERIES@DEFAULT.csv", modelYaml);
        Assert.Contains("file:../series/error_series@ERROR_SERIES@DEFAULT.csv", modelYaml);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_ThrowsWhenErrorsMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_semantics_missing");
        Directory.CreateDirectory(tempDir);

        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 5, BinUnit = "minutes" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "arrivals_series", Kind = "const", Values = new[] { 10.0, 12.0 } },
                new() { Id = "served_series", Kind = "const", Values = new[] { 9.0, 11.0 } }
            },
            Outputs = new List<OutputDefinition> { new() { Series = "*", As = "out.csv" } }
        };

        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals_series")] = new[] { 10.0, 12.0 },
            [new NodeId("served_series")] = new[] { 9.0, 11.0 }
        };

        const string spec = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
topology:
  nodes:
  - id: Edge
    semantics:
      arrivals: arrivals_series
      served: served_series
      errors: ""
nodes:
- id: arrivals_series
  kind: const
  values: [10, 12]
- id: served_series
  kind: const
  values: [9, 11]
outputs:
- series: "*"
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = new TimeGrid(2, 5, TimeUnit.Minutes),
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunArtifactWriter.WriteArtifactsAsync(request));
        Assert.Contains("semantics.errors", ex.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_WildcardOutputs_IncludesAllSeries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_wildcard");
        Directory.CreateDirectory(tempDir);

        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 5, BinUnit = "minutes" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "demand", Kind = "const", Values = new[] { 1.0, 2.0 } },
                new() { Id = "served", Kind = "const", Values = new[] { 0.5, 1.5 } }
            },
            Outputs = new List<OutputDefinition>
            {
                new() { Series = "*", As = "all.csv" }
            }
        };

        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("demand")] = new[] { 1.0, 2.0 },
            [new NodeId("served")] = new[] { 0.5, 1.5 }
        };

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = new TimeGrid(2, 5, TimeUnit.Minutes),
            Context = context,
            SpecText = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
outputs:
  - series: "*"
""",
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var csvFiles = Directory.GetFiles(Path.Combine(result.RunDirectory, "series"), "*.csv");
        Assert.Equal(2, csvFiles.Length);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task WriteArtifacts_WritesInvariantWarnings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_artifacts_warnings");
        Directory.CreateDirectory(tempDir);

        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 60, BinUnit = "minutes" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "arrivals", Kind = "const", Values = new[] { 10.0, 10.0 } },
                new() { Id = "served", Kind = "const", Values = new[] { 15.0, 5.0 } },
                new() { Id = "errors", Kind = "const", Values = new[] { 0.0, 0.0 } }
            },
            Topology = new TopologyDefinition
            {
                Nodes = new List<TopologyNodeDefinition>
                {
                    new()
                    {
                        Id = "Delivery",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors"
                        }
                    }
                }
            },
            Outputs = new List<OutputDefinition>()
        };

        var grid = new TimeGrid(2, 60, TimeUnit.Minutes);
        var context = new Dictionary<NodeId, double[]>
        {
            [new NodeId("arrivals")] = new[] { 10.0, 10.0 },
            [new NodeId("served")] = new[] { 15.0, 5.0 },
            [new NodeId("errors")] = new[] { 0.0, 0.0 }
        };

        const string spec = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 60
  binUnit: minutes
topology:
  nodes:
  - id: Delivery
    semantics:
      arrivals: arrivals
      served: served
      errors: errors
nodes:
- id: arrivals
  kind: const
  values: [10, 10]
- id: served
  kind: const
  values: [15, 5]
- id: errors
  kind: const
  values: [0, 0]
""";

        var request = new RunArtifactWriter.WriteRequest
        {
            Model = model,
            Grid = grid,
            Context = context,
            SpecText = spec,
            OutputDirectory = tempDir
        };

        var result = await RunArtifactWriter.WriteArtifactsAsync(request);
        var runJsonPath = Path.Combine(result.RunDirectory, "run.json");
        var json = await File.ReadAllTextAsync(runJsonPath);
        using var doc = JsonDocument.Parse(json);
        var warnings = doc.RootElement.GetProperty("warnings");

        Assert.True(warnings.GetArrayLength() > 0);
        Assert.Equal("served_exceeds_arrivals", warnings[0].GetProperty("code").GetString());

        Directory.Delete(tempDir, true);
    }

    private static ModelDefinition CreateTestModel()
    {
        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 60, BinUnit = "minutes" },
            Nodes = new List<NodeDefinition>
            {
                new() { Id = "demand", Kind = "const", Values = new[] { 10.0, 20.0, 30.0 } },
                new() { Id = "served", Kind = "const", Values = new[] { 8.0, 16.0, 24.0 } }
            },
            Outputs = new List<OutputDefinition>()
        };
    }

    private static string CreateClassAwareSpec() => @"
schemaVersion: 1
classes:
  - id: Alpha
  - id: Beta
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
traffic:
  arrivals:
    - nodeId: alpha_input
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_input
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: alpha_input
    kind: const
    values: [4, 4, 4]
  - id: beta_input
    kind: const
    values: [2, 2, 2]
  - id: demand
    kind: expr
    expr: ""alpha_input + beta_input""
  - id: capacity
    kind: const
    values: [5, 5, 5]
  - id: served
    kind: expr
    expr: ""MIN(demand, capacity)""
topology:
  nodes:
    - id: Service
      semantics:
        arrivals: demand
        served: served
        errors: demand
      initialCondition:
        queueDepth: 0
outputs:
  - series: served
    as: served.csv
";

    private static string CreateExpressionClassSpec() => @"
schemaVersion: 1
classes:
  - id: Alpha
  - id: Beta
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
traffic:
  arrivals:
    - nodeId: alpha_flow
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_flow
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: demand_base
    kind: const
    values: [10, 12, 14]
  - id: alpha_flow
    kind: expr
    expr: ""demand_base * 0.5""
  - id: beta_flow
    kind: expr
    expr: ""MAX(0, demand_base - alpha_flow)""
  - id: demand
    kind: expr
    expr: ""alpha_flow + beta_flow""
  - id: served
    kind: expr
    expr: ""demand""
topology:
  nodes:
    - id: Service
      semantics:
        arrivals: demand
        served: served
        errors: demand
outputs:
  - series: served
    as: served.csv
";

    private static double ReadFirstValue(string csvPath)
    {
        var firstDataLine = File.ReadLines(csvPath).Skip(1).FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(firstDataLine), $"No data rows found in {csvPath}");

        var parts = firstDataLine!.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(parts.Length >= 2, $"Unexpected CSV format in {csvPath}");

        return double.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    private static double[] ReadSeriesValues(string csvPath)
    {
        return File.ReadAllLines(csvPath)
            .Skip(1)
            .Select(line => line.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(parts => double.Parse(parts[1], CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static string CreateCapacityLimitedClassSpec() => @"
schemaVersion: 1
classes:
  - id: Alpha
  - id: Beta
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
traffic:
  arrivals:
    - nodeId: alpha_base
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_base
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: alpha_base
    kind: const
    values: [8, 8, 8]
  - id: beta_base
    kind: const
    values: [4, 4, 4]
  - id: inflow
    kind: expr
    expr: alpha_base + beta_base
  - id: capacity
    kind: const
    values: [6, 6, 6]
  - id: served
    kind: expr
    expr: MIN(inflow, capacity)
  - id: processed
    kind: expr
    expr: served * 0.5
topology:
  nodes:
    - id: Demo
      semantics:
        arrivals: inflow
        served: served
        errors: inflow
outputs:
  - series: served
    as: served.csv
  - series: processed
    as: processed.csv
";

    private static string CreateClassScalingSpec() => @"
schemaVersion: 1
classes:
  - id: Alpha
  - id: Beta
grid:
  bins: 3
  binSize: 5
  binUnit: minutes
traffic:
  arrivals:
    - nodeId: alpha_flow
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_flow
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: alpha_flow
    kind: const
    values: [10, 10, 10]
  - id: beta_flow
    kind: const
    values: [20, 20, 20]
  - id: inflow
    kind: expr
    expr: alpha_flow + beta_flow
  - id: losses
    kind: expr
    expr: inflow * 0.25
  - id: available
    kind: expr
    expr: MAX(0, inflow - losses)
topology:
  nodes:
    - id: Demo
      semantics:
        arrivals: inflow
        served: available
        errors: losses
outputs:
  - series: losses
    as: losses.csv
  - series: available
    as: available.csv
";

    private static string CreateClassAdditionSubtractionSpec() => @"
schemaVersion: 1
classes:
  - id: Alpha
  - id: Beta
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
traffic:
  arrivals:
    - nodeId: alpha_flow
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_flow
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
nodes:
  - id: alpha_flow
    kind: const
    values: [10, 10]
  - id: beta_flow
    kind: const
    values: [5, 5]
  - id: cap_term
    kind: const
    values: [4, 4]
  - id: net_output
    kind: expr
    expr: alpha_flow + beta_flow - cap_term
topology:
  nodes:
    - id: TestNode
      semantics:
        arrivals: alpha_flow
        served: net_output
        errors: beta_flow
outputs:
  - series: net_output
    as: net_output.csv
";
}
