using System.Collections.Generic;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Services;

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
}
