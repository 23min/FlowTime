using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class TemplateParserTests
{
    [Fact]
    public void Template_With_Const_Node_Parses_Successfully()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: simple-const-template
  title: Simple Constant Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
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
    values: [100, 150, 200]
  - id: served
    kind: const
    values: [90, 120, 180]
outputs:
  - series: "*"
""";

        var template = TemplateParser.ParseFromYaml(yaml);

        Assert.Equal("simple-const-template", template.Metadata.Id);
        Assert.Equal("Simple Constant Template", template.Metadata.Title);
        Assert.Equal("1.0.0", template.Metadata.Version);
        Assert.Equal("2025-01-01T00:00:00Z", template.Window.Start);
        Assert.Equal("UTC", template.Window.Timezone);
        Assert.Equal(3, template.Grid.Bins);
        Assert.Equal(60, template.Grid.BinSize);
        Assert.Equal("minutes", template.Grid.BinUnit);

        Assert.Equal(2, template.Nodes.Count);
        Assert.Equal(new[] { 100.0, 150.0, 200.0 }, template.Nodes[0].Values);

        Assert.Single(template.Topology.Nodes);
        var topologyNode = template.Topology.Nodes[0];
        Assert.Equal("OrderService", topologyNode.Id);
        Assert.Equal("arrivals", topologyNode.Semantics.Arrivals);
        Assert.Equal("served", topologyNode.Semantics.Served);

        Assert.Single(template.Outputs);
        Assert.Equal("*", template.Outputs[0].Series);
    }

    [Fact]
    public void Template_With_Pmf_Node_Preserves_Pmf_Definition()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: pmf-template
  title: PMF Template
  version: 2.0.0
window:
  start: 2025-02-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 15
  binUnit: minutes
topology:
  nodes:
    - id: OrderService
      kind: queue
      semantics:
        arrivals: stochastic_arrivals
        served: served
        queueDepth: queue_depth
      initialCondition:
        queueDepth: 0
  edges: []
nodes:
  - id: stochastic_arrivals
    kind: pmf
    pmf:
      values: [10, 20, 30]
      probabilities: [0.3, 0.5, 0.2]
  - id: served
    kind: const
    values: [5, 5, 5, 5]
  - id: queue_depth
    kind: const
    values: [0, 0, 0, 0]
outputs:
  - series: "*"
""";

        var template = TemplateParser.ParseFromYaml(yaml);

        Assert.Equal("pmf-template", template.Metadata.Id);
        Assert.Equal(4, template.Grid.Bins);

        var pmfNode = template.Nodes.Find(n => n.Id == "stochastic_arrivals");
        Assert.NotNull(pmfNode);
        Assert.Equal("pmf", pmfNode!.Kind);
        Assert.NotNull(pmfNode.Pmf);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, pmfNode.Pmf!.Values);
        Assert.Equal(new[] { 0.3, 0.5, 0.2 }, pmfNode.Pmf.Probabilities);
    }

    [Fact]
    public void Template_With_Invalid_Profile_Length_Fails_Validation()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: pmf-profile-invalid
  title: PMF Profile Invalid
  version: 1.0.0
window:
  start: 2025-05-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ProfileService
      kind: service
      semantics:
        arrivals: profiled
        served: served
  edges: []
nodes:
  - id: profiled
    kind: pmf
    pmf:
      values: [10, 20]
      probabilities: [0.5, 0.5]
    profile:
      kind: inline
      weights: [0.5, 1.5]
  - id: served
    kind: const
    values: [10, 10, 10]
outputs:
  - series: "*"
""";

        Assert.Throws<TemplateValidationException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Template_With_Parameters_Populates_Collection()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: parameterized-template
  title: Parameterized Template
  version: 1.0.0
window:
  start: 2025-03-01T00:00:00Z
  timezone: UTC
parameters:
  - name: arrival_rate
    type: number
    title: Arrival Rate
    default: 100
    min: 0
    max: 1000
  - name: queue_start
    type: number
    title: Queue Start
    default: 5
grid:
  bins: 5
  binSize: 60
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
    values: [100, 100, 100, 100, 100]
  - id: served
    kind: const
    values: [80, 90, 85, 88, 82]
outputs:
  - series: "*"
""";

        var template = TemplateParser.ParseFromYaml(yaml);

        Assert.Equal(2, template.Parameters.Count);
        Assert.Contains(template.Parameters, p => p.Name == "arrival_rate" && (p.Default?.ToString() ?? string.Empty) == "100");
        Assert.Contains(template.Parameters, p => p.Name == "queue_start" && (p.Default?.ToString() ?? string.Empty) == "5");
    }

    [Fact]
    public void Expression_Node_With_SelfShift_Without_Topology_Initial_Throws()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: queue-template
  title: Queue Template
  version: 1.0.0
window:
  start: 2025-04-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: QueueNode
      kind: queue
      semantics:
        arrivals: arrivals
        served: served
        queueDepth: queue_depth
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 20, 15]
  - id: served
    kind: const
    values: [8, 18, 14]
  - id: queue_depth
    kind: expr
    expr: "SHIFT(queue_depth, 1) + arrivals - served"
outputs:
  - series: "*"
""";

        Assert.Throws<TemplateValidationException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Invalid_Node_Kind_Throws_Exception()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: invalid-node-template
  title: Invalid Node Template
  version: 1.0.0
window:
  start: 2025-05-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: NodeA
      kind: service
      semantics:
        arrivals: invalid_node
        served: served
  edges: []
nodes:
  - id: invalid_node
    kind: invalid_kind
    values: [100]
  - id: served
    kind: const
    values: [50]
outputs:
  - series: "*"
""";

        Assert.Throws<TemplateValidationException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Output_Referencing_Unknown_Node_Throws()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: invalid-output-template
  title: Invalid Output Template
  version: 1.0.0
window:
  start: 2025-06-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: NodeA
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [100]
  - id: served
    kind: const
    values: [90]
outputs:
  - series: unknown
""";

        Assert.Throws<TemplateValidationException>(() => TemplateParser.ParseFromYaml(yaml));
    }
}
