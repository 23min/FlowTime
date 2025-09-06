using FlowTime.Adapters.Synthetic;
using Xunit;

namespace FlowTime.Adapters.Synthetic.Tests;

public class FileSeriesReaderTests
{
    private readonly string testDataPath;

    public FileSeriesReaderTests()
    {
        testDataPath = Path.Combine(Path.GetTempPath(), "flowtime-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(testDataPath);
        SetupTestData();
    }

    [Fact]
    public async Task ReadRunInfoAsync_ValidRunJson_ReturnsCorrectManifest()
    {
        var reader = new FileSeriesReader();
        var manifest = await reader.ReadRunInfoAsync(testDataPath);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("sim_2025-09-01T18-30-12Z_a1b2c3d4", manifest.RunId);
        Assert.Equal("sim-0.1.0", manifest.EngineVersion);
        Assert.Equal("sim", manifest.Source);
        Assert.Equal(4, manifest.Grid.Bins);
        Assert.Equal(60, manifest.Grid.BinMinutes);
        Assert.Equal("UTC", manifest.Grid.Timezone);
        Assert.Equal("left", manifest.Grid.Align);
        Assert.Equal(2, manifest.Series.Length);
    }

    [Fact]
    public async Task ReadIndexAsync_ValidIndexJson_ReturnsCorrectIndex()
    {
        var reader = new FileSeriesReader();
        var index = await reader.ReadIndexAsync(testDataPath);

        Assert.Equal(1, index.SchemaVersion);
        Assert.Equal(4, index.Grid.Bins);
        Assert.Equal(60, index.Grid.BinMinutes);
        Assert.Equal(2, index.Series.Length);

        var demandSeries = index.Series.First(s => s.Id == "demand@COMP_A");
        Assert.Equal("flow", demandSeries.Kind);
        Assert.Equal("entities/bin", demandSeries.Unit);
        Assert.Equal("COMP_A", demandSeries.ComponentId);
        Assert.Equal("DEFAULT", demandSeries.Class);
        Assert.Equal(4, demandSeries.Points);
    }

    [Fact]
    public async Task ReadSeriesAsync_ValidCsv_ReturnsCorrectSeries()
    {
        var reader = new FileSeriesReader();
        var series = await reader.ReadSeriesAsync(testDataPath, "demand@COMP_A");

        Assert.Equal(4, series.Length);
        Assert.Equal(10.0, series[0]);
        Assert.Equal(20.0, series[1]);
        Assert.Equal(30.0, series[2]);
        Assert.Equal(40.0, series[3]);
    }

    [Fact]
    public void SeriesExists_ExistingFile_ReturnsTrue()
    {
        var reader = new FileSeriesReader();
        Assert.True(reader.SeriesExists(testDataPath, "demand@COMP_A"));
    }

    [Fact]
    public void SeriesExists_NonExistingFile_ReturnsFalse()
    {
        var reader = new FileSeriesReader();
        Assert.False(reader.SeriesExists(testDataPath, "nonexistent@COMP_A"));
    }

    [Fact]
    public async Task ReadSeriesAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var reader = new FileSeriesReader();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.ReadSeriesAsync(testDataPath, "nonexistent@COMP_A"));
    }

    private void SetupTestData()
    {
        // Create run.json
        var runJson = """
        {
          "schemaVersion": 1,
          "runId": "sim_2025-09-01T18-30-12Z_a1b2c3d4",
          "engineVersion": "sim-0.1.0",
          "source": "sim",
          "grid": { "bins": 4, "binMinutes": 60, "timezone": "UTC", "align": "left" },
          "scenarioHash": "sha256:test123",
          "createdUtc": "2025-09-01T18:30:12Z",
          "warnings": [],
          "series": [
            { "id": "demand@COMP_A", "path": "series/demand@COMP_A.csv", "unit": "entities/bin" },
            { "id": "served@COMP_A", "path": "series/served@COMP_A.csv", "unit": "entities/bin" }
          ],
          "events": { "schemaVersion": 0, "fieldsReserved": [] }
        }
        """;
        File.WriteAllText(Path.Combine(testDataPath, "run.json"), runJson);

        // Create series directory and index.json
        var seriesDir = Path.Combine(testDataPath, "series");
        Directory.CreateDirectory(seriesDir);

        var indexJson = """
        {
          "schemaVersion": 1,
          "grid": { "bins": 4, "binMinutes": 60, "timezone": "UTC" },
          "series": [
            {
              "id": "demand@COMP_A",
              "kind": "flow",
              "path": "series/demand@COMP_A.csv",
              "unit": "entities/bin",
              "componentId": "COMP_A",
              "class": "DEFAULT",
              "points": 4,
              "hash": "sha256:test123"
            },
            {
              "id": "served@COMP_A",
              "kind": "flow",
              "path": "series/served@COMP_A.csv",
              "unit": "entities/bin",
              "componentId": "COMP_A",
              "class": "DEFAULT",
              "points": 4,
              "hash": "sha256:test456"
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(seriesDir, "index.json"), indexJson);

        // Create CSV files
        var demandCsv = """
        t,value
        0,10.0
        1,20.0
        2,30.0
        3,40.0
        """;
        File.WriteAllText(Path.Combine(seriesDir, "demand@COMP_A.csv"), demandCsv);

        var servedCsv = """
        t,value
        0,8.0
        1,16.0
        2,24.0
        3,32.0
        """;
        File.WriteAllText(Path.Combine(seriesDir, "served@COMP_A.csv"), servedCsv);
    }
}
