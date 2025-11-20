namespace FlowTime.UI.Components.Topology;

public enum TopologyColorBasis
{
    Sla,
    Utilization,
    Errors,
    Queue,
    ServiceTime,
    FlowLatency
}

public enum SparklineRenderMode
{
    Line,
    Bar
}

public enum EdgeOverlayMode
{
    Off,
    RetryRate,
    Attempts
}

public enum EdgeRenderMode
{
    Orthogonal,
    Bezier
}

public enum LayoutMode
{
    Layered,
    HappyPath
}

public sealed class TopologyOverlaySettings
{
    public bool ShowLabels { get; set; } = true;
    public bool ShowEdgeArrows { get; set; } = true;
    public bool ShowEdgeShares { get; set; } = true;
    public bool ShowSparklines { get; set; } = true;
    // When true, draw a scalar queue-depth badge on queue nodes
    public SparklineRenderMode SparklineMode { get; set; } = SparklineRenderMode.Line;
    public EdgeRenderMode EdgeStyle { get; set; } = EdgeRenderMode.Bezier;
    public EdgeOverlayMode EdgeOverlay { get; set; } = EdgeOverlayMode.Off;
    public bool ShowEdgeMultipliers { get; set; } = true;
    public bool ShowEdgeOverlayLabels { get; set; } = true;
    public bool ShowRetryBudget { get; set; } = true;
    public bool ShowTerminalEdges { get; set; } = true;

    public bool EnableFullDag { get; set; } = true;
    public bool IncludeServiceNodes { get; set; } = true;
    public bool IncludeDlqNodes { get; set; } = true;
    public bool IncludeExpressionNodes { get; set; } = true;
    public bool IncludeConstNodes { get; set; } = true;
    public bool NeighborEmphasis { get; set; } = true;

    public TopologyColorBasis ColorBasis { get; set; } = TopologyColorBasis.Sla;
    public double SlaWarningThreshold { get; set; } = 0.95;
    public double ErrorRateAlertThreshold { get; set; } = 0.05;
    public double UtilizationWarningThreshold { get; set; } = 0.9;
    public double ServiceTimeWarningThresholdMs { get; set; } = 400;
    public double ServiceTimeCriticalThresholdMs { get; set; } = 700;
    public double FlowLatencyWarningThresholdMs { get; set; } = 2000;
    public double FlowLatencyCriticalThresholdMs { get; set; } = 10_000;

    public double ZoomPercent { get; set; } = 100;

    public bool RespectUiPositions { get; set; } = false;

    public bool ShowArrivalsDependencies { get; set; } = true;
    public bool ShowServedDependencies { get; set; } = true;
    public bool ShowErrorsDependencies { get; set; } = true;
    public bool ShowQueueDependencies { get; set; } = true;
    public bool ShowCapacityDependencies { get; set; } = true;
    public bool ShowExpressionDependencies { get; set; } = true;
    public LayoutMode Layout { get; set; } = LayoutMode.HappyPath;
    public bool ShowRetryMetrics { get; set; } = true;

    // Inspector overview (Horizon chart)
    public bool ShowInspectorOverview { get; set; } = true;
    public int HorizonBands { get; set; } = 3; // Allow 3–5
    public bool NormalizeInspectorHorizonCounts { get; set; } = false;
    public double? InspectorHorizonGlobalCap { get; set; } = null;

    public TopologyOverlaySettings Clone()
    {
        return new TopologyOverlaySettings
        {
            ShowLabels = ShowLabels,
            ShowEdgeArrows = ShowEdgeArrows,
            ShowEdgeShares = ShowEdgeShares,
            ShowSparklines = ShowSparklines,
            SparklineMode = SparklineMode,
            EdgeStyle = EdgeStyle,
            EdgeOverlay = EdgeOverlay,
            ShowEdgeMultipliers = ShowEdgeMultipliers,
            ShowEdgeOverlayLabels = ShowEdgeOverlayLabels,
            ShowRetryBudget = ShowRetryBudget,
            ShowTerminalEdges = ShowTerminalEdges,
            EnableFullDag = EnableFullDag,
            IncludeServiceNodes = IncludeServiceNodes,
            IncludeDlqNodes = IncludeDlqNodes,
            IncludeExpressionNodes = IncludeExpressionNodes,
            IncludeConstNodes = IncludeConstNodes,
            NeighborEmphasis = NeighborEmphasis,
            ColorBasis = ColorBasis,
            SlaWarningThreshold = SlaWarningThreshold,
            ErrorRateAlertThreshold = ErrorRateAlertThreshold,
            UtilizationWarningThreshold = UtilizationWarningThreshold,
            ServiceTimeWarningThresholdMs = ServiceTimeWarningThresholdMs,
            ServiceTimeCriticalThresholdMs = ServiceTimeCriticalThresholdMs,
            ZoomPercent = ZoomPercent,
            RespectUiPositions = RespectUiPositions,
            ShowArrivalsDependencies = ShowArrivalsDependencies,
            ShowServedDependencies = ShowServedDependencies,
            ShowErrorsDependencies = ShowErrorsDependencies,
            ShowQueueDependencies = ShowQueueDependencies,
            ShowCapacityDependencies = ShowCapacityDependencies,
            ShowExpressionDependencies = ShowExpressionDependencies,
            Layout = Layout,
            ShowRetryMetrics = ShowRetryMetrics,
            ShowInspectorOverview = ShowInspectorOverview,
            HorizonBands = HorizonBands,
            NormalizeInspectorHorizonCounts = NormalizeInspectorHorizonCounts,
            InspectorHorizonGlobalCap = InspectorHorizonGlobalCap
        };
    }

    public static TopologyOverlaySettings Default => new();
}
