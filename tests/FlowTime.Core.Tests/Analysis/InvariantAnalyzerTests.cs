using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Analysis;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Analysis;

public class InvariantAnalyzerTests
{
    [Fact]
    public void DetectServiceWithBufferClassCoverageGaps_WarnsWhenOutflowOrLossMissingClasses()
    {
        var nodeDefinitions = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Queue"] = new NodeDefinition
            {
                Id = "Queue",
                Kind = "serviceWithBuffer",
                Inflow = "queue_inflow",
                Outflow = "queue_outflow",
                Loss = "queue_loss"
            }
        };

        var contributions = new Dictionary<NodeId, IReadOnlyDictionary<string, double[]>>();
        contributions[new NodeId("queue_inflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d },
            ["Airport"] = new[] { 2d }
        };
        contributions[new NodeId("queue_outflow")] = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downtown"] = new[] { 1d }
        };

        var warnings = InvariantAnalyzer.DetectServiceWithBufferClassCoverageGaps(nodeDefinitions, contributions);

        Assert.Contains(warnings, warning => warning.Code == "class_series_partial_outflow");
        Assert.Contains(warnings, warning => warning.Code == "class_series_missing_loss");
        Assert.Equal(2, warnings.Count);
    }
}
