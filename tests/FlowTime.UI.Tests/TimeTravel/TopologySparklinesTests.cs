using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologySparklinesTests
{
    [Fact]
    public void BuildNodeSparklinesSynthesizesDistributionForPmfNodes()
    {
        var metadata = new TimeTravelStateMetadataDto
        {
            RunId = "run",
            TemplateId = "template",
            Mode = "full",
            Schema = new TimeTravelSchemaMetadataDto { Id = "schema", Version = "1.0.0", Hash = "hash" },
            Storage = new TimeTravelStorageDescriptorDto()
        };

        var window = new TimeTravelStateWindowDto
        {
            Metadata = metadata,
            Window = new TimeTravelWindowSliceDto { StartBin = 0, EndBin = 0, BinCount = 1 },
            TimestampsUtc = Array.Empty<DateTimeOffset>(),
            Nodes = Array.Empty<TimeTravelNodeSeriesDto>()
        };

        var pmfNode = new TopologyNode(
            "distribution_node",
            "pmf",
            Array.Empty<string>(),
            Array.Empty<string>(),
            0,
            0,
            0,
            0,
            false,
            new TopologyNodeSemantics(
                Arrivals: null,
                Served: null,
                Errors: null,
                Queue: null,
                Capacity: null,
                Series: null,
                Expression: null,
                Distribution: new TopologyNodeDistribution(
                    new double[] { 1, 2, 3 },
                    new double[] { 0.2, 0.3, 0.5 }),
                InlineValues: null));

        var graph = new TopologyGraph(
            new[] { pmfNode },
            Array.Empty<TopologyEdge>());

        var topology = new Topology();
        topology.TestSetWindowData(window);
        topology.TestSetTopologyGraph(graph);
        topology.TestBuildNodeSparklines();

        var map = topology.TestGetNodeSparklines();
        Assert.True(map.ContainsKey(pmfNode.Id));

        var sparkline = map[pmfNode.Id];
        Assert.Equal(0d, sparkline.Min);
        Assert.Equal(1d, sparkline.Max);
        Assert.True(sparkline.Series.ContainsKey("distribution"));
        Assert.True(sparkline.Values.Count > 0);
        Assert.True(sparkline.Values[^1].HasValue);
        Assert.All(sparkline.Values.Take(sparkline.Values.Count - 1), value => Assert.False(value.HasValue));

        var missing = topology.TestGetNodesMissingSparkline();
        Assert.Empty(missing);
    }
}
