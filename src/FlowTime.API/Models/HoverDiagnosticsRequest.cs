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
    public double DurationMs { get; init; }
    public double RatePerSecond { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string Source { get; init; } = "manual";
    public HoverDiagnosticsCanvasPayload? Canvas { get; init; }
}
