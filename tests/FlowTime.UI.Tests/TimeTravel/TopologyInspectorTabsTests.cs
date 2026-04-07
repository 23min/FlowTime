using System;
using System.Linq;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyInspectorTabsTests
{
    [Fact]
    public void Topology_InspectorTabs_DefaultsToCharts()
    {
        var topology = new Topology();

        topology.TestOnNodeFocused("service-node");

        Assert.Equal("Charts", topology.TestGetInspectorTab());
    }

    [Fact]
    public void Topology_InspectorTabs_ContentMapping()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(CreateGraph());

        var expressionTabs = topology.TestGetInspectorTabsForNode("expr-1");
        Assert.Contains("Expression", expressionTabs);

        var serviceTabs = topology.TestGetInspectorTabsForNode("svc-1");
        Assert.DoesNotContain("Expression", serviceTabs);
    }

    [Fact]
    public void Topology_InspectorTabs_DoesNotInferExpressionFromLegacyKind()
    {
        var topology = new Topology();
        var graph = new TopologyGraph(
            new[]
            {
                new TopologyNode("legacy-expr", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
                {
                    Category = "service",
                    Analytical = new GraphNodeAnalyticalModel()
                }
            },
            Array.Empty<TopologyEdge>());

        topology.TestSetTopologyGraph(graph);

        var tabs = topology.TestGetInspectorTabsForNode("legacy-expr");

        Assert.DoesNotContain("Expression", tabs);
    }

    [Fact]
    public void Topology_InspectorTabs_PreservesSelectionWhileOpen()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(CreateGraph());

        topology.TestOnNodeFocused("svc-1");
        topology.TestSetInspectorTab("Dependencies");
        topology.TestOnNodeFocused("expr-1");

        Assert.Equal("Dependencies", topology.TestGetInspectorTab());
    }

    [Fact]
    public void Topology_InspectorTabs_ResetsOnClose()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(CreateGraph());

        topology.TestOnNodeFocused("svc-1");
        topology.TestSetInspectorTab("Warnings");
        topology.TestCloseInspector();

        topology.TestOnNodeFocused("svc-1");

        Assert.Equal("Charts", topology.TestGetInspectorTab());
    }

    [Fact]
    public void Topology_GraphQueryOptions_OperationalIncludesDependencies()
    {
        var topology = new Topology();
        var settings = TopologyOverlaySettings.Default.Clone();
        settings.EnableFullDag = false;

        topology.TestSetOverlaySettings(settings);

        var options = topology.TestBuildGraphQueryOptions();

        Assert.Equal("full", options.Mode);
        Assert.Null(options.Kinds);
    }

    private static TopologyGraph CreateGraph()
    {
        var nodes = new[]
        {
            new TopologyNode("expr-1", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            {
                Category = "expression",
                Analytical = new GraphNodeAnalyticalModel
                {
                    Identity = "expression"
                }
            },
            new TopologyNode("svc-1", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 1, 0, 0, false, EmptySemantics())
            {
                Category = "service",
                Analytical = new GraphNodeAnalyticalModel
                {
                    Identity = "service",
                    HasServiceSemantics = true
                }
            }
        };

        return new TopologyGraph(nodes, Array.Empty<TopologyEdge>());
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
}
