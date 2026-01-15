using System;
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
            new TopologyNode("ingress", "service", "service", Array.Empty<string>(), new[] { "processor" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("processor", "service", "service", new[] { "ingress" }, Array.Empty<string>(), 1, 0, 0, 0, false, EmptySemantics())
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
