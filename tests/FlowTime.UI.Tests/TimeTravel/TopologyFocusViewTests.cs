using System;
using System.Collections.Generic;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyFocusViewTests
{
    [Fact]
    public void Topology_FocusView_TogglesOnSelectedNode()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(CreateGraph());

        Assert.False(topology.TestIsFocusToggleEnabled());
        Assert.False(topology.TestIsFocusViewEnabled());

        topology.TestToggleFocusView();
        Assert.False(topology.TestIsFocusViewEnabled());

        topology.TestOnNodeSelected("processor");

        Assert.True(topology.TestIsFocusToggleEnabled());

        topology.TestToggleFocusView();

        Assert.True(topology.TestIsFocusViewEnabled());
    }

    [Fact]
    public async Task Topology_FocusView_PreservesFullGraphState()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(CreateGraph());
        topology.TestOnNodeSelected("processor");

        var fullSnapshot = new ViewportSnapshot
        {
            Scale = 1.1,
            OffsetX = 20,
            OffsetY = -30,
            WorldCenterX = 140,
            WorldCenterY = 260,
            OverlayScale = 1.1,
            BaseScale = 1.1
        };

        topology.TestSetFullViewportSnapshot(fullSnapshot);
        topology.TestSetFocusViewEnabled(true);

        var focusSnapshot = new ViewportSnapshot
        {
            Scale = 0.85,
            OffsetX = -50,
            OffsetY = 75,
            WorldCenterX = 400,
            WorldCenterY = 320,
            OverlayScale = 0.85,
            BaseScale = 0.85
        };

        await topology.TestOnCanvasViewportChanged(focusSnapshot);

        var storedFull = topology.TestGetFullViewportSnapshot();
        Assert.NotNull(storedFull);
        AssertSnapshotMatches(fullSnapshot, storedFull!);

        topology.TestSetFocusViewEnabled(false);

        var pending = topology.TestGetPendingViewportSnapshot();
        Assert.NotNull(pending);
        AssertSnapshotMatches(fullSnapshot, pending!);

        var storedFocus = topology.TestGetFocusViewportSnapshot();
        Assert.NotNull(storedFocus);
        AssertSnapshotMatches(focusSnapshot, storedFocus!);
    }

    private static TopologyGraph CreateGraph()
    {
        var nodes = new[]
        {
            CreateTopologyNode("ingress", "service", "service", Array.Empty<string>(), new[] { "processor" }, 0, 0, 0, 0, false, EmptySemantics()),
            CreateTopologyNode("processor", "service", "service", new[] { "ingress" }, Array.Empty<string>(), 1, 0, 0, 0, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("edge_ingress_processor", "ingress", "processor", 1, null, null, null, null)
        };

        return new TopologyGraph(nodes, edges);
    }

    private static TopologyNodeSemantics EmptySemantics() => new(
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
        Distribution: null,
        InlineValues: null,
        Aliases: null,
        Metadata: null,
        MaxAttempts: null,
        BackoffStrategy: null,
        ExhaustedPolicy: null);

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

    private static void AssertSnapshotMatches(ViewportSnapshot expected, ViewportSnapshot actual)
    {
        Assert.Equal(expected.Scale, actual.Scale);
        Assert.Equal(expected.OffsetX, actual.OffsetX);
        Assert.Equal(expected.OffsetY, actual.OffsetY);
        Assert.Equal(expected.WorldCenterX, actual.WorldCenterX);
        Assert.Equal(expected.WorldCenterY, actual.WorldCenterY);
        Assert.Equal(expected.OverlayScale, actual.OverlayScale);
        Assert.Equal(expected.BaseScale, actual.BaseScale);
    }
}
