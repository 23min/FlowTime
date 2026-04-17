using System.Globalization;
using FlowTime.Core.Models;

namespace FlowTime.TimeMachine.Telemetry;

/// <summary>
/// Reads a single CSV file (t,value format, one header row) and exposes its
/// values as a <see cref="TelemetryData"/> snapshot under a specified series ID.
///
/// This is the named, injectable form of the <c>file:</c>-referenced CSV input
/// that FlowTime.Core's model parser already supports.  Having it implement
/// <see cref="ITelemetrySource"/> makes the file-read path composable and testable
/// without needing a full run directory.
///
/// CSV format expected:
/// <code>
///   t,value
///   0,10.5
///   1,12.0
///   ...
/// </code>
/// </summary>
public sealed class FileCsvSource : ITelemetrySource
{
    private readonly string filePath;
    private readonly string seriesId;
    private readonly TimeGrid grid;

    /// <param name="filePath">Absolute or relative path to the CSV file.</param>
    /// <param name="seriesId">Node ID to assign the parsed series to in <see cref="TelemetryData.Series"/>.</param>
    /// <param name="grid">Grid that defines how many bins to expect; used to validate series length.</param>
    public FileCsvSource(string filePath, string seriesId, TimeGrid grid)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be null or whitespace.", nameof(filePath));
        }

        if (string.IsNullOrWhiteSpace(seriesId))
        {
            throw new ArgumentException("Series ID must not be null or whitespace.", nameof(seriesId));
        }

        this.filePath = Path.GetFullPath(filePath);
        this.seriesId = seriesId;
        this.grid = grid;
    }

    /// <inheritdoc/>
    public async Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSV file '{filePath}' was not found.", filePath);
        }

        var values = await ParseCsvAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (values.Length != grid.Bins)
        {
            throw new InvalidDataException(
                $"CSV file '{filePath}' contains {values.Length} data rows but the grid expects {grid.Bins} bins.");
        }

        return new TelemetryData
        {
            Grid = grid,
            Series = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                [seriesId] = values,
            },
            Provenance = new TelemetryProvenance
            {
                SourcePath = filePath,
                CapturedAt = DateTimeOffset.UtcNow,
            },
        };
    }

    private static async Task<double[]> ParseCsvAsync(string path, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"CSV file '{path}' must have at least one header row and one data row.");
        }

        var values = new List<double>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var commaIndex = line.IndexOf(',');
            if (commaIndex < 0)
            {
                throw new InvalidDataException(
                    $"CSV file '{path}' line {i + 1}: expected 't,value' but got '{line}'.");
            }

            var valueSpan = line.AsSpan(commaIndex + 1);
            if (!double.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidDataException(
                    $"CSV file '{path}' line {i + 1}: cannot parse value '{line[(commaIndex + 1)..]}'.");
            }

            values.Add(value);
        }

        return values.ToArray();
    }
}
