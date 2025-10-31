namespace FlowTime.UI.Components.Topology;

public sealed class ViewportSnapshot
{
    public double Scale { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double WorldCenterX { get; set; }
    public double WorldCenterY { get; set; }
    public double OverlayScale { get; set; }
    public double BaseScale { get; set; }

    public ViewportSnapshot CloneWithScale(double scale)
    {
        return new ViewportSnapshot
        {
            Scale = scale,
            BaseScale = scale,
            OverlayScale = scale,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            WorldCenterX = WorldCenterX,
            WorldCenterY = WorldCenterY
        };
    }
}
