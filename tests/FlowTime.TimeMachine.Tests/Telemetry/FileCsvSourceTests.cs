using System.Globalization;
using FlowTime.Core.Models;
using FlowTime.TimeMachine.Telemetry;

namespace FlowTime.TimeMachine.Tests.Telemetry;

public sealed class FileCsvSourceTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public FileCsvSourceTests()
    {
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(tempDir, recursive: true);
    }

    private string WriteCsv(string fileName, params double[] values)
    {
        var path = Path.Combine(tempDir, fileName);
        var lines = new List<string> { "t,value" };
        for (var i = 0; i < values.Length; i++)
        {
            lines.Add($"{i},{values[i].ToString(CultureInfo.InvariantCulture)}");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    private static TimeGrid Grid(int bins = 4) =>
        new(bins, 15, TimeUnit.Minutes);

    // ── Constructor guards ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FileCsvSource(null!, "demand", Grid()));
    }

    [Fact]
    public void Constructor_WhitespacePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FileCsvSource("   ", "demand", Grid()));
    }

    [Fact]
    public void Constructor_NullSeriesId_Throws()
    {
        var path = WriteCsv("demand.csv", 1, 2, 3, 4);
        Assert.Throws<ArgumentException>(() => new FileCsvSource(path, null!, Grid()));
    }

    [Fact]
    public void Constructor_EmptySeriesId_Throws()
    {
        var path = WriteCsv("demand.csv", 1, 2, 3, 4);
        Assert.Throws<ArgumentException>(() => new FileCsvSource(path, "", Grid()));
    }

    [Fact]
    public void Constructor_WhitespaceSeriesId_Throws()
    {
        var path = WriteCsv("demand.csv", 1, 2, 3, 4);
        Assert.Throws<ArgumentException>(() => new FileCsvSource(path, "   ", Grid()));
    }

    // ── ReadAsync happy path ───────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_ReturnsCorrectValues()
    {
        var path = WriteCsv("demand.csv", 10.0, 20.0, 30.0, 40.0);
        var source = new FileCsvSource(path, "demand", Grid(4));

        var data = await source.ReadAsync();

        Assert.True(data.Series.ContainsKey("demand"));
        Assert.Equal([10.0, 20.0, 30.0, 40.0], data.Series["demand"]);
    }

    [Fact]
    public async Task ReadAsync_ReturnsCorrectGrid()
    {
        var path = WriteCsv("d.csv", 1, 2, 3, 4);
        var source = new FileCsvSource(path, "d", Grid(4));

        var data = await source.ReadAsync();

        Assert.Equal(4, data.Grid.Bins);
        Assert.Equal(15, data.Grid.BinSize);
        Assert.Equal(TimeUnit.Minutes, data.Grid.BinUnit);
    }

    [Fact]
    public async Task ReadAsync_SetsProvenance_SourcePath()
    {
        var path = WriteCsv("d.csv", 1, 2, 3, 4);
        var source = new FileCsvSource(path, "d", Grid(4));

        var data = await source.ReadAsync();

        Assert.NotNull(data.Provenance);
        Assert.Equal(Path.GetFullPath(path), data.Provenance!.SourcePath);
        Assert.NotNull(data.Provenance.CapturedAt);
    }

    [Fact]
    public async Task ReadAsync_SeriesIdIsCaseInsensitiveKey()
    {
        var path = WriteCsv("d.csv", 1, 2, 3, 4);
        var source = new FileCsvSource(path, "Demand", Grid(4));

        var data = await source.ReadAsync();

        // Dictionary uses OrdinalIgnoreCase
        Assert.True(data.Series.ContainsKey("demand"));
        Assert.True(data.Series.ContainsKey("DEMAND"));
    }

    [Fact]
    public async Task ReadAsync_HandlesDecimalValues()
    {
        var path = WriteCsv("d.csv", 1.5, 2.75, 3.125, 0.0);
        var source = new FileCsvSource(path, "d", Grid(4));

        var data = await source.ReadAsync();

        Assert.Equal([1.5, 2.75, 3.125, 0.0], data.Series["d"]);
    }

    [Fact]
    public async Task ReadAsync_SkipsBlankLines()
    {
        // Write CSV with a blank trailing line
        var path = Path.Combine(tempDir, "blank.csv");
        File.WriteAllText(path, "t,value\n0,10\n1,20\n2,30\n3,40\n\n");
        var source = new FileCsvSource(path, "x", Grid(4));

        var data = await source.ReadAsync();

        Assert.Equal(4, data.Series["x"].Length);
    }

    // ── ReadAsync error cases ──────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_MissingFile_Throws()
    {
        var source = new FileCsvSource(Path.Combine(tempDir, "nonexistent.csv"), "d", Grid(4));

        await Assert.ThrowsAsync<FileNotFoundException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TooFewRows_Throws()
    {
        // CSV has 2 data rows but grid expects 4 bins
        var path = WriteCsv("short.csv", 1.0, 2.0);
        var source = new FileCsvSource(path, "x", Grid(4));

        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TooManyRows_Throws()
    {
        // CSV has 6 data rows but grid expects 4 bins
        var path = WriteCsv("long.csv", 1.0, 2.0, 3.0, 4.0, 5.0, 6.0);
        var source = new FileCsvSource(path, "x", Grid(4));

        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_InvalidNumericValue_Throws()
    {
        var path = Path.Combine(tempDir, "bad.csv");
        File.WriteAllText(path, "t,value\n0,10\n1,not_a_number\n2,30\n3,40\n");
        var source = new FileCsvSource(path, "x", Grid(4));

        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_MissingComma_Throws()
    {
        var path = Path.Combine(tempDir, "nocomma.csv");
        File.WriteAllText(path, "t,value\nonly_one_column\n");
        var source = new FileCsvSource(path, "x", Grid(1));

        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_HeaderOnly_Throws()
    {
        var path = Path.Combine(tempDir, "headeronly.csv");
        File.WriteAllText(path, "t,value\n");
        var source = new FileCsvSource(path, "x", Grid(4));

        // Either InvalidDataException (wrong count) or InvalidDataException (too short)
        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync());
    }
}
