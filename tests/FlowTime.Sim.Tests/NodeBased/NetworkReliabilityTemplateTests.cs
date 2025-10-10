using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

public class NetworkReliabilityTemplateTests
{
    [Fact]
    public void NetworkReliabilityTemplate_CanBeLoadedWithParameterSubstitution()
    {
        // Arrange
        var yaml = @"
metadata:
  id: 'network-reliability'
  title: 'Network Reliability Test'
  description: 'Test template for RNG functionality'
  tags: [test, rng]

parameters:
  - name: bins
    type: integer
    default: 12
  - name: rngSeed
    type: integer
    default: 42

grid:
  bins: ${bins}
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: ${rngSeed}

nodes:
  - id: base_requests
    kind: const
    values: [100, 120, 150]
    
  - id: network_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.7]
      probabilities: [0.7, 0.2, 0.1]

outputs:
  - id: base_requests
    source: base_requests
    filename: requests.csv
";
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 6,
            ["rngSeed"] = 123
        };

        // Act
        var template = ParameterSubstitution.ParseWithSubstitution(yaml, parameters);

        // Assert
        Assert.Equal("network-reliability", template.Metadata.Id);
        Assert.Equal(6, template.Grid.Bins);
        Assert.NotNull(template.Rng);
        Assert.Equal("pcg32", template.Rng.Kind);
        Assert.Equal("123", template.Rng.Seed);
        
        // Check PMF nodes exist
        var pmfNodes = template.Nodes.Where(n => n.Kind == "pmf").ToList();
        Assert.True(pmfNodes.Count >= 1, "Should have at least 1 PMF node");
        
        // Check const nodes exist
        var constNodes = template.Nodes.Where(n => n.Kind == "const").ToList();
        Assert.True(constNodes.Count >= 1, "Should have at least 1 const node");
    }

    [Fact]
    public void NetworkReliabilityTemplate_HasCorrectPmfProbabilities()
    {
        // Arrange
        var yaml = @"
metadata:
  id: 'pmf-test'
  title: 'PMF Test'

grid:
  bins: 3
  binSize: 60
  binUnit: minutes

rng:
  kind: pcg32
  seed: 42

nodes:
  - id: pmf_node
    kind: pmf
    pmf:
      values: [1.0, 0.5, 0.2]
      probabilities: [0.6, 0.3, 0.1]

outputs:
  - id: pmf_node
    source: pmf_node
    filename: pmf.csv
";
        var parameters = new Dictionary<string, object>();

        // Act
        var template = ParameterSubstitution.ParseWithSubstitution(yaml, parameters);

        // Assert - All PMF nodes should have probabilities that sum to 1.0
        var pmfNodes = template.Nodes.Where(n => n.Kind == "pmf").ToList();
        Assert.True(pmfNodes.Count >= 1, "Should have at least 1 PMF node");
        
        foreach (var pmfNode in pmfNodes)
        {
            if (pmfNode.Pmf != null)
            {
                var sum = pmfNode.Pmf.Probabilities.Sum();
                Assert.True(Math.Abs(sum - 1.0) < 0.001, 
                    $"PMF node {pmfNode.Id} probabilities sum to {sum}, expected 1.0");
            }
        }
    }
}