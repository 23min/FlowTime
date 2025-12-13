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
    private const string CsvHeader = "timestampUtc,runId,buildHash,payloadSignature,interopDispatches,totalDispatches,durationMs,ratePerSecond,source,canvasWidth,canvasHeight,operationalOnly,mode,neighborEmphasis,zoomPercent";

    public static async Task AppendHoverEntryAsync(string path, HoverDiagnosticsRow row, int maxRows, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var fileExists = File.Exists(path);
        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            if (!fileExists)
            {
                await writer.WriteLineAsync(CsvHeader);
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
    double? ZoomPercent)
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
            FormatNullableDouble(ZoomPercent));
    }
}
