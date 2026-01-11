using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowTime.Contracts.Services;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public sealed class SinkNodeTemplateTests
{
    [Fact]
    public async Task SinkNode_ServedEqualsArrivals()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sink-kind-template
  title: Sink Kind Template
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
    - id: TerminalSuccess
      kind: sink
      semantics:
        arrivals: arrivals
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 20, 30]
outputs:
  - series: "*"
""";

        var templates = new Dictionary<string, string> { ["sink-kind-template"] = yaml };
        var templateService = new TemplateService(templates, NullLogger<TemplateService>.Instance);
        var modelYaml = await templateService.GenerateEngineModelAsync("sink-kind-template", new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(modelYaml);
        var (grid, graph) = ModelParser.ParseModel(model);
        var series = graph.Evaluate(grid);

        var node = Assert.Single(model.Topology!.Nodes, n => n.Id == "TerminalSuccess");
        var arrivalsId = node.Semantics.Arrivals;
        var servedId = node.Semantics.Served;
        var errorsId = node.Semantics.Errors;

        var arrivals = series[new NodeId(arrivalsId)].ToArray();
        var served = series[new NodeId(servedId)].ToArray();
        var errors = series[new NodeId(errorsId)].ToArray();

        Assert.Equal(arrivals, served);
        Assert.All(errors, value => Assert.Equal(0d, value));
    }

    [Fact]
    public async Task SinkNode_AllowsRefusedArrivalsAndDerivesServed()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sink-refused-template
  title: Sink Refused Template
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
    - id: TerminalSuccess
      kind: sink
      semantics:
        arrivals: arrivals
        errors: refused
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10]
  - id: refused
    kind: const
    values: [2, 0, 5]
outputs:
  - series: "*"
""";

        var templates = new Dictionary<string, string> { ["sink-refused-template"] = yaml };
        var templateService = new TemplateService(templates, NullLogger<TemplateService>.Instance);
        var modelYaml = await templateService.GenerateEngineModelAsync("sink-refused-template", new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(modelYaml);
        var (grid, graph) = ModelParser.ParseModel(model);
        var series = graph.Evaluate(grid);

        var node = Assert.Single(model.Topology!.Nodes, n => n.Id == "TerminalSuccess");
        var arrivals = series[new NodeId(node.Semantics.Arrivals)].ToArray();
        var errors = series[new NodeId(node.Semantics.Errors)].ToArray();
        var served = series[new NodeId(node.Semantics.Served)].ToArray();

        Assert.Equal(new[] { 10d, 10d, 10d }, arrivals);
        Assert.Equal(new[] { 2d, 0d, 5d }, errors);
        Assert.Equal(new[] { 8d, 10d, 5d }, served);
    }

    [Fact]
    public async Task SinkNode_DispatchSchedule_IsPreservedInGeneratedModel()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sink-schedule-template
  title: Sink Schedule Template
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
    - id: TerminalSuccess
      kind: sink
      dispatchSchedule:
        kind: time-based
        periodBins: 2
        phaseOffset: 0
      semantics:
        arrivals: arrivals
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 10]
outputs:
  - series: "*"
""";

        var templates = new Dictionary<string, string> { ["sink-schedule-template"] = yaml };
        var templateService = new TemplateService(templates, NullLogger<TemplateService>.Instance);
        var modelYaml = await templateService.GenerateEngineModelAsync("sink-schedule-template", new Dictionary<string, object>());
        var model = ModelService.ParseAndConvert(modelYaml);

        var node = Assert.Single(model.Topology!.Nodes, n => n.Id == "TerminalSuccess");
        Assert.NotNull(node.DispatchSchedule);
        Assert.Equal(2, node.DispatchSchedule!.PeriodBins);
        Assert.Equal(0, node.DispatchSchedule.PhaseOffset);
        Assert.Equal("time-based", node.DispatchSchedule.Kind);
    }
}
