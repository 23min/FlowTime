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
    bool IsFocused,
    NodeSparklineDto? Sparkline);

internal sealed record EdgeRenderInfo(
    string Id,
    string From,
    string To,
    double FromX,
    double FromY,
    double ToX,
    double ToY,
    double? Share);

internal sealed record CanvasViewport(double MinX, double MinY, double MaxX, double MaxY, double Padding);

internal sealed record OverlaySettingsPayload(
    bool ShowLabels,
  	bool ShowEdgeArrows,
  	bool ShowEdgeShares,
  	bool ShowSparklines,
    SparklineRenderMode SparklineMode,
    EdgeRenderMode EdgeStyle,
    bool AutoLod,
    double ZoomLowThreshold,
    double ZoomMidThreshold,
    double ZoomPercent,
    TopologyColorBasis ColorBasis,
    double SlaWarningThreshold,
    double UtilizationWarningThreshold,
    double ErrorRateAlertThreshold,
    bool NeighborEmphasis,
    bool IncludeServiceNodes,
    bool IncludeExpressionNodes,
    bool IncludeConstNodes,
    int SelectedBin,
    double SlaSuccessThreshold,
    double SlaWarningCutoff,
    double UtilizationWarningCutoff,
    double UtilizationCriticalCutoff,
    double ErrorWarningCutoff,
    double ErrorCriticalCutoff);

internal sealed record NodeSparklineDto(
    IReadOnlyList<double?> Values,
    IReadOnlyList<double?> Utilization,
    IReadOnlyList<double?> ErrorRate,
    IReadOnlyList<double?> QueueDepth,
    double Min,
    double Max,
    bool IsFlat,
    int StartIndex);
