using System.Collections.Generic;

namespace FlowTime.UI.Components.Topology;

internal sealed record CanvasRenderRequest(
    IReadOnlyList<NodeRenderInfo> Nodes,
    IReadOnlyList<EdgeRenderInfo> Edges,
    CanvasViewport Viewport);

internal sealed record NodeRenderInfo(
    string Id,
    double X,
    double Y,
    double Radius,
    string Fill,
    string Stroke,
    bool IsFocused);

internal sealed record EdgeRenderInfo(
    string Id,
    string From,
    string To,
    double FromX,
    double FromY,
    double ToX,
    double ToY);

internal sealed record CanvasViewport(double OffsetX, double OffsetY, double Scale);
