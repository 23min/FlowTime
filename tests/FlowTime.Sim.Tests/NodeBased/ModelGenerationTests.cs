using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class ModelGenerationTests
{
    [Fact]
    public async Task GenerateEngineModelAsync_WithConstNodes_ProducesEngineCompatibleYaml()
    {
        // Arrange - Template with const nodes using ${} placeholders
        var templateYaml = """
metadata:
  id: const-model-test
  title: Constant Node Model Test
parameters:
  - name: arrival_rate
    type: number
    default: 100
    min: 0
    max: 1000
grid:
  bins: 4
  binSize: 30
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [${arrival_rate}, 150, 200, 120]
  - id: capacity
    kind: const
    values: [300, 300, 300, 300]
outputs:
  - id: arrivals_output
    source: arrivals
    description: Arrival data
    filename: arrivals.csv
rng:
  kind: pcg32
  seed: 12345
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"arrival_rate", 80}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("const-model-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("parameters:", generatedYaml); // Parameters should be stripped
        Assert.DoesNotContain("metadata:", generatedYaml); // Metadata should be stripped
        Assert.DoesNotContain("${arrival_rate}", generatedYaml); // Parameter should be substituted
        Assert.Contains("80", generatedYaml); // Substituted value should be present

        // Validate Engine schema conversion
        Assert.Contains("schemaVersion: 1", generatedYaml);
        Assert.Contains("grid:", generatedYaml);
        Assert.Contains("bins: 4", generatedYaml);
        Assert.Contains("binSize: 30", generatedYaml); // Now passed through unchanged
        Assert.Contains("binUnit: minutes", generatedYaml); // Now passed through unchanged
        Assert.Contains("nodes:", generatedYaml);
        Assert.Contains("kind: const", generatedYaml);
        Assert.Contains("outputs:", generatedYaml);
        Assert.Contains("series: arrivals", generatedYaml); // Engine format
        Assert.Contains("as: arrivals.csv", generatedYaml); // Engine format
        Assert.DoesNotContain("source:", generatedYaml); // Should be converted
        Assert.DoesNotContain("filename:", generatedYaml); // Should be converted
        Assert.Contains("rng:", generatedYaml);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_WithPmfNodes_PreservesStochasticDefinitions()
    {
        // Arrange - Template with PMF nodes (should NOT be pre-compiled)
        var templateYaml = """
metadata:
  id: pmf-model-test
  title: PMF Node Model Test
parameters:
  - name: high_prob
    type: number
    default: 0.6
    min: 0.1
    max: 0.9
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: stochastic_demand
    kind: pmf
    pmf:
      values: [10, 20, 30]
      probabilities: [0.2, ${high_prob}, 0.2]
  - id: baseline
    kind: const
    values: [100, 100, 100]
outputs:
  - id: demand_output
    source: stochastic_demand
    description: Stochastic demand data
    filename: demand.csv
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"high_prob", 0.7}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("pmf-model-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("parameters:", generatedYaml); // Parameters should be stripped
        Assert.DoesNotContain("metadata:", generatedYaml); // Metadata should be stripped
        Assert.DoesNotContain("${high_prob}", generatedYaml); // Parameter should be substituted
        Assert.Contains("0.7", generatedYaml); // Substituted value should be present
        
        // Validate grid format: now uses binSize/binUnit (no conversion)
        Assert.Contains("binSize: 1", generatedYaml);
        Assert.Contains("binUnit: hours", generatedYaml);
        
        // Validate PMF structure preserved for Engine
        Assert.Contains("kind: pmf", generatedYaml);
        Assert.Contains("pmf:", generatedYaml);
        Assert.Contains("values: [10, 20, 30]", generatedYaml);
        Assert.Contains("probabilities: [0.2, 0.7, 0.2]", generatedYaml);
        
        // Validate output conversion to Engine format
        Assert.Contains("outputs:", generatedYaml);
        Assert.Contains("series: stochastic_demand", generatedYaml); // Engine format
        Assert.Contains("as: demand.csv", generatedYaml); // Engine format
        Assert.DoesNotContain("source:", generatedYaml); // Should be converted
        Assert.DoesNotContain("filename:", generatedYaml); // Should be converted
    }

    [Fact]
    public async Task GenerateEngineModelAsync_WithExpressionNodes_PreservesNodeReferences()
    {
        // Arrange - Template with expression nodes referencing other nodes
        var templateYaml = """
metadata:
  id: expr-model-test
  title: Expression Node Model Test
parameters:
  - name: efficiency
    type: number
    default: 0.8
    min: 0.1
    max: 1.0
grid:
  bins: 4
  binSize: 15
  binUnit: minutes
nodes:
  - id: demand
    kind: const
    values: [100, 150, 200, 120]
  - id: capacity
    kind: const
    values: [180, 180, 180, 180]
  - id: served
    kind: expr
    expression: "MIN(demand, capacity * ${efficiency})"
outputs:
  - id: served_output
    source: served
    description: Served demand
    filename: served.csv
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"efficiency", 0.9}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("expr-model-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("parameters:", generatedYaml); // Parameters should be stripped
        Assert.DoesNotContain("metadata:", generatedYaml); // Metadata should be stripped
        Assert.DoesNotContain("${efficiency}", generatedYaml); // Parameter should be substituted
        Assert.Contains("0.9", generatedYaml); // Substituted value should be present
        
        // Validate grid format: binSize/binUnit passed through
        Assert.Contains("binSize: 15", generatedYaml);
        Assert.Contains("binUnit: minutes", generatedYaml);
        
        // Validate expression structure conversion: Template.Expression → Engine.expr
        Assert.Contains("kind: expr", generatedYaml);
        Assert.Contains("expr: \"MIN(demand, capacity * 0.9)\"", generatedYaml);
        Assert.DoesNotContain("expression:", generatedYaml); // Should be converted to 'expr'
        
        // Validate output conversion to Engine format
        Assert.Contains("outputs:", generatedYaml);
        Assert.Contains("series: served", generatedYaml); // Engine format
        Assert.Contains("as: served.csv", generatedYaml); // Engine format
        Assert.DoesNotContain("source:", generatedYaml); // Should be converted
        Assert.DoesNotContain("filename:", generatedYaml); // Should be converted
    }

    [Fact]
    public async Task GenerateEngineModelAsync_ComplexTemplate_GeneratesValidYaml()
    {
        // Arrange - Complex template with binSize/binUnit conversion
        var templateYaml = """
metadata:
  id: complex-model-test
  title: Complex Multi-Node Model Test
parameters:
  - name: base_demand
    type: number
    default: 50
    min: 10
    max: 200
grid:
  bins: 3
  binSize: 2
  binUnit: hours
nodes:
  - id: baseline_demand
    kind: const
    values: [${base_demand}, ${base_demand}, ${base_demand}]
  - id: capacity
    kind: const
    values: [100, 100, 100]
outputs:
  - id: demand_output
    source: baseline_demand
    description: Baseline demand data
    filename: demand.csv
rng:
  kind: pcg32
  seed: 54321
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"base_demand", 75}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("complex-model-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("${base_demand}", generatedYaml); // All parameters substituted
        Assert.DoesNotContain("parameters:", generatedYaml); // Parameters stripped
        Assert.DoesNotContain("metadata:", generatedYaml); // Metadata stripped
        Assert.Contains("75", generatedYaml); // Parameter value present
        Assert.Contains("schemaVersion: 1", generatedYaml); // Engine schema version added
        
        // Validate grid format: binSize/binUnit passed through
        Assert.Contains("binSize: 2", generatedYaml);
        Assert.Contains("binUnit: hours", generatedYaml);
        
        Assert.Contains("nodes:", generatedYaml);
        Assert.Contains("kind: const", generatedYaml);
        
        // Validate output conversion to Engine format
        Assert.Contains("outputs:", generatedYaml);
        Assert.Contains("series: baseline_demand", generatedYaml); // Engine format
        Assert.Contains("as: demand.csv", generatedYaml); // Engine format
        Assert.DoesNotContain("source:", generatedYaml); // Should be converted
        Assert.DoesNotContain("filename:", generatedYaml); // Should be converted
    }

    [Fact]
    public async Task GenerateEngineModelAsync_WithSimpleParameters_SubstitutesCorrectly()
    {
        // Arrange - Simple template to test parameter substitution
        var templateYaml = """
metadata:
  id: param-substitution-test
  title: Parameter Substitution Test
parameters:
  - name: rate
    type: number
    default: 100
grid:
  bins: 3
  binSize: 30
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [${rate}, ${rate}, ${rate}]
outputs:
  - series: arrivals
    as: arrivals.csv
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"rate", 150}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("param-substitution-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("${rate}", generatedYaml);
        Assert.Contains("150", generatedYaml);
        Assert.Contains("values: [150, 150, 150]", generatedYaml);
    }

    [Fact]
    public async Task ValidateParametersAsync_ValidParameters_ReturnsSuccess()
    {
        // Arrange
        var templateYaml = """
metadata:
  id: validation-test
  title: Parameter Validation Test
parameters:
  - name: required_param
    type: number
    min: 1
    max: 100
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: test_node
    kind: const
    values: [${required_param}, ${required_param}, ${required_param}]
outputs:
  - series: test_node
    as: test.csv
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"required_param", 50}
        };
        
        // Act
        var result = await service.ValidateParametersAsync("validation-test", parameters);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_WithRngConfiguration_PreservesRngSettings()
    {
        // Arrange - Template with RNG configuration
        var templateYaml = """
metadata:
  id: rng-config-test
  title: RNG Configuration Test
parameters:
  - name: seed_value
    type: integer
    default: 12345
grid:
  bins: 2
  binSize: 30
  binUnit: minutes
nodes:
  - id: test_node
    kind: const
    values: [100, 200]
outputs:
  - series: test_node
    as: test.csv
rng:
  kind: pcg32
  seed: ${seed_value}
""";

        var service = CreateTestService(templateYaml);
        var parameters = new Dictionary<string, object>
        {
            {"seed_value", 54321}
        };

        // Act
        var generatedYaml = await service.GenerateEngineModelAsync("rng-config-test", parameters);

        // Assert
        Assert.NotNull(generatedYaml);
        Assert.DoesNotContain("${seed_value}", generatedYaml);
        Assert.Contains("rng:", generatedYaml);
        Assert.Contains("kind: pcg32", generatedYaml);
        Assert.Contains("seed: 54321", generatedYaml);
    }

    private NodeBasedTemplateService CreateTestService(string templateYaml)
    {
        // Extract template ID from YAML
        var lines = templateYaml.Split('\n');
        var idLine = lines.FirstOrDefault(l => l.Trim().StartsWith("id:"));
        var templateId = "test-template";
        if (idLine != null)
        {
            var idValue = idLine.Split(':')[1].Trim();
            // Remove quotes if present
            templateId = idValue.Trim('\'', '"');
        }
        
    // Use preloaded YAML to avoid strict parsing prior to substitution
    var preloaded = new Dictionary<string, string>
    {
      [templateId] = templateYaml
    };
    return new NodeBasedTemplateService(preloaded, NullLogger<NodeBasedTemplateService>.Instance);
    }
}