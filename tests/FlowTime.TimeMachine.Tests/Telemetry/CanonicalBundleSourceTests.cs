using System.Globalization;
using System.Text.Json;
using FlowTime.Adapters.Synthetic;
using FlowTime.Core.Models;
using FlowTime.TimeMachine.Telemetry;

namespace FlowTime.TimeMachine.Tests.Telemetry;

/// <summary>
/// Tests for <see cref="CanonicalBundleSource"/>.
///
/// Each test creates an in-memory bundle directory (series/index.json + CSV files)
/// to avoid depending on the full TelemetryBundleBuilder pipeline.
/// </summary>
public sealed class CanonicalBundleSourceTests : IDisposable
{
    private readonly string bundleDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public CanonicalBundleSourceTests()
    {
        Directory.CreateDirectory(bundleDir);
        Directory.CreateDirectory(Path.Combine(bundleDir, "series"));
    }

    public void Dispose()
    {
        Directory.Delete(bundleDir, recursive: true);
    }

    // ── Fixture helpers ────────────────────────────────────────────────────

    private void WriteIndex(int bins, int binSize, string binUnit, params string[] seriesIds)
    {
        var seriesArray = seriesIds.Select((id, i) => new
        {
            id,
            kind = "flow",
            path = $"series/{id}.csv",
            unit = "units",
            componentId = id,
            @class = "DEFAULT",
            points = bins,
            hash = $"hash{i}"
        });

        var indexDoc = new
        {
            schemaVersion = 1,
            grid = new { bins, binSize, binUnit, timezone = "UTC", align = "left" },
            series = seriesArray,
        };

        var indexPath = Path.Combine(bundleDir, "series", "index.json");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(indexDoc));
    }

    private void WriteCsv(string seriesId, params double[] values)
    {
        var path = Path.Combine(bundleDir, "series", $"{seriesId}.csv");
        var lines = new List<string> { "t,value" };
        for (var i = 0; i < values.Length; i++)
        {
            lines.Add($"{i},{values[i].ToString(CultureInfo.InvariantCulture)}");
        }
        File.WriteAllLines(path, lines);
    }

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullDirectory_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalBundleSource(null!));
    }

    [Fact]
    public void Constructor_WhitespaceDirectory_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalBundleSource("   "));
    }

    // ── ReadAsync happy path ───────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_SingleSeries_ReturnsCorrectValues()
    {
        WriteIndex(4, 15, "minutes", "demand");
        WriteCsv("demand", 10.0, 20.0, 30.0, 40.0);

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.True(data.Series.ContainsKey("demand"));
        Assert.Equal([10.0, 20.0, 30.0, 40.0], data.Series["demand"]);
    }

    [Fact]
    public async Task ReadAsync_MultipleSeries_AllPresent()
    {
        WriteIndex(4, 15, "minutes", "arrivals", "served", "capacity");
        WriteCsv("arrivals", 15, 15, 15, 15);
        WriteCsv("served", 10, 10, 10, 10);
        WriteCsv("capacity", 12, 12, 12, 12);

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.Equal(3, data.Series.Count);
        Assert.Equal([15.0, 15.0, 15.0, 15.0], data.Series["arrivals"]);
        Assert.Equal([10.0, 10.0, 10.0, 10.0], data.Series["served"]);
        Assert.Equal([12.0, 12.0, 12.0, 12.0], data.Series["capacity"]);
    }

    [Fact]
    public async Task ReadAsync_ReturnsCorrectGrid()
    {
        WriteIndex(8, 1, "hours", "demand");
        WriteCsv("demand", 1, 2, 3, 4, 5, 6, 7, 8);

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.Equal(8, data.Grid.Bins);
        Assert.Equal(1, data.Grid.BinSize);
        Assert.Equal(TimeUnit.Hours, data.Grid.BinUnit);
    }

    [Fact]
    public async Task ReadAsync_SetsProvenance_SourcePath()
    {
        WriteIndex(2, 60, "minutes", "x");
        WriteCsv("x", 1.0, 2.0);

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.NotNull(data.Provenance);
        Assert.Equal(Path.GetFullPath(bundleDir), data.Provenance!.SourcePath);
    }

    [Fact]
    public async Task ReadAsync_MissingCsvFile_SkipsSeriesGracefully()
    {
        // Index references "served" but the CSV is absent
        WriteIndex(4, 15, "minutes", "arrivals", "served");
        WriteCsv("arrivals", 1, 2, 3, 4);
        // "served.csv" intentionally NOT written

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.True(data.Series.ContainsKey("arrivals"));
        Assert.False(data.Series.ContainsKey("served"));
    }

    [Fact]
    public async Task ReadAsync_EmptyIndex_ReturnsEmptySeries()
    {
        WriteIndex(4, 15, "minutes");  // no series

        var source = new CanonicalBundleSource(bundleDir);
        var data = await source.ReadAsync();

        Assert.Empty(data.Series);
    }

    // ── ReadAsync error cases ──────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_NonExistentDirectory_Throws()
    {
        var source = new CanonicalBundleSource(Path.Combine(bundleDir, "nonexistent"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_MissingIndexJson_Throws()
    {
        // series/ dir exists but index.json is absent
        var source = new CanonicalBundleSource(bundleDir);

        await Assert.ThrowsAsync<FileNotFoundException>(() => source.ReadAsync());
    }
}
