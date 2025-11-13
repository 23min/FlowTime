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
}
