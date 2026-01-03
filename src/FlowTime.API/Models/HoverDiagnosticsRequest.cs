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
    public string? HoveredNodeId { get; init; }
    public string? FocusedNodeId { get; init; }
    public double? NodeCount { get; init; }
    public double? EdgeCount { get; init; }
    public bool? InspectorVisible { get; init; }
    public double PointerThrottleSkips { get; init; }
    public double PointerEventsReceived { get; init; }
    public double PointerEventsProcessed { get; init; }
    public double PointerQueueDrops { get; init; }
    public double PointerIntentSkips { get; init; }
    public double AvgDrawMs { get; init; }
    public double MaxDrawMs { get; init; }
    public double LastDrawMs { get; init; }
    public double FrameCount { get; init; }
    public double PanDistance { get; init; }
    public double ZoomEvents { get; init; }
    public double DragFrameCount { get; init; }
    public double DragTotalDurationMs { get; init; }
    public double DragAverageFrameMs { get; init; }
    public double DragMaxFrameMs { get; init; }
    public double SceneRebuilds { get; init; }
    public double OverlayUpdates { get; init; }
    public double LayoutReads { get; init; }
    public double PointerInpSampleCount { get; init; }
    public double PointerInpAverageMs { get; init; }
    public double PointerInpMaxMs { get; init; }
    public double EdgeCandidatesLast { get; init; }
    public double EdgeCandidatesAverage { get; init; }
    public double EdgeCandidateSamples { get; init; }
    public double EdgeCandidateFallbacks { get; init; }
    public double EdgeGridCellSize { get; init; }
    public double EdgeCacheHits { get; init; }
    public double EdgeCacheMisses { get; init; }
    public HoverDiagnosticsCanvasPayload? Canvas { get; init; }

    public double? ResolveCanvasWidth() => CanvasWidth ?? Canvas?.Width;

    public double? ResolveCanvasHeight() => CanvasHeight ?? Canvas?.Height;
}
