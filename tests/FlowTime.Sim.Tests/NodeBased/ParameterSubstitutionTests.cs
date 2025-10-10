using System.IO;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

public class ParameterSubstitutionTests
{
    [Fact]
    public void Template_With_String_Parameter_Reference_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: systemName
    type: string
    title: System Name
    description: Name of the system component
    default: "ProductionSystem"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
    description: "Arrivals for ${systemName}"
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["systemName"] = "TestSystem"
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        var output = substitutedTemplate.Outputs.First(o => o.Id == "arrival_series");
        Assert.Equal("Arrivals for TestSystem", output.Description);
    }

    [Fact]
    public void Template_With_Integer_Parameter_Reference_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: binCount
    type: integer
    title: Number of Bins
    description: Number of time bins
    default: 5
    min: 1
    max: 100
grid:
  bins: ${binCount}
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["binCount"] = 10
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        Assert.Equal(10, substitutedTemplate.Grid.Bins);
    }

    [Fact]
    public void Template_With_Double_Parameter_Reference_In_PMF_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: baseValue
    type: number
    title: Base Value
    description: Base value for PMF
    default: 100.0
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: pmf
    pmf:
      values: [${baseValue}, 150.0, 200.0]
      probabilities: [0.5, 0.3, 0.2]
outputs:
  - id: arrival_series
    source: arrivals
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["baseValue"] = 75.0
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        var pmfNode = substitutedTemplate.Nodes.First(n => n.Id == "arrivals");
        Assert.NotNull(pmfNode.Pmf);
        Assert.Equal(75.0, pmfNode.Pmf.Values[0]);
        Assert.Equal(150.0, pmfNode.Pmf.Values[1]);
        Assert.Equal(200.0, pmfNode.Pmf.Values[2]);
    }

    [Fact]
    public void Template_With_Parameter_Reference_In_Expression_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: multiplier
    type: number
    title: Multiplier Factor
    description: Factor to multiply base values
    default: 1.5
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: base_load
    kind: const
    values: [100, 150, 200]
  - id: adjusted_load
    kind: expr
    expression: "base_load * ${multiplier}"
    dependencies: ["base_load"]
outputs:
  - id: load_series
    source: adjusted_load
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["multiplier"] = 2.0
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        var exprNode = substitutedTemplate.Nodes.First(n => n.Id == "adjusted_load");
        Assert.Equal("base_load * 2", exprNode.Expression);
    }

    [Fact]
    public void Template_With_Parameter_Reference_In_RNG_Seed_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: randomSeed
    type: integer
    title: Random Seed
    description: Seed for deterministic random generation
    default: 42
    min: 1
    max: 2147483647
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
rng:
  kind: pcg32
  seed: "${randomSeed}"
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["randomSeed"] = 12345
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        Assert.NotNull(substitutedTemplate.Rng);
        Assert.Equal("12345", substitutedTemplate.Rng.Seed);
    }

    [Fact]
    public void Template_With_Missing_Parameter_Reference_Throws_Exception()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: systemName
    type: string
    title: System Name
    default: "ProductionSystem"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
    description: "Arrivals for ${missingParam}"
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["systemName"] = "TestSystem"
        };

        Assert.Throws<ParameterSubstitutionException>(() => 
            ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues));
    }

    [Fact]
    public void Template_With_Default_Parameter_Values_Uses_Defaults_When_Not_Provided()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: systemName
    type: string
    title: System Name
    default: "DefaultSystem"
  - name: binCount
    type: integer
    title: Bin Count
    default: 5
grid:
  bins: ${binCount}
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
    description: "Arrivals for ${systemName}"
""";

        var parameterValues = new Dictionary<string, object>(); // Empty - should use defaults

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        Assert.Equal(5, substitutedTemplate.Grid.Bins);
        var output = substitutedTemplate.Outputs.First(o => o.Id == "arrival_series");
        Assert.Equal("Arrivals for DefaultSystem", output.Description);
    }

    [Fact]
    public void Template_With_Multiple_Parameter_References_In_Same_String_Substitutes_All()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
parameters:
  - name: environment
    type: string
    title: Environment
    default: "prod"
  - name: component
    type: string
    title: Component
    default: "api"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
    description: "Load for ${environment}-${component} system"
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["environment"] = "staging",
            ["component"] = "worker"
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);
        
        var output = substitutedTemplate.Outputs.First(o => o.Id == "arrival_series");
        Assert.Equal("Load for staging-worker system", output.Description);
    }

    [Fact(Skip = "Pending time-travel template fields (Window/Classes) integration into FlowTime.Sim.Core")]
    public void ParameterSubstitution_NestedObjects_Applies_To_Window_Start()
    {
        var yaml = File.ReadAllText("fixtures/templates/time-travel/topology_baseline.yaml");
        var parameterValues = new Dictionary<string, object>
        {
            ["startTimestamp"] = "2045-03-15T12:30:00Z"
        };

        dynamic substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);

        Assert.NotNull(substitutedTemplate.Window);
        Assert.Equal("2045-03-15T12:30:00Z", substitutedTemplate.Window.Start);
        Assert.Equal("UTC", substitutedTemplate.Window.Timezone);
        Assert.Single(substitutedTemplate.Classes);
        Assert.Equal("*", substitutedTemplate.Classes[0]);
    }
}
