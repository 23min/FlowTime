using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

public class RngValidationTests
{
    [Fact]
    public void RNG_With_Valid_PCG32_Kind_Parses_Successfully()
    {
        // Arrange
        var yaml = @"
metadata:
  id: valid-rng
  title: Valid RNG Template

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: 12345

nodes:
  - id: test_node
    kind: const
    values: [1, 2, 3]

outputs:
  - id: test_output
    source: test_node
    filename: test.csv
";

        // Act
        var template = TemplateParser.ParseFromYaml(yaml);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("12345", template.Rng.Seed);
    }

    [Fact]
    public void RNG_With_String_Seed_Parses_Successfully()
    {
        // Arrange
        var yaml = @"
metadata:
  id: string-seed-rng
  title: String Seed RNG Template

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: ""42""

nodes:
  - id: test_node
    kind: const
    values: [1, 2, 3]

outputs:
  - id: test_output
    source: test_node
    filename: test.csv
";

        // Act
        var template = TemplateParser.ParseFromYaml(yaml);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("42", template.Rng.Seed);
    }

    [Fact]
    public void Template_Without_RNG_Has_Null_Rng_Property()
    {
        // Arrange
        var yaml = @"
metadata:
  id: no-rng
  title: No RNG Template

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

nodes:
  - id: test_node
    kind: const
    values: [1, 2, 3]

outputs:
  - id: test_output
    source: test_node
    filename: test.csv
";

        // Act
        var template = TemplateParser.ParseFromYaml(yaml);

        // Assert
        Assert.Null(template.Rng);
    }

    [Fact]
    public void RNG_With_Parameter_Substitution_For_Seed_Works()
    {
        // Arrange
        var yaml = @"
metadata:
  id: param-rng
  title: Parameterized RNG Template

parameters:
  - name: mySeed
    type: integer
    default: 999

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: ${mySeed}

nodes:
  - id: test_node
    kind: const
    values: [1, 2, 3]

outputs:
  - id: test_output
    source: test_node
    filename: test.csv
";
        var parameters = new Dictionary<string, object> { ["mySeed"] = 777 };

        // Act
        var template = ParameterSubstitution.ParseWithSubstitution(yaml, parameters);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("777", template.Rng.Seed);
    }

    [Fact]
    public void RNG_With_Parameter_Substitution_Uses_Default_When_Not_Provided()
    {
        // Arrange
        var yaml = @"
metadata:
  id: default-rng
  title: Default RNG Template

parameters:
  - name: defaultSeed
    type: integer
    default: 555

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: ${defaultSeed}

nodes:
  - id: test_node
    kind: const
    values: [1, 2, 3]

outputs:
  - id: test_output
    source: test_node
    filename: test.csv
";
        var parameters = new Dictionary<string, object>(); // Empty - should use default

        // Act
        var template = ParameterSubstitution.ParseWithSubstitution(yaml, parameters);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("555", template.Rng.Seed);
    }

    [Fact]
    public void Multiple_PMF_Nodes_With_RNG_Parse_Successfully()
    {
        // Arrange
        var yaml = @"
metadata:
  id: multi-pmf-rng
  title: Multiple PMF with RNG Template

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: 321

nodes:
  - id: reliability_factor
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.8]
      probabilities: [0.7, 0.2, 0.1]
      
  - id: performance_factor
    kind: pmf
    pmf:
      values: [0.8, 1.0, 1.2]
      probabilities: [0.1, 0.8, 0.1]
      
  - id: base_capacity
    kind: const
    values: [100, 120, 110]
    
  - id: effective_capacity
    kind: expr
    expression: ""base_capacity * reliability_factor * performance_factor""
    dependencies: [base_capacity, reliability_factor, performance_factor]

outputs:
  - id: effective_capacity
    source: effective_capacity
    filename: capacity.csv
";

        // Act
        var template = TemplateParser.ParseFromYaml(yaml);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("321", template.Rng.Seed);
        
        var pmfNodes = template.Nodes.Where(n => n.Kind == "pmf").ToList();
        Assert.Equal(2, pmfNodes.Count);
        
