using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowTime.Sim.Cli;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Cli;

/// <summary>
/// TDD Red Phase: CLI tests for SIM-M2.7 Phase 3 - Provenance CLI Support
/// These tests define the expected behavior before implementation.
/// </summary>
public class GenerateProvenanceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _templatesDir;
    private readonly ITemplateService _templateService;

    public GenerateProvenanceTests()
    {
        // Set up test environment
        _testDir = Path.Combine(Path.GetTempPath(), "flow-sim-cli-provenance-tests", Guid.NewGuid().ToString("N"));
        _templatesDir = Path.Combine(_testDir, "templates");
        Directory.CreateDirectory(_templatesDir);

        // Set environment variables for service
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_DATA_DIR", _testDir);
        Environment.SetEnvironmentVariable("FLOWTIME_SIM_TEMPLATES_DIR", _templatesDir);

        // Create test template
        var testTemplateYaml = @"
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-template
  title: Test Template
  description: Test template for provenance CLI tests
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: bins
    type: integer
    default: 3
  - name: binSize
    type: integer
    default: 1
grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes
topology:
  nodes:
    - id: TestQueue
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        queueDepth: queue_depth
      initialCondition:
        queueDepth: 0
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [50, 50, 50]
  - id: served
    kind: expr
    expr: ""arrivals""
  - id: queue_depth
    kind: backlog
    inflow: arrivals
    outflow: served
outputs:
  - series: ""*""
".Trim();

        File.WriteAllText(Path.Combine(_templatesDir, "test-template.yaml"), testTemplateYaml);

        // Create template service (provenance service will be used by CLI command handler)
        _templateService = new TemplateService(_templatesDir, NullLogger<TemplateService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Argument Parsing Tests

    [Fact]
    public void ArgParser_ParsesProvenanceOption()
    {
        // Arrange & Act
        var opts = ArgParser.ParseArgs(new[] { "generate", "--id", "test-template", "--provenance", "provenance.json" });

        // Assert
        Assert.Equal("generate", opts.Verb);
        Assert.Equal("test-template", opts.TemplateId);
        Assert.Equal("provenance.json", opts.ProvenancePath);
    }

    [Fact]
    public void ArgParser_ParsesEmbedProvenanceFlag()
    {
        // Arrange & Act
        var opts = ArgParser.ParseArgs(new[] { "generate", "--id", "test-template", "--embed-provenance" });

        // Assert
        Assert.Equal("generate", opts.Verb);
        Assert.Equal("test-template", opts.TemplateId);
        Assert.True(opts.EmbedProvenance);
    }

    [Fact]
    public void ArgParser_ParsesBothProvenanceOptions()
    {
        // Arrange & Act
        var opts = ArgParser.ParseArgs(new[] 
        { 
            "generate", 
            "--id", "test-template", 
            "--provenance", "provenance.json",
            "--embed-provenance"
        });

        // Assert
        Assert.Equal("provenance.json", opts.ProvenancePath);
        Assert.True(opts.EmbedProvenance);
    }

    #endregion

    #region Separate Provenance File Tests

    [Fact]
    public async Task Generate_WithProvenanceOption_SavesProvenanceToFile()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var provenancePath = Path.Combine(_testDir, "provenance.json");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            provenancePath,  // ProvenancePath
            false);          // EmbedProvenance

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath), "Model file should exist");
        Assert.True(File.Exists(provenancePath), "Provenance file should exist");

        // Verify provenance content
        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonDocument.Parse(provenanceJson);
        
        Assert.Equal("flowtime-sim", provenance.RootElement.GetProperty("source").GetString());
        Assert.Equal("test-template", provenance.RootElement.GetProperty("templateId").GetString());
        Assert.Matches(@"[a-f0-9]{64}", provenance.RootElement.GetProperty("modelId").GetString());
    }

    [Fact]
    public async Task Generate_WithProvenanceOption_ProvenanceIncludesParameters()
    {
        // Arrange
        var paramsPath = Path.Combine(_testDir, "params.json");
        await File.WriteAllTextAsync(paramsPath, """{"bins": 3, "binSize": 2}""");

        var modelPath = Path.Combine(_testDir, "model.yaml");
        var provenancePath = Path.Combine(_testDir, "provenance.json");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            paramsPath,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            provenancePath,
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(provenancePath));

        var provenanceJson = await File.ReadAllTextAsync(provenancePath);
        var provenance = JsonDocument.Parse(provenanceJson);
        
        var parameters = provenance.RootElement.GetProperty("parameters");
        Assert.Equal(3, GetInt32(parameters.GetProperty("bins")));
        Assert.Equal(2, GetInt32(parameters.GetProperty("binSize")));
    }

    [Fact]
    public async Task Generate_WithoutProvenanceOption_DoesNotSaveProvenanceFile()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var provenancePath = Path.Combine(_testDir, "provenance.json");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            null,  // No provenance path
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath), "Model file should exist");
        Assert.False(File.Exists(provenancePath), "Provenance file should NOT exist");
    }

    #endregion

    #region Embedded Provenance Tests

    [Fact]
    public async Task Generate_WithEmbedProvenanceFlag_EmbedsProvenanceInModel()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            null,
            true);  // EmbedProvenance = true

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath));

        var modelYaml = await File.ReadAllTextAsync(modelPath);
        
        // Verify embedded provenance structure
        Assert.Contains("provenance:", modelYaml);
        Assert.Contains("source: flowtime-sim", modelYaml);
        Assert.Contains("templateId: test-template", modelYaml);
        Assert.Matches(@"modelId: [a-f0-9]{64}", modelYaml);
        
        // Verify provenance comes after schemaVersion but before grid
        var schemaVersionPos = modelYaml.IndexOf("schemaVersion:");
        var provenancePos = modelYaml.IndexOf("provenance:");
        var outputsPos = modelYaml.IndexOf("outputs:");
        
        Assert.True(schemaVersionPos < provenancePos, "provenance should come after schemaVersion");
        Assert.True(outputsPos >= 0, "outputs section should be present");
        Assert.True(provenancePos > outputsPos, "provenance should follow outputs");
    }

    [Fact]
    public async Task Generate_WithEmbedProvenance_NoSeparateProvenanceFile()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var provenancePath = Path.Combine(_testDir, "provenance.json");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            null,
            true);  // EmbedProvenance = true

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath));
        Assert.False(File.Exists(provenancePath), "No separate provenance file should be created");
    }

    #endregion

    #region Mutual Exclusivity Tests

    [Fact]
    public async Task Generate_WithBothProvenanceOptions_ReturnsError()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var provenancePath = Path.Combine(_testDir, "provenance.json");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            provenancePath,  // ProvenancePath set
            true);           // EmbedProvenance also set

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.NotEqual(0, exitCode); // Should return error code
        
        // Model should not be created when there's an error
        Assert.False(File.Exists(modelPath), "Model should not be created on error");
        Assert.False(File.Exists(provenancePath), "Provenance should not be created on error");
    }

    [Fact]
    public async Task Generate_WithModeOverride_WritesTelemetryMode()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            "telemetry",
            null,
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        var modelYaml = await File.ReadAllTextAsync(modelPath);
        Assert.Contains("mode: telemetry", modelYaml);
    }

    [Fact]
    public async Task Generate_WithInvalidMode_ReturnsError()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            "invalid",
            null,
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(modelPath));
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public async Task Generate_WithoutAnyProvenanceOptions_WorksAsExpected()
    {
        // Arrange
        var modelPath = Path.Combine(_testDir, "model.yaml");
        var opts = new CliOptions(
            "generate",
            null,
            "test-template",
            null,
            modelPath,
            "yaml",
            false,
            _templatesDir,
            null,
            null,
            null,  // No provenance options
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath));

        var modelYaml = await File.ReadAllTextAsync(modelPath);
        
        // Embedded provenance is now included by default in generated models
        Assert.Contains("provenance:", modelYaml);
        
        // Should contain expected model structure
        Assert.Contains("schemaVersion: 1", modelYaml);
        Assert.Contains("grid:", modelYaml);
        Assert.Contains("nodes:", modelYaml);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Calls the actual ExecuteGenerateCommand with provenance support.
    /// </summary>
    private Task<int> ExecuteGenerateWithProvenance(ITemplateService service, CliOptions opts)
    {
        // Call the actual Program.ExecuteGenerateCommand via ProgramWrapper
        return ProgramWrapper.ExecuteGenerate(service, opts, CancellationToken.None);
    }

    private static int GetInt32(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetInt32(),
        JsonValueKind.String => int.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Unsupported JSON value kind {element.ValueKind} for numeric conversion")
    };

    #endregion
}
