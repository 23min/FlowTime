using System.Collections.Generic;

namespace FlowTime.UI.Components.Topology;

internal sealed record CanvasRenderRequest(
    string? Title,
    IReadOnlyList<NodeRenderInfo> Nodes,
    IReadOnlyList<EdgeRenderInfo> Edges,
    CanvasViewport Viewport,
    OverlaySettingsPayload Overlays,
    TooltipPayload? Tooltip,
    ViewportSnapshotPayload? SavedViewport,
    bool PreserveViewport);

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
    NodeSparklineDto? Sparkline,
    string? FocusLabel,
    bool IsLeaf,
    NodeSemanticsDto? Semantics,
    NodeMetricSnapshotDto? Metrics,
    int Lane);

internal sealed record NodeSemanticsDto(
    string? Arrivals,
    string? Served,
    string? Errors,
    string? Attempts,
    string? Failures,
    string? RetryEcho,
    string? Queue,
    string? Capacity,
    string? Series,
    string? Expression,
    NodeDistributionDto? Distribution,
    IReadOnlyList<double>? InlineValues,
    IReadOnlyDictionary<string, string>? Aliases);

internal sealed record NodeDistributionDto(
    IReadOnlyList<double> Values,
    IReadOnlyList<double> Probabilities);

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
    string? Field,
    double? Multiplier,
    int? Lag);

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
    bool ShowQueueScalarBadge,
    SparklineRenderMode SparklineMode,
    EdgeRenderMode EdgeStyle,
    EdgeOverlayMode EdgeOverlay,
    bool ShowEdgeOverlayLabels,
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
    double ServiceTimeWarningThresholdMs,
    double ServiceTimeCriticalThresholdMs,
    bool ShowArrivalsDependencies,
    bool ShowServedDependencies,
    bool ShowErrorsDependencies,
    bool ShowQueueDependencies,
    bool ShowCapacityDependencies,
    bool ShowExpressionDependencies,
    bool ShowRetryMetrics,
    bool ShowEdgeMultipliers);

internal sealed record ViewportSnapshotPayload(
    double Scale,
    double OffsetX,
    double OffsetY,
    double WorldCenterX,
    double WorldCenterY,
    double OverlayScale,
    double BaseScale);

internal sealed record NodeSparklineDto(
    IReadOnlyList<double?> Values,
    IReadOnlyList<double?> Utilization,
    IReadOnlyList<double?> ErrorRate,
    IReadOnlyList<double?> QueueDepth,
    double Min,
    double Max,
    bool IsFlat,
    int StartIndex,
    IReadOnlyDictionary<string, SparklineSeriesSliceDto> Series);

internal sealed record SparklineSeriesSliceDto(
    IReadOnlyList<double?> Values,
    int StartIndex);

internal sealed record NodeMetricSnapshotDto(
    double? SuccessRate,
    double? Utilization,
    double? ErrorRate,
    double? QueueDepth,
    double? LatencyMinutes,
    double? ServiceTimeMs,
    IReadOnlyDictionary<string, double?>? Raw);
