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
    public bool ShowEdgeShares { get; set; } = false;
    public bool ShowSparklines { get; set; } = true;
    public SparklineRenderMode SparklineMode { get; set; } = SparklineRenderMode.Line;
    public EdgeRenderMode EdgeStyle { get; set; } = EdgeRenderMode.Orthogonal;

    public bool EnableFullDag { get; set; }
    public bool IncludeServiceNodes { get; set; } = true;
    public bool IncludeExpressionNodes { get; set; }
    public bool IncludeConstNodes { get; set; }
    public bool NeighborEmphasis { get; set; } = true;

    public TopologyColorBasis ColorBasis { get; set; } = TopologyColorBasis.Sla;
    public double SlaWarningThreshold { get; set; } = 0.95;
    public double ErrorRateAlertThreshold { get; set; } = 0.05;
    public double UtilizationWarningThreshold { get; set; } = 0.9;

    public bool AutoLod { get; set; } = true;
    public double ZoomLowThreshold { get; set; } = 0.5;
    public double ZoomMidThreshold { get; set; } = 1.0;
    public double ZoomPercent { get; set; } = 100;

    public bool RespectUiPositions { get; set; } = false;

    public bool ShowArrivalsDependencies { get; set; } = true;
    public bool ShowServedDependencies { get; set; } = true;
    public bool ShowErrorsDependencies { get; set; } = true;
    public bool ShowQueueDependencies { get; set; } = true;
    public bool ShowCapacityDependencies { get; set; } = true;
    public bool ShowExpressionDependencies { get; set; } = true;
    public LayoutMode Layout { get; set; } = LayoutMode.Layered;

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
            AutoLod = AutoLod,
            ZoomLowThreshold = ZoomLowThreshold,
            ZoomMidThreshold = ZoomMidThreshold,
            ZoomPercent = ZoomPercent,
            RespectUiPositions = RespectUiPositions,
            ShowArrivalsDependencies = ShowArrivalsDependencies,
            ShowServedDependencies = ShowServedDependencies,
            ShowErrorsDependencies = ShowErrorsDependencies,
            ShowQueueDependencies = ShowQueueDependencies,
            ShowCapacityDependencies = ShowCapacityDependencies,
            ShowExpressionDependencies = ShowExpressionDependencies,
            Layout = Layout
        };
    }

    public static TopologyOverlaySettings Default => new();
}
