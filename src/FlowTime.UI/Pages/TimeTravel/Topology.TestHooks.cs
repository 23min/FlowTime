using System.Collections.Generic;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging;

namespace FlowTime.UI.Pages.TimeTravel;

/// <summary>
/// Test accessors for the Topology page to simplify verification of private sparkline helpers.
/// </summary>
public partial class Topology
{
    internal void TestSetTopologyGraph(TopologyGraph graph)
    {
        topologyGraph = graph;
    }

    internal void TestSetWindowData(TimeTravelStateWindowDto data)
    {
        windowData = data;
    }

    internal void TestBuildNodeSparklines()
    {
        BuildNodeSparklines();
    }

    internal IReadOnlyDictionary<string, NodeSparklineData> TestGetNodeSparklines()
    {
        return nodeSparklines;
    }

    internal IReadOnlyCollection<string> TestGetNodesMissingSparkline()
    {
        return nodesMissingSparkline;
    }

    internal void TestOnNodeFocused(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            inspectorPinned = false;
            inspectorOpen = false;
            inspectorNodeId = null;
        }
        else
        {
            inspectorNodeId = nodeId;
            inspectorOpen = true;
            inspectorPinned = true;
        }
    }

    internal bool TestIsInspectorOpen() => inspectorOpen;

    internal string? TestGetInspectorNodeId() => inspectorNodeId;

    internal void TestSetNodeSparklines(IReadOnlyDictionary<string, NodeSparklineData> sparklines)
    {
        nodeSparklines = sparklines;
    }

    internal void TestSetActiveMetrics(IReadOnlyDictionary<string, NodeBinMetrics> metrics)
    {
        activeMetrics = metrics;
    }

    internal IReadOnlyList<InspectorMetricBlock> TestBuildInspectorMetrics(string nodeId)
    {
        return BuildInspectorMetricBlocks(nodeId);
    }

    internal void TestCloseInspector()
    {
        CloseInspector();
    }

    internal void TestSetLogger(ILogger<Topology> logger)
    {
        Logger = logger;
    }

    internal void TestUpdateActiveMetrics(int bin)
    {
        UpdateActiveMetrics(bin);
    }
}
