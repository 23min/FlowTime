using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlowTime.API.Services;

internal static class DiagnosticsFileWriter
{
    private const string HoverCsvHeader = "timestampUtc,runId,buildHash,payloadSignature,interopDispatches,totalDispatches,durationMs,ratePerSecond,source,canvasWidth,canvasHeight,operationalOnly,mode,neighborEmphasis,zoomPercent,hoveredNodeId,focusedNodeId,nodeCount,edgeCount,inspectorVisible,pointerThrottleSkips,pointerEventsReceived,pointerEventsProcessed,pointerQueueDrops,pointerIntentSkips,dragFrameCount,dragTotalDurationMs,dragAverageFrameMs,dragMaxFrameMs,sceneRebuilds,overlayUpdates,layoutReads,pointerInpSampleCount,pointerInpAverageMs,pointerInpMaxMs";
    private const string CanvasCsvHeader = "timestampUtc,runId,buildHash,payloadSignature,nodeCount,edgeCount,avgDrawMs,maxDrawMs,lastDrawMs,frameCount,panDistance,zoomEvents,source,canvasWidth,canvasHeight,operationalOnly,mode,neighborEmphasis,zoomPercent,inspectorVisible";

    public static async Task AppendHoverEntryAsync(string path, HoverDiagnosticsRow row, int maxRows, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var fileExists = File.Exists(path);
        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            if (!fileExists)
            {
                await writer.WriteLineAsync(HoverCsvHeader);
            }

            await writer.WriteLineAsync(row.ToCsvLine());
        }

        if (maxRows > 0)
        {
            await TrimCsvFileAsync(path, maxRows, cancellationToken);
        }
    }

    public static async Task AppendCanvasEntryAsync(string path, CanvasDiagnosticsRow row, int maxRows, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var fileExists = File.Exists(path);
        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            if (!fileExists)
            {
                await writer.WriteLineAsync(CanvasCsvHeader);
            }

            await writer.WriteLineAsync(row.ToCsvLine());
        }

        if (maxRows > 0)
        {
            await TrimCsvFileAsync(path, maxRows, cancellationToken);
        }
    }

    private static async Task TrimCsvFileAsync(string path, int maxRows, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        if (lines.Length <= maxRows + 1)
        {
            return;
        }

        var header = lines[0];
        var trimmed = lines.Skip(Math.Max(1, lines.Length - maxRows)).ToArray();
        await File.WriteAllLinesAsync(path, new[] { header }.Concat(trimmed), cancellationToken);
    }
}

internal sealed record HoverDiagnosticsRow(
    DateTime TimestampUtc,
    string RunId,
    string BuildHash,
    string? PayloadSignature,
    double InteropDispatches,
    double TotalDispatches,
    double DurationMs,
    double RatePerSecond,
    string Source,
    double? CanvasWidth,
    double? CanvasHeight,
    bool OperationalOnly,
    string Mode,
    bool NeighborEmphasis,
    double? ZoomPercent,
    string? HoveredNodeId,
    string? FocusedNodeId,
    double? NodeCount,
    double? EdgeCount,
    bool InspectorVisible,
    double PointerThrottleSkips,
    double PointerEventsReceived,
    double PointerEventsProcessed,
    double PointerQueueDrops,
    double PointerIntentSkips,
    double DragFrameCount,
    double DragTotalDurationMs,
    double DragAverageFrameMs,
    double DragMaxFrameMs,
    double SceneRebuilds,
    double OverlayUpdates,
    double LayoutReads,
    double PointerInpSampleCount,
    double PointerInpAverageMs,
    double PointerInpMaxMs)
{
    public string ToCsvLine()
    {
        string FormatDouble(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string FormatNullableDouble(double? value) => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
        string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\"", "\"\"");
            return $"\"{normalized}\"";
        }

        return string.Join(",",
            Escape(TimestampUtc.ToString("o", CultureInfo.InvariantCulture)),
            Escape(RunId),
            Escape(BuildHash),
            Escape(PayloadSignature),
            FormatDouble(InteropDispatches),
            FormatDouble(TotalDispatches),
            FormatDouble(DurationMs),
            FormatDouble(RatePerSecond),
            Escape(Source),
            FormatNullableDouble(CanvasWidth),
            FormatNullableDouble(CanvasHeight),
            OperationalOnly ? "true" : "false",
            Escape(Mode),
            NeighborEmphasis ? "true" : "false",
            FormatNullableDouble(ZoomPercent),
            Escape(HoveredNodeId),
            Escape(FocusedNodeId),
            FormatNullableDouble(NodeCount),
            FormatNullableDouble(EdgeCount),
            InspectorVisible ? "true" : "false",
            FormatDouble(PointerThrottleSkips),
            FormatDouble(PointerEventsReceived),
            FormatDouble(PointerEventsProcessed),
            FormatDouble(PointerQueueDrops),
            FormatDouble(PointerIntentSkips),
            FormatDouble(DragFrameCount),
            FormatDouble(DragTotalDurationMs),
            FormatDouble(DragAverageFrameMs),
            FormatDouble(DragMaxFrameMs),
            FormatDouble(SceneRebuilds),
            FormatDouble(OverlayUpdates),
            FormatDouble(LayoutReads),
            FormatDouble(PointerInpSampleCount),
            FormatDouble(PointerInpAverageMs),
            FormatDouble(PointerInpMaxMs));
    }
}

internal sealed record CanvasDiagnosticsRow(
    DateTime TimestampUtc,
    string RunId,
    string BuildHash,
    string? PayloadSignature,
    double? NodeCount,
    double? EdgeCount,
    double AvgDrawMs,
    double MaxDrawMs,
    double LastDrawMs,
    double FrameCount,
    double PanDistance,
    double ZoomEvents,
    double DragFrameCount,
    double DragTotalDurationMs,
    double DragAverageFrameMs,
    double DragMaxFrameMs,
    string Source,
    double? CanvasWidth,
    double? CanvasHeight,
    bool OperationalOnly,
    string Mode,
    bool NeighborEmphasis,
    double? ZoomPercent,
    bool InspectorVisible)
{
    public string ToCsvLine()
    {
        string FormatDouble(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string FormatNullableDouble(double? value) => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
        string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\"", "\"\"");
            return $"\"{normalized}\"";
        }

        return string.Join(",",
            Escape(TimestampUtc.ToString("o", CultureInfo.InvariantCulture)),
            Escape(RunId),
            Escape(BuildHash),
            Escape(PayloadSignature),
            FormatNullableDouble(NodeCount),
            FormatNullableDouble(EdgeCount),
            FormatDouble(AvgDrawMs),
            FormatDouble(MaxDrawMs),
            FormatDouble(LastDrawMs),
            FormatDouble(FrameCount),
            FormatDouble(PanDistance),
            FormatDouble(ZoomEvents),
            FormatDouble(DragFrameCount),
            FormatDouble(DragTotalDurationMs),
            FormatDouble(DragAverageFrameMs),
            FormatDouble(DragMaxFrameMs),
            Escape(Source),
            FormatNullableDouble(CanvasWidth),
            FormatNullableDouble(CanvasHeight),
            OperationalOnly ? "true" : "false",
            Escape(Mode),
            NeighborEmphasis ? "true" : "false",
            FormatNullableDouble(ZoomPercent),
            InspectorVisible ? "true" : "false");
    }
}
