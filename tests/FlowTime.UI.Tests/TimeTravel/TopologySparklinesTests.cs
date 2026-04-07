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

        var pmfNode = CreateTopologyNode(
            "distribution_node", "pmf", "pmf",
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
                Attempts: null,
                Failures: null,
                ExhaustedFailures: null,
                RetryEcho: null,
                RetryBudgetRemaining: null,
                Queue: null,
                Capacity: null,
                Parallelism: null,
                Series: null,
                Expression: null,
                Distribution: new TopologyNodeDistribution(
                    new double[] { 1, 2, 3 },
                    new double[] { 0.2, 0.3, 0.5 }),
                InlineValues: null,
                Aliases: null));

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

    private static TopologyNode CreateTopologyNode(
        string id,
        string kind,
        string semanticKind,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> outputs,
        int layer,
        int index,
        double x,
        double y,
        bool isPositionFixed,
        TopologyNodeSemantics semantics,
        int lane = 0,
        GraphDispatchScheduleModel? DispatchSchedule = null,
        string? NodeRole = null)
    {
        return new TopologyNode(id, kind, inputs, outputs, layer, index, x, y, isPositionFixed, semantics, lane, DispatchSchedule, NodeRole)
        {
            Category = ResolveCategory(semanticKind),
            Analytical = CreateAnalytical(semanticKind)
        };
    }

    private static string ResolveCategory(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "queue" => "queue",
            "dlq" => "dlq",
            "router" => "router",
            "dependency" => "dependency",
            "sink" => "sink",
            "const" or "constant" or "pmf" => "constant",
            "expr" or "expression" => "expression",
            _ => "service"
        };
    }

    private static GraphNodeAnalyticalModel CreateAnalytical(string kind)
    {
        var normalized = kind.ToLowerInvariant();
        var category = ResolveCategory(kind);
        var hasQueueSemantics = normalized is "queue" or "dlq" or "servicewithbuffer";
        var hasServiceSemantics = category == "service";

        return new GraphNodeAnalyticalModel
        {
            Identity = normalized switch
            {
                "const" => "constant",
                "expr" => "expression",
                _ => kind
            },
            HasQueueSemantics = hasQueueSemantics,
            HasServiceSemantics = hasServiceSemantics,
            HasCycleTimeDecomposition = hasQueueSemantics && hasServiceSemantics,
            StationarityWarningApplicable = hasQueueSemantics
        };
    }
}
