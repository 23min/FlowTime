namespace FlowTime.UI.Components.Topology;

public enum TopologyColorBasis
{
    Sla,
    Utilization,
    Errors,
    Queue
}

public enum SparklineRenderMode
{
    Line,
    Bar
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
    public SparklineRenderMode SparklineMode { get; set; } = SparklineRenderMode.Line;
    public EdgeRenderMode EdgeStyle { get; set; } = EdgeRenderMode.Bezier;

    public bool EnableFullDag { get; set; } = true;
    public bool IncludeServiceNodes { get; set; } = true;
    public bool IncludeExpressionNodes { get; set; } = true;
    public bool IncludeConstNodes { get; set; } = true;
    public bool NeighborEmphasis { get; set; } = true;

    public TopologyColorBasis ColorBasis { get; set; } = TopologyColorBasis.Sla;
    public double SlaWarningThreshold { get; set; } = 0.95;
    public double ErrorRateAlertThreshold { get; set; } = 0.05;
    public double UtilizationWarningThreshold { get; set; } = 0.9;

    public double ZoomPercent { get; set; } = 100;

    public bool RespectUiPositions { get; set; } = false;

    public bool ShowArrivalsDependencies { get; set; } = true;
    public bool ShowServedDependencies { get; set; } = true;
    public bool ShowErrorsDependencies { get; set; } = true;
    public bool ShowQueueDependencies { get; set; } = true;
    public bool ShowCapacityDependencies { get; set; } = true;
    public bool ShowExpressionDependencies { get; set; } = true;
    public LayoutMode Layout { get; set; } = LayoutMode.HappyPath;

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
            EnableFullDag = EnableFullDag,
            IncludeServiceNodes = IncludeServiceNodes,
            IncludeExpressionNodes = IncludeExpressionNodes,
            IncludeConstNodes = IncludeConstNodes,
            NeighborEmphasis = NeighborEmphasis,
            ColorBasis = ColorBasis,
            SlaWarningThreshold = SlaWarningThreshold,
            ErrorRateAlertThreshold = ErrorRateAlertThreshold,
            UtilizationWarningThreshold = UtilizationWarningThreshold,
            ZoomPercent = ZoomPercent,
            RespectUiPositions = RespectUiPositions,
            ShowArrivalsDependencies = ShowArrivalsDependencies,
            ShowServedDependencies = ShowServedDependencies,
            ShowErrorsDependencies = ShowErrorsDependencies,
            ShowQueueDependencies = ShowQueueDependencies,
            ShowCapacityDependencies = ShowCapacityDependencies,
            ShowExpressionDependencies = ShowExpressionDependencies,
            Layout = Layout,
            ShowInspectorOverview = ShowInspectorOverview,
            HorizonBands = HorizonBands,
            NormalizeInspectorHorizonCounts = NormalizeInspectorHorizonCounts,
            InspectorHorizonGlobalCap = InspectorHorizonGlobalCap
        };
    }

    public static TopologyOverlaySettings Default => new();
}
