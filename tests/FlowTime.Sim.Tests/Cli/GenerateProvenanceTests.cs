using System.Text.Json;
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
    private readonly INodeBasedTemplateService _templateService;

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
metadata:
  id: test-template
  title: Test Template
  description: Test template for provenance CLI tests
parameters:
  - name: bins
    type: integer
    default: 10
  - name: binSize
    type: integer
    default: 1
grid:
  bins: {{bins}}
  binSize: {{binSize}}
  binUnit: hours
nodes:
  - id: NODE_A
    type: queue
    capacity: 100
".Trim();

        File.WriteAllText(Path.Combine(_templatesDir, "test-template.yaml"), testTemplateYaml);

        // Create template service (provenance service will be used by CLI command handler)
        _templateService = new NodeBasedTemplateService(_templatesDir, NullLogger<NodeBasedTemplateService>.Instance);
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
        Assert.StartsWith("model_", provenance.RootElement.GetProperty("modelId").GetString());
    }

    [Fact]
    public async Task Generate_WithProvenanceOption_ProvenanceIncludesParameters()
    {
        // Arrange
        var paramsPath = Path.Combine(_testDir, "params.json");
        await File.WriteAllTextAsync(paramsPath, """{"bins": 12, "binSize": 2}""");

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
        Assert.Equal(12, parameters.GetProperty("bins").GetInt32());
        Assert.Equal(2, parameters.GetProperty("binSize").GetInt32());
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
        Assert.Contains("modelId: model_", modelYaml);
        
        // Verify provenance comes after schemaVersion but before grid
        var schemaVersionPos = modelYaml.IndexOf("schemaVersion:");
        var provenancePos = modelYaml.IndexOf("provenance:");
        var gridPos = modelYaml.IndexOf("grid:");
        
        Assert.True(schemaVersionPos < provenancePos, "provenance should come after schemaVersion");
        Assert.True(provenancePos < gridPos, "provenance should come before grid");
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
            null,  // No provenance options
            false);

        // Act
        var exitCode = await ExecuteGenerateWithProvenance(_templateService, opts);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(modelPath));

        var modelYaml = await File.ReadAllTextAsync(modelPath);
        
        // Should NOT contain provenance
        Assert.DoesNotContain("provenance:", modelYaml);
        
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
    private Task<int> ExecuteGenerateWithProvenance(INodeBasedTemplateService service, CliOptions opts)
    {
        // Call the actual Program.ExecuteGenerateCommand via ProgramWrapper
        return ProgramWrapper.ExecuteGenerate(service, opts, CancellationToken.None);
    }

    #endregion
}
