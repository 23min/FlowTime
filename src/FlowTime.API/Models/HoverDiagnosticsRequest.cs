using System;

namespace FlowTime.API.Models;

public sealed record HoverDiagnosticsCanvasPayload
{
    public double? Width { get; init; }
    public double? Height { get; init; }
}

public sealed record HoverDiagnosticsRequest
{
    public string? RunId { get; init; }
    public string? BuildHash { get; init; }
    public string? PayloadSignature { get; init; }
    public double InteropDispatches { get; init; }
    public double TotalDispatches { get; init; }
    public double DurationMs { get; init; }
    public double RatePerSecond { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string Source { get; init; } = "manual";
    public double? CanvasWidth { get; init; }
    public double? CanvasHeight { get; init; }
    public bool? OperationalOnly { get; init; }
    public string? Mode { get; init; }
    public bool? HoverCacheDisabled { get; init; }
    public bool? NeighborEmphasis { get; init; }
    public double? ZoomPercent { get; init; }
    public HoverDiagnosticsCanvasPayload? Canvas { get; init; }

    public double? ResolveCanvasWidth() => CanvasWidth ?? Canvas?.Width;

    public double? ResolveCanvasHeight() => CanvasHeight ?? Canvas?.Height;
}
