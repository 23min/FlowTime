using FlowTime.Generator.Capture;
using FlowTime.Tests.Support;

namespace FlowTime.Generator.Tests;

public sealed class RunArtifactReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsBindings_FromTopologySemantics()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_reader_topology", includeTopology: true);

        var reader = new RunArtifactReader();
        var context = await reader.ReadAsync(runDir);

        Assert.Equal(6, context.SeriesBindings.Count);
        Assert.Contains(context.SeriesBindings, b => b.NodeId == "OrderService" && b.Metric.ToString() == "Arrivals");
        Assert.Contains(context.SeriesBindings, b => b.NodeId == "PaymentService" && b.Metric.ToString() == "Errors");
    }

    [Fact]
    public async Task ReadAsync_FallbacksToOutputs_WhenTopologyMissing()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_reader_outputs", includeTopology: false);

        var reader = new RunArtifactReader();
        var context = await reader.ReadAsync(runDir);

        Assert.NotEmpty(context.SeriesBindings);
        Assert.Contains(context.SeriesBindings, b => b.NodeId == "OrderService" && b.Metric.ToString() == "Arrivals");
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptyBindings_WhenTopologyAndOutputsMissing()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_reader_empty", includeTopology: false);

        var specPath = Path.Combine(runDir, "spec.yaml");
        var spec = """
schemaVersion: 1
metadata:
  id: minimal
  title: Minimal
  version: 1.0.0
grid:
  bins: 4
  binSize: 5
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"
topology:
  nodes: []
  edges: []
nodes: []
outputs: []
""";
        await File.WriteAllTextAsync(specPath, spec);

        var reader = new RunArtifactReader();
        var context = await reader.ReadAsync(runDir);

        Assert.Empty(context.SeriesBindings);
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenSpecMissing()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_reader_missing_spec", includeTopology: true);

        File.Delete(Path.Combine(runDir, "spec.yaml"));

        var reader = new RunArtifactReader();
        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(runDir));
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenIndexMissing()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_reader_missing_index", includeTopology: true);

        File.Delete(Path.Combine(runDir, "series", "index.json"));

        var reader = new RunArtifactReader();
        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(runDir));
    }
}
