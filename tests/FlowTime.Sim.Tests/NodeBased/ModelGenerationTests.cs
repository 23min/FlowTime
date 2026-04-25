using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Contracts.Dtos;
using FlowTime.Contracts.Services;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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

        // m-E24-02: top-level leaked-state fields (generator/mode/metadata/window) gone;
        // their meaningful subset surfaces inside provenance (Q5/A4) or grid.start (window.start).
        Assert.Equal(1, model.SchemaVersion);

        Assert.Equal(4, model.Grid.Bins);
        Assert.Equal(30, model.Grid.BinSize);
        Assert.Equal("minutes", model.Grid.BinUnit);
        Assert.Equal("2025-01-01T00:00:00Z", model.Grid.Start);

        Assert.Contains(model.Nodes, n => n.Id == "arrivals" && n.Values![0] == 80);
        Assert.Equal(3, model.Outputs.Count);
        Assert.Contains(model.Outputs, o => o.Series == "*");
        Assert.Contains(model.Outputs, o => o.Series == "arrivals");
        Assert.Contains(model.Outputs, o => o.Series == "served");

        Assert.NotNull(model.Provenance);
        Assert.Equal("const-model-test", model.Provenance!.TemplateId);
        Assert.Equal("1.0.0", model.Provenance.TemplateVersion);
        Assert.Equal("simulation", model.Provenance.Mode);
        Assert.StartsWith("flowtime-sim/", model.Provenance.Generator);
        Assert.NotNull(model.Provenance.Parameters);
        Assert.True(model.Provenance.Parameters!.TryGetValue("arrival_rate", out var arrivalRate));
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
        queueDepth: queue_depth
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

        // m-E24-02: top-level mode/metadata gone; survives in provenance.* (Q5/A4).
        Assert.NotNull(model.Provenance);
        Assert.Equal("simulation", model.Provenance!.Mode);
        Assert.Equal("pmf-model-test", model.Provenance.TemplateId);
        Assert.Equal("2025-02-01T00:00:00Z", model.Grid.Start);
        Assert.NotNull(model.Provenance.Parameters);
        Assert.True(model.Provenance.Parameters!.TryGetValue("high_prob", out var highProb));
        Assert.Equal(0.2, Convert.ToDouble(highProb, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GenerateModelAsync_WithBuiltinProfile_ExpandsPmfToConstSeries()
    {
        var templateYaml = """
metadata:
  id: pmf-profile-builtin
  title: PMF Profile Builtin Test
  version: 1.0.0
window:
  start: 2025-04-01T00:00:00Z
  timezone: UTC
grid:
  bins: 12
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: ProfiledService
      kind: service
      semantics:
        arrivals: profiled_demand
        served: baseline
  edges: []
nodes:
  - id: profiled_demand
    kind: pmf
    pmf:
      values: [50, 100]
      probabilities: [0.4, 0.6]
    profile:
      kind: builtin
      name: weekday-office
  - id: baseline
    kind: const
    values: [80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "pmf-profile-builtin");
        var generatedYaml = await service.GenerateEngineModelAsync("pmf-profile-builtin", new Dictionary<string, object>());
        var model = DeserializeArtifact(generatedYaml);

        var profiledNode = model.Nodes.Single(n => n.Id == "profiled_demand");
        Assert.Equal("const", profiledNode.Kind);
        Assert.NotNull(profiledNode.Values);
        Assert.Equal(12, profiledNode.Values!.Length);
        Assert.NotNull(profiledNode.Metadata);
        Assert.NotNull(profiledNode.Pmf);
        Assert.Equal(new[] { 50d, 100d }, profiledNode.Pmf!.Values);
        Assert.Equal(new[] { 0.4d, 0.6d }, profiledNode.Pmf.Probabilities);

        var expectedMean = 50 * 0.4 + 100 * 0.6;
        var average = profiledNode.Values.Average();
        Assert.InRange(average, expectedMean - 0.01, expectedMean + 0.01);
        Assert.True(profiledNode.Values.Max() > profiledNode.Values.Min());

        Assert.True(profiledNode.Metadata!.TryGetValue("profile.kind", out var profileKind));
        Assert.Equal("builtin", profileKind);
        Assert.Equal("weekday-office", profiledNode.Metadata!["profile.name"]);
        Assert.Equal("pmf", profiledNode.Metadata["origin.kind"]);
    }

    [Fact]
    public async Task GenerateModelAsync_WithInlineProfile_UsesProvidedWeights()
    {
        var templateYaml = """
metadata:
  id: pmf-profile-inline
  title: PMF Profile Inline Test
  version: 1.0.0
window:
  start: 2025-04-02T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: InlineService
      kind: service
      semantics:
        arrivals: inline_profiled
        served: inline_capacity
  edges: []
nodes:
  - id: inline_profiled
    kind: pmf
    pmf:
      values: [20, 40]
      probabilities: [0.5, 0.5]
    profile:
      kind: inline
      weights: [0.5, 1.5, 1.0]
  - id: inline_capacity
    kind: const
    values: [40, 40, 40]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "pmf-profile-inline");
        var generatedYaml = await service.GenerateEngineModelAsync("pmf-profile-inline", new Dictionary<string, object>());
        var model = DeserializeArtifact(generatedYaml);

        var profiledNode = model.Nodes.Single(n => n.Id == "inline_profiled");
        Assert.Equal("const", profiledNode.Kind);
        Assert.NotNull(profiledNode.Values);
        Assert.Equal(3, profiledNode.Values!.Length);
        Assert.NotNull(profiledNode.Pmf);
        Assert.Equal(new[] { 20d, 40d }, profiledNode.Pmf!.Values);
        Assert.Equal(new[] { 0.5d, 0.5d }, profiledNode.Pmf.Probabilities);

        var expectedMean = 30d;
        var expectedSeries = new[] { expectedMean * 0.5, expectedMean * 1.5, expectedMean * 1.0 };
        for (int i = 0; i < expectedSeries.Length; i++)
        {
            Assert.Equal(expectedSeries[i], profiledNode.Values[i], 6);
        }

        Assert.NotNull(profiledNode.Metadata);
        Assert.True(profiledNode.Metadata!.TryGetValue("profile.kind", out var profileKind));
        Assert.Equal("inline", profileKind);
        Assert.Equal("pmf", profiledNode.Metadata!["origin.kind"]);
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
        // m-E24-02: top-level mode gone; survives in provenance.mode.
        Assert.NotNull(model.Provenance);
        Assert.Equal("simulation", model.Provenance!.Mode);
        Assert.NotNull(model.Provenance.Parameters);
        Assert.True(model.Provenance.Parameters!.TryGetValue("efficiency", out var efficiency));
        Assert.Equal(0.9, Convert.ToDouble(efficiency, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GenerateModelAsync_WithTelemetryMode_PopulatesSourcesAndTelemetryMetadata()
    {
        var templateYaml = """
metadata:
  id: telemetry-model-test
  title: Telemetry Model Test
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 2
  binSize: 5
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
    values: [120, 130]
    source: file://telemetry/order-service_arrivals.csv
  - id: served
    kind: const
    values: [110, 115]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "telemetry-model-test");

        var generatedYaml = await service.GenerateEngineModelAsync(
            "telemetry-model-test",
            new Dictionary<string, object>(),
            TemplateMode.Telemetry);

        var model = DeserializeArtifact(generatedYaml);

        // m-E24-02: top-level mode gone; survives in provenance.mode (Q5/A4).
        Assert.NotNull(model.Provenance);
        Assert.Equal("telemetry", model.Provenance!.Mode);
        // Per Q4: nodes[].source is dropped from emission; not declared on NodeDto.
        // Verify the arrivals node exists (without checking Source — it no longer exists).
        Assert.Contains(model.Nodes, n => n.Id == "arrivals");
        Assert.Contains(model.Nodes, n => n.Id == "served" && n.Values is { Length: 2 });
        // Empty parameter sets serialize as YAML omission (D-m-E24-02-03 — nullable +
        // OmitNull). Allow either null or empty dictionary on the round-trip.
        Assert.True(model.Provenance.Parameters is null || model.Provenance.Parameters.Count == 0);
    }

    [Fact]
    public async Task GenerateModelAsync_PreservesServiceTimeSemantics()
    {
        var templateYaml = """
metadata:
  id: service-time-model-test
  title: Service Time Semantic Preservation
  version: 1.0.0
window:
  start: 2025-04-01T00:00:00Z
  timezone: UTC
grid:
  bins: 2
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: IncidentIntake
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        processingTimeMsSum: processing_sum
        servedCount: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 12]
  - id: served
    kind: const
    values: [9, 11]
  - id: processing_sum
    kind: const
    values: [4500, 5100]
outputs:
  - series: "*"
""";

        var service = CreateTestService(templateYaml, "service-time-model-test");
        var generatedYaml = await service.GenerateEngineModelAsync(
            "service-time-model-test",
            new Dictionary<string, object>());

        var model = DeserializeArtifact(generatedYaml);
        Assert.NotNull(model.Topology);
        var node = Assert.Single(model.Topology!.Nodes, n => n.Id == "IncidentIntake");
        Assert.Equal("processing_sum", node.Semantics.ProcessingTimeMsSum);
        Assert.Equal("served", node.Semantics.ServedCount);
    }

    private static TemplateService CreateTestService(string templateYaml, string templateId)
    {
        var templates = new Dictionary<string, string>
        {
            { templateId, templateYaml }
        };

        return new TemplateService(templates, NullLogger<TemplateService>.Instance);
    }

    // m-E24-02: deserialize into the unified ModelDto.
    // Uses IgnoreUnmatchedProperties so any residual leaked-state fields would be
    // tolerated; assertions below pin the new shape (Q5/A4 — provenance.mode etc.).
    private static ModelDto DeserializeArtifact(string yaml) => ModelService.ParseYaml(yaml);
}
