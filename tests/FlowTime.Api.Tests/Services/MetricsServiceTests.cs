using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlowTime.API.Services;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Api.Tests.Services;

public sealed class MetricsServiceTests : IDisposable
{
    private const string telemetryRunId = "run_metrics_unit";
    private const string simulationRunId = "run_metrics_sim";

    private readonly string artifactsRoot;
    private readonly MetricsService metricsService;

    public MetricsServiceTests()
    {
        artifactsRoot = Path.Combine(Path.GetTempPath(), $"metrics_service_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ArtifactsDirectory", artifactsRoot)
            })
            .Build();

        CreateTelemetryRun(telemetryRunId);
        CreateSimulationRun(simulationRunId);

        var manifestReader = new RunManifestReader();
        var stateQueryService = new StateQueryService(
            configuration,
            NullLogger<StateQueryService>.Instance,
            manifestReader,
            new ModeValidator());

        metricsService = new MetricsService(configuration, NullLogger<MetricsService>.Instance, stateQueryService, manifestReader);
    }

    [Fact]
    public async Task GetMetricsAsync_ComputesSlaAndMiniBars()
    {
        var response = await metricsService.GetMetricsAsync(telemetryRunId, 0, 3);

        Assert.Equal(4, response.Grid.Bins);
        Assert.Equal(5, response.Grid.BinMinutes);
        Assert.Equal("UTC", response.Window.Timezone);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), response.Window.Start);

        Assert.Equal(2, response.Services.Count);

        var serviceA = Assert.Single(response.Services, s => s.Id == "ServiceA");
        Assert.Equal(1, serviceA.BinsMet);
        Assert.Equal(4, serviceA.BinsTotal);
        Assert.Equal(0.25, serviceA.SlaPct, 6);
        Assert.Equal(new double?[] { 1d, 0.6, 0.8, 0d }, serviceA.Mini);

        var serviceB = Assert.Single(response.Services, s => s.Id == "ServiceB");
        Assert.Equal(2, serviceB.BinsMet);
        Assert.Equal(4, serviceB.BinsTotal);
        Assert.Equal(0.5, serviceB.SlaPct, 6);
        Assert.Equal(new double?[] { 1d, 1d, 0.75, 0.5 }, serviceB.Mini);
    }

    [Fact]
    public async Task GetMetricsAsync_DefaultRangeUsesTailWindow()
    {
        var response = await metricsService.GetMetricsAsync(telemetryRunId, null, null);

        Assert.Equal(4, response.Grid.Bins);
        var serviceA = Assert.Single(response.Services, s => s.Id == "ServiceA");
        Assert.Equal(4, serviceA.Mini.Count);
    }

    [Fact]
    public async Task GetMetricsAsync_InvalidStartBinThrows()
    {
        var ex = await Assert.ThrowsAsync<MetricsQueryException>(() => metricsService.GetMetricsAsync(telemetryRunId, -1, null));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task GetMetricsAsync_MissingRunThrows()
    {
        var ex = await Assert.ThrowsAsync<MetricsQueryException>(() => metricsService.GetMetricsAsync("missing_run", 0, 1));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetMetricsAsync_SimulationSemanticsResolvedFromModel()
    {
        var response = await metricsService.GetMetricsAsync(simulationRunId, 0, 2);

        Assert.Equal(3, response.Grid.Bins);
        var service = Assert.Single(response.Services);
        Assert.Equal("SimService", service.Id);
        Assert.Equal(2, service.BinsMet);
        Assert.Equal(3, service.BinsTotal);
        Assert.Equal(2d / 3d, service.SlaPct, 6);
        Assert.Equal(new double?[] { 1d, 0.96, 0.6 }, service.Mini);
    }

    private void CreateTelemetryRun(string identifier)
    {
        var runDir = Path.Combine(artifactsRoot, identifier);
        var modelDir = Path.Combine(runDir, "model");
        Directory.CreateDirectory(modelDir);

        WriteSeries(modelDir, "ServiceA_arrivals.csv", new[] { 10d, 10d, 10d, 10d });
        WriteSeries(modelDir, "ServiceA_served.csv", new[] { 10d, 6d, 8d, 0d });
        WriteSeries(modelDir, "ServiceA_errors.csv", new[] { 0d, 1d, 0d, 2d });

        WriteSeries(modelDir, "ServiceB_arrivals.csv", new[] { 8d, 8d, 8d, 8d });
        WriteSeries(modelDir, "ServiceB_served.csv", new[] { 8d, 8d, 6d, 4d });
        WriteSeries(modelDir, "ServiceB_errors.csv", new[] { 0d, 0d, 0d, 0d });

        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), BuildTelemetryModelYaml(), Encoding.UTF8);
        WriteMetadata(modelDir, identifier, mode: "telemetry");
        WriteRunJson(runDir, identifier, source: "telemetry", bins: 4);
    }

    private void CreateSimulationRun(string identifier)
    {
        var runDir = Path.Combine(artifactsRoot, identifier);
        var modelDir = Path.Combine(runDir, "model");
        Directory.CreateDirectory(modelDir);

        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), BuildSimulationModelYaml(), Encoding.UTF8);
        WriteMetadata(modelDir, identifier, mode: "simulation");
        WriteRunJson(runDir, identifier, source: "engine", bins: 6);
    }

    private static string BuildTelemetryModelYaml()
    {
        return """
schemaVersion: 1

grid:
  bins: 4
  binSize: 5
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"

topology:
  nodes:
    - id: "ServiceA"
      kind: "service"
      semantics:
        arrivals: "file:ServiceA_arrivals.csv"
        served: "file:ServiceA_served.csv"
        errors: "file:ServiceA_errors.csv"
    - id: "ServiceB"
      kind: "service"
      semantics:
        arrivals: "file:ServiceB_arrivals.csv"
        served: "file:ServiceB_served.csv"
        errors: "file:ServiceB_errors.csv"
  edges: []
""";
    }

    private static string BuildSimulationModelYaml()
    {
        return """
schemaVersion: 1

grid:
  bins: 6
  binSize: 5
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"

topology:
  nodes:
    - id: "SimService"
      kind: "service"
      semantics:
        arrivals: "base_load"
        served: "processed_load"
        errors: "service_errors"
  edges: []

nodes:
  - id: "base_load"
    kind: "const"
    values: [10, 10, 10, 10, 10, 10]
  - id: "processed_load"
    kind: "const"
    values: [10, 9.6, 6, 4, 10, 10]
  - id: "service_errors"
    kind: "expr"
    expr: base_load - processed_load

outputs:
  - series: base_load
    as: base_load.csv
  - series: processed_load
    as: processed_load.csv
  - series: service_errors
    as: service_errors.csv
""";
    }

    private static void WriteMetadata(string modelDirectory, string identifier, string mode)
    {
        var metadata = new
        {
            templateId = "unit-template",
            templateTitle = "Unit Template",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode,
            modelHash = $"sha256:{identifier}"
        };

        var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "metadata.json"), metadataJson, Encoding.UTF8);

        var provenance = new
        {
            source = "unit-test",
            templateId = "unit-template",
            templateVersion = "1.0.0",
            mode,
            modelId = identifier,
            schemaVersion = 1
        };

        var provenanceJson = System.Text.Json.JsonSerializer.Serialize(provenance, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "provenance.json"), provenanceJson, Encoding.UTF8);
    }

    private static void WriteRunJson(string runDirectory, string identifier, string source, int bins)
    {
        var run = new
        {
            schemaVersion = 1,
            runId = identifier,
            engineVersion = "0.0-test",
            source,
            grid = new
            {
                bins,
                binSize = 5,
                binUnit = "minutes",
                timezone = "UTC",
                align = "left"
            },
            modelHash = $"sha256:{identifier}",
            scenarioHash = "sha256:scenario",
            createdUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            warnings = Array.Empty<string>(),
            series = Array.Empty<object>()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(run, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(runDirectory, "run.json"), json, Encoding.UTF8);
    }

    private static void WriteSeries(string directory, string fileName, IReadOnlyList<double> values)
    {
        var path = Path.Combine(directory, fileName);
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.NewLine = "\n";
        writer.WriteLine("bin_index,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{values[i]}"));
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(artifactsRoot))
            {
                Directory.Delete(artifactsRoot, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
