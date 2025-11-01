using System;
using System.Collections.Generic;
using System.Globalization;
using Bunit;
using FlowTime.UI.Components.Topology;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyInspectorSparklineTests : TestContext
{
    [Fact]
    public void RendersZeroBaselineWithinPlotWhenDataSpansNegativeAndPositive()
    {
        var values = new double?[] { -0.4, -0.1, 0.25, 0.6 };
        var sparkline = NodeSparklineData.Create(
            values,
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            startIndex: 0);

        var cut = RenderComponent<TopologyInspectorSparkline>(parameters => parameters
            .Add(p => p.Data, sparkline)
            .Add(p => p.SelectedBin, 2)
            .Add(p => p.Stroke, "#123456"));

        var axis = cut.Find("line.node-inspector-sparkline-axis");
        var y1 = double.Parse(axis.GetAttribute("y1")!, CultureInfo.InvariantCulture);

        // With explicit zero baseline logic the axis should sit above the bottom padding (112 by default).
        Assert.InRange(y1, 60, 111.999);

        // Highlight circle should be present for the selected bin.
        var highlight = cut.Find("circle");
        Assert.Equal("#123456", highlight.GetAttribute("fill"));
    }

    [Fact]
    public void ProducesMinAndMaxLabelsFromSamples()
    {
        var values = new double?[] { 0.1, null, 0.9, 0.55 };
        var sparkline = NodeSparklineData.Create(
            values,
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            Array.Empty<double?>(),
            startIndex: 0);

        var cut = RenderComponent<TopologyInspectorSparkline>(parameters => parameters
            .Add(p => p.Data, sparkline)
            .Add(p => p.SelectedBin, 3));

        Assert.Contains("0.1", cut.Markup);
        Assert.Contains("0.9", cut.Markup);
    }
}
