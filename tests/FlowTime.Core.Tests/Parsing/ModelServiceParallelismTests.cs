using FlowTime.Contracts.Services;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Parsing;

public class ModelServiceParallelismTests
{
    [Fact]
    public void ParseYaml_ParsesTopologyParallelismAsTypedLiteral()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: DispatchQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: dispatch_arrivals
        served: dispatch_served
        parallelism: 3
  edges: []
nodes:
  - id: dispatch_arrivals
    kind: const
    values: [10, 10, 10]
  - id: dispatch_served
    kind: const
    values: [8, 8, 8]
outputs:
  - series: "*"
""";

        var model = ModelService.ParseYaml(yaml);

        var parallelism = Assert.IsType<ParallelismReference>(model.Topology!.Nodes[0].Semantics.Parallelism);
        Assert.Equal(3d, parallelism.Constant);
        Assert.Null(parallelism.SeriesReference);
    }

    [Fact]
    public void ParseYaml_ParsesTopologyParallelismAsTypedSeriesReference()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
topology:
  nodes:
    - id: DispatchQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: dispatch_arrivals
        served: dispatch_served
        parallelism: worker_count
  edges: []
nodes:
  - id: dispatch_arrivals
    kind: const
    values: [10, 10, 10]
  - id: dispatch_served
    kind: const
    values: [8, 8, 8]
  - id: worker_count
    kind: const
    values: [2, 3, 4]
outputs:
  - series: "*"
""";

        var model = ModelService.ParseYaml(yaml);

        var parallelism = Assert.IsType<ParallelismReference>(model.Topology!.Nodes[0].Semantics.Parallelism);
        Assert.NotNull(parallelism.SeriesReference);
        Assert.Equal("worker_count", parallelism.SeriesReference!.RawText);
        Assert.Equal("worker_count", parallelism.SeriesReference.NodeId);
        Assert.Null(parallelism.Constant);
    }
}