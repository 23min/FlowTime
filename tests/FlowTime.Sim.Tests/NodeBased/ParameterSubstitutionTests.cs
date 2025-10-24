using System.Collections.Generic;
using System.Linq;
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
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: systemName
    type: string
    title: System Name
    default: "ProductionSystem"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
  - id: served
    kind: const
    values: [90, 120, 180]
outputs:
  - id: arrival_series
    series: arrivals
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
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: binCount
    type: integer
    title: Number of Bins
    default: 5
grid:
  bins: ${binCount}
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200, 250, 275]
  - id: served
    kind: const
    values: [90, 120, 180, 210, 240]
outputs:
  - id: arrival_series
    series: arrivals
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
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: baseValue
    type: number
    title: Base Value
    default: 100.0
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: pmf
    pmf:
      values: [${baseValue}, 150.0, 200.0]
      probabilities: [0.5, 0.3, 0.2]
  - id: served
    kind: const
    values: [80, 90, 100]
outputs:
  - id: arrival_series
    series: arrivals
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["baseValue"] = 75.0
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);

        var pmfNode = substitutedTemplate.Nodes.First(n => n.Id == "arrivals");
        Assert.NotNull(pmfNode.Pmf);
        Assert.Equal(75.0, pmfNode.Pmf!.Values[0]);
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
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: multiplier
    type: number
    title: Multiplier Factor
    default: 1.5
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: base_load
        served: adjusted_load
  edges: []
nodes:
  - id: base_load
    kind: const
    values: [100, 150, 200]
  - id: adjusted_load
    kind: expr
    expr: "base_load * ${multiplier}"
outputs:
  - id: load_series
    series: adjusted_load
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["multiplier"] = 2.0
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);

        var exprNode = substitutedTemplate.Nodes.First(n => n.Id == "adjusted_load");
        Assert.Equal("base_load * 2", exprNode.Expr);
    }

    [Fact]
    public void Template_With_Parameter_Reference_In_RNG_Seed_Substitutes_Value()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: randomSeed
    type: integer
    title: Random Seed
    default: 42
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
  - id: served
    kind: const
    values: [90, 140, 180]
outputs:
  - id: arrival_series
    series: arrivals
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
        Assert.Equal("12345", substitutedTemplate.Rng!.Seed);
    }

    [Fact]
    public void Template_With_Missing_Parameter_Reference_Throws_Exception()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: systemName
    type: string
    title: System Name
    default: "ProductionSystem"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
  - id: served
    kind: const
    values: [90, 120, 140]
outputs:
  - id: arrival_series
    series: arrivals
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
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
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
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200, 250, 300]
  - id: served
    kind: const
    values: [90, 120, 180, 210, 240]
outputs:
  - id: arrival_series
    series: arrivals
    description: "Arrivals for ${systemName}"
""";

        var parameterValues = new Dictionary<string, object>(); // Empty - should use defaults

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);

        Assert.Equal(5, substitutedTemplate.Grid.Bins);
        var output = substitutedTemplate.Outputs.First(o => o.Id == "arrival_series");
        Assert.Equal("Arrivals for DefaultSystem", output.Description);
    }

    [Fact]
    public void Array_Parameter_Substitution_Produces_Json_Array_Literals()
    {
        var yaml = """
metadata:
  id: array-substitution
  title: Array Substitution Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: baseLoad
    type: array
    arrayOf: double
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: service
      kind: service
      semantics:
        arrivals: base_requests
        served: base_requests
  edges: []
nodes:
  - id: base_requests
    kind: const
    values: ${baseLoad}
outputs:
  - id: base_series
    series: base_requests
""";

        var parameterValues = new Dictionary<string, object>
        {
            ["baseLoad"] = new[] { 10d, 20d, 30d }
        };

        var substitutedTemplate = ParameterSubstitution.ParseWithSubstitution(yaml, parameterValues);

        var constNode = substitutedTemplate.Nodes.First(n => n.Id == "base_requests");
        Assert.Equal(new[] { 10d, 20d, 30d }, constNode.Values);
    }

    [Fact]
    public void Template_With_Multiple_Parameter_References_In_Same_String_Substitutes_All()
    {
        var yaml = """
metadata:
  id: param-test
  title: Parameter Substitution Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: environment
    type: string
    default: "prod"
  - name: component
    type: string
    default: "api"
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ServiceNode
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
  - id: served
    kind: const
    values: [90, 130, 180]
outputs:
  - id: arrival_series
    series: arrivals
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
}
