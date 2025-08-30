using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Services;

namespace FlowTime.UI.Components;

public static class DagLayout
{
    public const double NodeWidth = 70;
    public const double NodeHeight = 28;
    private const double HSpacing = 40; // horizontal spacing between columns
    private const double VSpacing = 16; // vertical spacing

    public sealed record LNode(string Id, double X, double Y, bool IsSource, bool IsSink);
    public sealed record LEdge(string From, string To);
    public sealed record LayoutResult(IReadOnlyList<LNode> Nodes, IReadOnlyList<LEdge> Edges, double Width, double Height);

    public static LayoutResult Layout(GraphStructureResult structure)
    {
        // Column index is topological order; group nodes by that order sequentially
        var orderIndex = structure.Order.Select((id, idx) => (id, idx)).ToDictionary(t => t.id, t => t.idx);
        // For now, single row layout with nodes in order; upgrade later to layered by depth
        var nodes = new List<LNode>();
        double x = 0;
        double y = 0;
        foreach (var id in structure.Order)
        {
            var info = structure.Nodes.First(n => n.Id == id);
            var isSource = info.Inputs.Count == 0;
            var isSink = !structure.Nodes.Any(n => n.Inputs.Contains(id));
            nodes.Add(new LNode(id, x, y, isSource, isSink));
            x += NodeWidth + HSpacing;
        }
        var edges = new List<LEdge>();
        foreach (var n in structure.Nodes)
        {
            foreach (var inp in n.Inputs)
            {
                edges.Add(new LEdge(inp, n.Id));
            }
        }
        double width = nodes.Count == 0 ? 0 : nodes.Max(n => n.X) + NodeWidth;
        double height = NodeHeight;
        return new LayoutResult(nodes, edges, width, height);
    }
}
