using System.Collections.Generic;

namespace FlowTime.UI.Components.Topology;

internal sealed record CanvasRenderRequest(
    IReadOnlyList<NodeRenderInfo> Nodes,
    IReadOnlyList<EdgeRenderInfo> Edges,
    CanvasViewport Viewport,
    OverlaySettingsPayload Overlays);

internal sealed record NodeRenderInfo(
    string Id,
    double X,
    double Y,
    double Width,
    double Height,
    double CornerRadius,
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

internal sealed record CanvasViewport(double MinX, double MinY, double MaxX, double MaxY, double Padding);

internal sealed record OverlaySettingsPayload(
    OverlayMode Labels,
    OverlayMode EdgeArrows,
    OverlayMode EdgeShares,
    OverlayMode Sparklines,
    bool AutoLod,
    double ZoomLowThreshold,
    double ZoomMidThreshold,
    TopologyColorBasis ColorBasis,
    double SlaWarningThreshold,
    double UtilizationWarningThreshold,
    double ErrorRateAlertThreshold,
    bool NeighborEmphasis,
    bool IncludeServiceNodes,
    bool IncludeExpressionNodes,
    bool IncludeConstNodes);
