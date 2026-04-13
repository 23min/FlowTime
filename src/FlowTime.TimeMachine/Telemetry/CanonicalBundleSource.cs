using FlowTime.Adapters.Synthetic;
using FlowTime.Core.Models;

namespace FlowTime.TimeMachine.Telemetry;

/// <summary>
/// Reads a canonical bundle directory (series/index.json + series/*.csv) and
/// returns a <see cref="TelemetryData"/> snapshot for replay into the Time Machine.
///
/// A canonical bundle is produced by the Time Machine's <c>TelemetryBundleBuilder</c>
/// and is the replay side of the telemetry loop.  This source implements that loop's
/// read half: given a bundle directory, produce the deterministic inputs the Time
/// Machine needs to reproduce the captured run.
///
/// Directory layout expected:
/// <code>
///   bundleDirectory/
///     series/
///       index.json          ← grid + series metadata
///       demand.csv          ← t,value CSV per series
///       served.csv
///       ...
/// </code>
/// </summary>
public sealed class CanonicalBundleSource : ITelemetrySource
{
    private readonly string bundleDirectory;
    private readonly ISeriesReader seriesReader;

    /// <param name="bundleDirectory">Absolute or relative path to the bundle directory.</param>
    /// <param name="seriesReader">
    /// Injected reader; defaults to <see cref="FileSeriesReader"/> when null.
    /// Pass a test double to unit-test without disk I/O.
    /// </param>
    public CanonicalBundleSource(string bundleDirectory, ISeriesReader? seriesReader = null)
    {
        if (string.IsNullOrWhiteSpace(bundleDirectory))
        {
            throw new ArgumentException("Bundle directory must not be null or whitespace.", nameof(bundleDirectory));
        }

        this.bundleDirectory = Path.GetFullPath(bundleDirectory);
        this.seriesReader = seriesReader ?? new FileSeriesReader();
    }

    /// <inheritdoc/>
    public async Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(bundleDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Canonical bundle directory '{bundleDirectory}' was not found.");
        }

        var index = await seriesReader.ReadIndexAsync(bundleDirectory).ConfigureAwait(false);
        var grid = ToTimeGrid(index.Grid);

        var series = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var meta in index.Series)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seriesReader.SeriesExists(bundleDirectory, meta.Id))
            {
                continue;
            }

            var s = await seriesReader.ReadSeriesAsync(bundleDirectory, meta.Id).ConfigureAwait(false);
            series[meta.Id] = s.ToArray();
        }

        return new TelemetryData
        {
            Grid = grid,
            Series = series,
            Provenance = new TelemetryProvenance
            {
                SourcePath = bundleDirectory,
                CapturedAt = DateTimeOffset.UtcNow,
            },
        };
    }

    private static FlowTime.Core.Models.TimeGrid ToTimeGrid(FlowTime.Adapters.Synthetic.TimeGrid g) =>
        new(g.Bins, g.BinSize, TimeUnitExtensions.Parse(g.BinUnit));
}
