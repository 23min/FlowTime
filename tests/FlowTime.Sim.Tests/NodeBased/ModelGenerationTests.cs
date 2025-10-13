using System;
using System.Collections.Generic;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class ModelGenerationTests
{
    [Fact]
    public async Task GenerateModelAsync_WithConstNodes_EmbedsProvenance()
    {
        var templateYaml = """
metadata:
  id: const-model-test
  title: Constant Node Model Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
parameters:
  - name: arrival_rate
    type: number
    default: 100
grid:
  bins: 4
  binSize: 30
  binUnit: minutes
topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [${arrival_rate}, 150, 200, 120]
  - id: served
    kind: const
    values: [95, 120, 180, 100]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "const-model-test");
        var parameters = new Dictionary<string, object>
        {
            {"arrival_rate", 80}
        };

        var generatedYaml = await service.GenerateEngineModelAsync("const-model-test", parameters);

        var model = DeserializeArtifact(generatedYaml);

        Assert.Equal(1, model.SchemaVersion);
        Assert.Equal("flowtime-sim", model.Generator);
        Assert.Equal("simulation", model.Mode);
        Assert.Equal("const-model-test", model.Metadata.Id);
        Assert.Equal("Constant Node Model Test", model.Metadata.Title);
        Assert.Equal("2025-01-01T00:00:00Z", model.Window.Start);
        Assert.Equal("UTC", model.Window.Timezone);

        Assert.Equal(4, model.Grid.Bins);
        Assert.Equal(30, model.Grid.BinSize);
        Assert.Equal("minutes", model.Grid.BinUnit);

        Assert.Contains(model.Nodes, n => n.Id == "arrivals" && n.Values![0] == 80);
        Assert.Single(model.Outputs);
        Assert.Equal("*", model.Outputs[0].Series);

        Assert.Equal("flowtime-sim", model.Provenance.Source);
        Assert.Equal("const-model-test", model.Provenance.TemplateId);
        Assert.Equal("1.0.0", model.Provenance.TemplateVersion);
        Assert.Equal("simulation", model.Provenance.Mode);
        Assert.StartsWith("flowtime-sim/", model.Provenance.Generator);
        Assert.True(model.Provenance.Parameters.TryGetValue("arrival_rate", out var arrivalRate));
        Assert.Equal(80, Convert.ToInt32(arrivalRate));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.ModelId));
    }

    [Fact]
    public async Task GenerateModelAsync_WithPmfNodes_PreservesSeries()
    {
        var templateYaml = """
metadata:
  id: pmf-model-test
  title: PMF Node Model Test
  version: 1.0.0
window:
  start: 2025-02-01T00:00:00Z
  timezone: UTC
parameters:
  - name: high_prob
    type: number
    default: 0.2
grid:
  bins: 3
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: OrderService
      kind: queue
      semantics:
        arrivals: stochastic_demand
        served: baseline
        queue: queue_depth
      initialCondition:
        queueDepth: 0
  edges: []
nodes:
  - id: stochastic_demand
    kind: pmf
    pmf:
      values: [10, 20, 30]
      probabilities: [0.2, ${high_prob}, 0.6]
  - id: baseline
    kind: const
    values: [12, 12, 12]
  - id: queue_depth
    kind: const
    values: [0, 0, 0]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "pmf-model-test");
        var parameters = new Dictionary<string, object>
        {
            {"high_prob", 0.2}
        };

        var generatedYaml = await service.GenerateEngineModelAsync("pmf-model-test", parameters);

        var model = DeserializeArtifact(generatedYaml);

        var pmfNode = model.Nodes.Single(n => n.Id == "stochastic_demand");
        Assert.NotNull(pmfNode.Pmf);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, pmfNode.Pmf!.Values);
        Assert.Equal(0.2, pmfNode.Pmf.Probabilities[0]);
        Assert.Equal(0.2, pmfNode.Pmf.Probabilities[1]);
        Assert.Equal(0.6, pmfNode.Pmf.Probabilities[2]);

        Assert.Equal("simulation", model.Mode);
        Assert.Equal("pmf-model-test", model.Metadata.Id);
        Assert.True(model.Provenance.Parameters.TryGetValue("high_prob", out var highProb));
        Assert.Equal(0.2, Convert.ToDouble(highProb, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GenerateModelAsync_WithExpressionNode_SubstitutesParameters()
    {
        var templateYaml = """
metadata:
  id: expr-model-test
  title: Expression Node Model Test
  version: 1.0.0
window:
  start: 2025-03-01T00:00:00Z
  timezone: UTC
parameters:
  - name: efficiency
    type: number
    default: 0.8
grid:
  bins: 4
  binSize: 15
  binUnit: minutes
topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: demand
        served: served
  edges: []
nodes:
  - id: demand
    kind: const
    values: [100, 150, 200, 120]
  - id: capacity
    kind: const
    values: [180, 180, 180, 180]
  - id: served
    kind: expr
    expr: "MIN(demand, capacity * ${efficiency})"
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "expr-model-test");
        var parameters = new Dictionary<string, object>
        {
            {"efficiency", 0.9}
        };

        var generatedYaml = await service.GenerateEngineModelAsync("expr-model-test", parameters);

        var model = DeserializeArtifact(generatedYaml);
        var exprNode = model.Nodes.Single(n => n.Id == "served");
        Assert.Equal("expr", exprNode.Kind);
        Assert.Equal("MIN(demand, capacity * 0.9)", exprNode.Expr);
        Assert.Equal("simulation", model.Mode);
        Assert.True(model.Provenance.Parameters.TryGetValue("efficiency", out var efficiency));
        Assert.Equal(0.9, Convert.ToDouble(efficiency, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static TemplateService CreateTestService(string templateYaml, string templateId)
    {
        var templates = new Dictionary<string, string>
        {
            { templateId, templateYaml }
        };

        return new TemplateService(templates, NullLogger<TemplateService>.Instance);
    }

    private static SimModelArtifact DeserializeArtifact(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<SimModelArtifact>(yaml);
    }
}
