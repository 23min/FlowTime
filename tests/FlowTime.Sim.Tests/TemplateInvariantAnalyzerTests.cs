using FlowTime.Sim.Core.Analysis;
using Xunit;

namespace FlowTime.Sim.Tests;

public sealed class TemplateInvariantAnalyzerTests
{
    [Fact]
    public void Analyze_DetectsServedExceedsArrivals()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 2
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: Delivery
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
nodes:
  - id: arrivals
    kind: const
    values: [10, 8]
  - id: served
    kind: const
    values: [12, 7]
  - id: errors
    kind: const
    values: [1, 1]
outputs: []
""";

        var result = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Code == "served_exceeds_arrivals");
    }

    [Fact]
    public void Analyze_FlagsRouterMissingClassRoute()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: alpha_source
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_source
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
topology:
  nodes:
    - id: RouterNode
      kind: service
      semantics:
        arrivals: source_total
        served: route_alpha
        errors: route_alpha
nodes:
  - id: alpha_source
    kind: const
    values: [5]
  - id: beta_source
    kind: const
    values: [5]
  - id: source_total
    kind: expr
    expr: alpha_source + beta_source
  - id: route_alpha
    kind: expr
    expr: source_total * 0.5
  - id: hub_router
    kind: router
    inputs:
      queue: source_total
    routes:
      - target: route_alpha
        classes: [Alpha]
outputs: []
""";

        var result = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.Contains(result.Warnings, w => w.Code == "router_missing_class_route" && w.NodeId == "hub_router");
    }

    [Fact]
    public void Analyze_RouterOverridesPreventClassLeakageWarning()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: alpha_source
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 1
    - nodeId: beta_source
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 1
topology:
  nodes:
    - id: RouterNode
      kind: service
      semantics:
        arrivals: source_total
        served: route_alpha
        errors: route_beta
nodes:
  - id: alpha_source
    kind: const
    values: [5]
  - id: beta_source
    kind: const
    values: [5]
  - id: source_total
    kind: expr
    expr: alpha_source + beta_source
  - id: route_alpha
    kind: const
    values: [8]
  - id: route_beta
    kind: const
    values: [1]
  - id: hub_router
    kind: router
    inputs:
      queue: source_total
    routes:
      - target: route_alpha
        classes: [Alpha]
      - target: route_beta
        classes: [Beta]
outputs: []
""";

        var result = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "router_class_leakage");
    }

    [Fact]
    public void Analyze_WarnsWhenDispatchScheduleNeverReleases()
    {
        var yaml = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: QueueNode
      kind: queue
      semantics:
        arrivals: queue_inflow
        served: queue_dispatch
        queueDepth: backlog_depth
nodes:
  - id: queue_inflow
    kind: const
    values: [5, 5, 5]
  - id: queue_dispatch
    kind: const
    values: [0, 0, 0]
  - id: backlog_depth
    kind: serviceWithBuffer
    inflow: queue_inflow
    outflow: queue_dispatch
    dispatchSchedule:
      periodBins: 4
      phaseOffset: 0
outputs: []
""";

        var result = TemplateInvariantAnalyzer.Analyze(yaml);

        Assert.Contains(result.Warnings, w => w.Code == "dispatch_never_releases" && w.NodeId == "QueueNode");
    }
}
