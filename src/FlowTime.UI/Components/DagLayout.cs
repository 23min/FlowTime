using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Services;

namespace FlowTime.UI.Components;

public static class DagLayout
{
    public const double NodeWidth = 70;
    // Reduced constant node height per request (compact micro-DAG)
    public const double NodeHeight = 20;
    private const double hSpacing = 32; // horizontal spacing between columns (slightly tighter for compactness)
    private const double vSpacing = 14; // vertical spacing between rows
    private const double maxRowWidth = 520; // wrap threshold (px) before moving to next row

    public sealed record LNode(string Id, double X, double Y, bool IsSource, bool IsSink);
    public sealed record LEdge(string From, string To);
    public sealed record LayoutResult(IReadOnlyList<LNode> Nodes, IReadOnlyList<LEdge> Edges, double Width, double Height);

    public static LayoutResult Layout(GraphStructureResult structure)
    {
        // Simple wrapping layout: place nodes in topological order left-to-right, wrap when row width exceeded.
        var nodes = new List<LNode>();
        double x = 0;
        double y = 0;
        double currentRowWidth = 0;
        double maxWidth = 0;
        foreach (var id in structure.Order)
        {
            var info = structure.Nodes.First(n => n.Id == id);
            var isSource = info.Inputs.Count == 0;
            var isSink = !structure.Nodes.Any(n => n.Inputs.Contains(id));
            // Wrap if node would exceed threshold
            if (x > 0 && (x + NodeWidth) > maxRowWidth)
            {
                y += NodeHeight + vSpacing;
                x = 0;
            }
            nodes.Add(new LNode(id, x, y, isSource, isSink));
            x += NodeWidth + hSpacing;
            currentRowWidth = x - hSpacing + NodeWidth; // approximate row width after adding node
            if (currentRowWidth > maxWidth) maxWidth = currentRowWidth;
        }
        if (nodes.Count == 0) maxWidth = 0;
        var edges = new List<LEdge>();
        foreach (var n in structure.Nodes)
        {
            foreach (var inp in n.Inputs)
            {
                edges.Add(new LEdge(inp, n.Id));
            }
        }
        double width = nodes.Count == 0 ? 0 : maxWidth;
        double height = nodes.Count == 0 ? 0 : nodes.Max(n => n.Y) + NodeHeight;
        return new LayoutResult(nodes, edges, width, height);
    }
}
