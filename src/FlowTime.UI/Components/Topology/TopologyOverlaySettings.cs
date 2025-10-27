namespace FlowTime.UI.Components.Topology;

public sealed class TopologyOverlaySettings
{
    public bool ShowLabels { get; set; } = true;
    public bool ShowEdgeArrows { get; set; } = true;
    public bool ShowEdgeShares { get; set; }
    public bool ShowSparklines { get; set; }
    public bool EnableFullDag { get; set; }

    public TopologyOverlaySettings Clone()
    {
        return new TopologyOverlaySettings
        {
            ShowLabels = ShowLabels,
            ShowEdgeArrows = ShowEdgeArrows,
            ShowEdgeShares = ShowEdgeShares,
            ShowSparklines = ShowSparklines,
            EnableFullDag = EnableFullDag
        };
    }

    public static TopologyOverlaySettings Default => new();
}
