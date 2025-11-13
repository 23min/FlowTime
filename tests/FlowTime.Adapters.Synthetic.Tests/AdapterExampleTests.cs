using FlowTime.Adapters.Synthetic;
using Xunit;

namespace FlowTime.Adapters.Synthetic.Tests;

/// <summary>
/// Integration tests and examples showing how to use the file adapter
/// </summary>
public class AdapterExampleTests
{
    [Fact]
    public async Task Example_ReadSimRunArtifacts_WorksEndToEnd()
    {
        // Setup: Create a sample run directory with FlowTime-Sim format artifacts
        var testRunPath = Path.Combine(Path.GetTempPath(), "flowtime-example-" + Guid.NewGuid().ToString("N")[..8]);
        CreateSampleRunArtifacts(testRunPath);

        try
        {
            // Create reader and adapter
            var reader = new FileSeriesReader();
            var adapter = new RunArtifactAdapter(reader, testRunPath);

            // Read manifest
            var manifest = await adapter.GetManifestAsync();
            Assert.Equal("sim", manifest.Source);
            Assert.Equal(4, manifest.Grid.Bins);
            Assert.Equal(60, manifest.Grid.BinMinutes);

            // Read series index
            var index = await adapter.GetIndexAsync();
            Assert.Equal(3, index.Series.Length);

            // Read individual series
            var demandSeries = await adapter.GetSeriesAsync("demand@COMP_A");
            var servedSeries = await adapter.GetSeriesAsync("served@COMP_A");
            var backlogSeries = await adapter.GetSeriesAsync("backlog@COMP_A");

            // Verify data
            Assert.Equal(4, demandSeries.Length);
            Assert.Equal(100.0, demandSeries[0]);
            Assert.Equal(4, backlogSeries.Length); // Complete series

            // Read multiple series at once (handles missing gracefully)
            var multiSeries = await adapter.GetSeriesAsync("demand@COMP_A", "served@COMP_A", "nonexistent@COMP_A");
            Assert.NotNull(multiSeries["demand@COMP_A"]);
            Assert.NotNull(multiSeries["served@COMP_A"]);
            Assert.Null(multiSeries["nonexistent@COMP_A"]);

            // Get all series for a component
            var componentSeries = await adapter.GetComponentSeriesAsync("COMP_A");
            Assert.Equal(3, componentSeries.Count);

            // Validate artifacts
            var validation = await adapter.ValidateAsync();
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                var warnings = string.Join(", ", validation.Warnings);
                throw new Exception($"Validation failed. Errors: [{errors}]. Warnings: [{warnings}]");
            }
            Assert.True(validation.IsValid);

            // Get FlowTime.Core compatible grid
            var coreGrid = await adapter.GetCoreTimeGridAsync();
            Assert.Equal(4, coreGrid.Bins);
            Assert.Equal(60, coreGrid.BinMinutes);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testRunPath))
            {
                Directory.Delete(testRunPath, true);
            }
        }
    }

    private static void CreateSampleRunArtifacts(string runPath)
    {
        Directory.CreateDirectory(runPath);
        var seriesDir = Path.Combine(runPath, "series");
        Directory.CreateDirectory(seriesDir);

        // Create run.json with SIM-M2 format
        var runJson = """
        {
          "schemaVersion": 1,
          "runId": "sim_2025-09-03T12-00-00Z_abc12345",
          "engineVersion": "sim-0.2.0",
          "source": "sim",
          "grid": { "bins": 4, "binSize": 1, "binUnit": "hours", "timezone": "UTC", "align": "left" },
          "scenarioHash": "sha256:sample123",
          "createdUtc": "2025-09-03T12:00:00Z",
          "warnings": [
            {
              "code": "pmf_normalized",
              "message": "PMF normalized",
              "nodeId": "COMP_A",
              "bins": [0],
              "value": null
            }
          ],
          "series": [
            { "id": "demand@COMP_A", "path": "series/demand@COMP_A.csv", "unit": "entities/bin" },
            { "id": "served@COMP_A", "path": "series/served@COMP_A.csv", "unit": "entities/bin" },
            { "id": "backlog@COMP_A", "path": "series/backlog@COMP_A.csv", "unit": "entities" }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(runPath, "run.json"), runJson);

        // Create series/index.json
        var indexJson = """
        {
          "schemaVersion": 1,
          "grid": { "bins": 4, "binSize": 1, "binUnit": "hours", "timezone": "UTC" },
          "series": [
            {
              "id": "demand@COMP_A",
              "kind": "flow",
              "path": "series/demand@COMP_A.csv",
              "unit": "entities/bin",
              "componentId": "COMP_A",
              "class": "DEFAULT",
              "points": 4,
              "hash": "sha256:demand123"
            },
            {
              "id": "served@COMP_A",
              "kind": "flow",
              "path": "series/served@COMP_A.csv",
              "unit": "entities/bin",
              "componentId": "COMP_A",
              "class": "DEFAULT",
              "points": 4,
              "hash": "sha256:served123"
            },
            {
              "id": "backlog@COMP_A",
              "kind": "state",
              "path": "series/backlog@COMP_A.csv",
              "unit": "entities",
              "componentId": "COMP_A",
              "class": "DEFAULT",
              "points": 4,
              "hash": "sha256:backlog123"
            }
          ],
          "formats": {
            "aggregatesTable": {
                "path": "aggregates/node_time_bin.parquet",
              "dimensions": ["time_bin", "component_id", "class"],
              "measures": ["arrivals", "served", "errors"]
            }
          }
        }
        """;
        File.WriteAllText(Path.Combine(seriesDir, "index.json"), indexJson);

        // Create CSV files with realistic data
        var demandCsv = """
        t,value
        0,100.0
        1,120.0
        2,150.0
        3,80.0
        """;
        File.WriteAllText(Path.Combine(seriesDir, "demand@COMP_A.csv"), demandCsv);

        var servedCsv = """
        t,value
        0,90.0
        1,110.0
        2,140.0
        3,85.0
        """;
        File.WriteAllText(Path.Combine(seriesDir, "served@COMP_A.csv"), servedCsv);

        // Backlog series (state level) - complete series
        var backlogCsv = """
        t,value
        0,0.0
        1,10.0
        2,20.0
        3,15.0
        """;
        File.WriteAllText(Path.Combine(seriesDir, "backlog@COMP_A.csv"), backlogCsv);
    }
}
