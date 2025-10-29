using System.Collections.Generic;

namespace FlowTime.UI.Components.Topology;

internal sealed record CanvasRenderRequest(
    IReadOnlyList<NodeRenderInfo> Nodes,
    IReadOnlyList<EdgeRenderInfo> Edges,
    CanvasViewport Viewport,
    OverlaySettingsPayload Overlays,
    TooltipPayload? Tooltip);

internal sealed record NodeRenderInfo(
    string Id,
    string Kind,
    double X,
    double Y,
    double Width,
    double Height,
    double CornerRadius,
    string Fill,
    string Stroke,
    bool IsFocused,
    bool IsVisible,
    NodeSparklineDto? Sparkline);

internal sealed record EdgeRenderInfo(
    string Id,
    string From,
    string To,
    double FromX,
    double FromY,
    double ToX,
    double ToY,
    double? Share,
    string? EdgeType,
    string? Field);

internal sealed record CanvasViewport(double MinX, double MinY, double MaxX, double MaxY, double Padding);

internal sealed record TooltipPayload(
    string Title,
    string Subtitle,
    IReadOnlyList<string> Lines);

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
    bool EnableFullDag,
    bool IncludeServiceNodes,
    bool IncludeExpressionNodes,
    bool IncludeConstNodes,
    int SelectedBin,
    double SlaSuccessThreshold,
    double SlaWarningCutoff,
    double UtilizationWarningCutoff,
    double UtilizationCriticalCutoff,
    double ErrorWarningCutoff,
    double ErrorCriticalCutoff,
    bool ShowArrivalsDependencies,
    bool ShowServedDependencies,
    bool ShowErrorsDependencies,
    bool ShowQueueDependencies,
    bool ShowCapacityDependencies,
    bool ShowExpressionDependencies,
    bool ShowComputeNodes);

internal sealed record NodeSparklineDto(
    IReadOnlyList<double?> Values,
    IReadOnlyList<double?> Utilization,
    IReadOnlyList<double?> ErrorRate,
    IReadOnlyList<double?> QueueDepth,
    double Min,
    double Max,
    bool IsFlat,
    int StartIndex);
