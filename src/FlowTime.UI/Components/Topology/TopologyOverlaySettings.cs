namespace FlowTime.UI.Components.Topology;

public enum OverlayMode
{
    Auto,
    On,
    Off
}

public enum TopologyColorBasis
{
    Sla,
    Utilization,
    Errors,
    Queue
}

public sealed class TopologyOverlaySettings
{
    public OverlayMode Labels { get; set; } = OverlayMode.Auto;
    public OverlayMode EdgeArrows { get; set; } = OverlayMode.On;
    public OverlayMode EdgeShares { get; set; } = OverlayMode.Auto;
    public OverlayMode Sparklines { get; set; } = OverlayMode.Auto;

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

    public TopologyOverlaySettings Clone()
    {
        return new TopologyOverlaySettings
        {
            Labels = Labels,
            EdgeArrows = EdgeArrows,
            EdgeShares = EdgeShares,
            Sparklines = Sparklines,
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
            ZoomMidThreshold = ZoomMidThreshold
        };
    }

    public static TopologyOverlaySettings Default => new();
}