        // Verify both PMF nodes parsed correctly
        var reliabilityNode = pmfNodes.First(n => n.Id == "reliability_factor");
        Assert.NotNull(reliabilityNode.Pmf);
        Assert.Equal(1.0, reliabilityNode.Pmf.Probabilities.Sum(), 3);
        
        var performanceNode = pmfNodes.First(n => n.Id == "performance_factor");
        Assert.NotNull(performanceNode.Pmf);
        Assert.Equal(1.0, performanceNode.Pmf.Probabilities.Sum(), 3);
    }

    [Fact]
    public void PMF_Node_With_RNG_And_Expression_Integration_Parses_Successfully()
    {
        // Arrange
        var yaml = @"
metadata:
  id: pmf-expr-rng
  title: PMF Expression RNG Integration

grid:
  bins: 4
  binSize: 30
  binUnit: minutes

rng:
  kind: pcg32
  seed: 1001

nodes:
  - id: demand
    kind: const
    values: [50, 75, 100, 80]
    
  - id: supply_variability
    kind: pmf
    pmf:
      values: [0.7, 0.9, 1.0, 1.1, 1.3]
      probabilities: [0.05, 0.15, 0.6, 0.15, 0.05]
      
  - id: market_factor
    kind: pmf
    pmf:
      values: [0.8, 1.0, 1.2]
      probabilities: [0.2, 0.6, 0.2]
      
  - id: actual_supply
    kind: expr
    expression: ""demand * supply_variability""
    dependencies: [demand, supply_variability]
    
  - id: market_demand
    kind: expr
    expression: ""demand * market_factor""
    dependencies: [demand, market_factor]
    
  - id: unmet_demand
    kind: expr
    expression: ""MAX(0, market_demand - actual_supply)""
    dependencies: [market_demand, actual_supply]
    
  - id: surplus
    kind: expr
    expression: ""MAX(0, actual_supply - market_demand)""
    dependencies: [actual_supply, market_demand]

outputs:
  - id: actual_supply
    source: actual_supply
    filename: supply.csv
  - id: unmet_demand
    source: unmet_demand
    filename: unmet.csv
  - id: surplus
    source: surplus
    filename: surplus.csv
";

        // Act
        var template = TemplateParser.ParseFromYaml(yaml);

        // Assert
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("1001", template.Rng.Seed);
        
        // Verify PMF nodes
        var pmfNodes = template.Nodes.Where(n => n.Kind == "pmf").ToList();
        Assert.Equal(2, pmfNodes.Count);
        
        // Verify expression nodes that use PMF values
        var exprNodes = template.Nodes.Where(n => n.Kind == "expr").ToList();
        Assert.Equal(4, exprNodes.Count);
        
        var actualSupplyNode = exprNodes.First(n => n.Id == "actual_supply");
        Assert.Contains("supply_variability", actualSupplyNode.Expression);
        
        var marketDemandNode = exprNodes.First(n => n.Id == "market_demand");
        Assert.Contains("market_factor", marketDemandNode.Expression);
    }

    [Fact]
    public void PMF_Probabilities_Must_Sum_To_One_With_RNG()
    {
        // Arrange
        var yaml = @"
metadata:
  id: valid-pmf-sum
  title: Valid PMF Sum

grid:
  bins: 2
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: 123

nodes:
  - id: test_pmf
    kind: pmf
    pmf:
      values: [1, 2, 3]
      probabilities: [0.3, 0.3, 0.4]

outputs:
  - id: test_output
    source: test_pmf
    filename: test.csv
";

        // Act & Assert
        var template = TemplateParser.ParseFromYaml(yaml);
        
        var pmfNode = template.Nodes.First(n => n.Kind == "pmf");
        Assert.NotNull(pmfNode.Pmf);
        
        var sum = pmfNode.Pmf.Probabilities.Sum();
        Assert.True(Math.Abs(sum - 1.0) < 0.001, $"PMF probabilities sum to {sum}, expected 1.0");
    }
}