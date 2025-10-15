using System.Globalization;
using System.Linq;
using FlowTime.API.Services;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace FlowTime.Generator.Tests;

public sealed class TelemetryStateGoldenTests
{
    private readonly ITestOutputHelper output;

    public TelemetryStateGoldenTests(ITestOutputHelper output) => this.output = output;

    public static IEnumerable<object[]> FixtureData => new[]
    {
        new object[] { FixtureKind.OrderSystem, "state_order_system" },
        new object[] { FixtureKind.Microservices, "state_microservices" },
        new object[] { FixtureKind.HttpService, "state_http_service" }
    };

    [Theory]
    [MemberData(nameof(FixtureData))]
    public async Task BundledTelemetryProducesExpectedState(FixtureKind kind, string runId)
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, runId, kind, includeTopology: true);

        var captureDir = Path.Combine(temp.Path, "capture");
        var capture = new TelemetryCapture();
        var capturePlan = await capture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = captureDir
        });
        Assert.Empty(capturePlan.Warnings);

        var definition = TelemetryRunFactory.GetDefinition(kind);

        var templatePath = Path.Combine(temp.Path, "template.yaml");
        await File.WriteAllTextAsync(templatePath, BuildTemplateYaml(captureDir, definition));

        var outputRoot = Path.Combine(temp.Path, "runs");
        Directory.CreateDirectory(outputRoot);

        var bundle = new TelemetryBundleBuilder();
        var bundleResult = await bundle.BuildAsync(new TelemetryBundleOptions
        {
            CaptureDirectory = captureDir,
            ModelPath = templatePath,
            OutputRoot = outputRoot,
            DeterministicRunId = true
        });

        var logger = new ListLogger<StateQueryService>();
        var stateService = TestStateQueryServiceFactory.Create(bundleResult.RunDirectory, logger);
        var snapshot = await stateService.GetStateAsync(bundleResult.RunId, binIndex: 0, CancellationToken.None);

        Assert.Equal("telemetry", snapshot.Metadata.Mode);
        Assert.True(snapshot.Metadata.TelemetrySourcesResolved);

        var seriesLookup = definition.Series.ToDictionary(s => s.SeriesId, s => s.Values);

        foreach (var service in definition.Services)
        {
            var node = snapshot.Nodes.Single(n => n.Id == service.NodeId);
            if (!string.IsNullOrWhiteSpace(service.Arrivals))
            {
                Assert.Equal(seriesLookup[service.Arrivals][0], node.Metrics.Arrivals);
            }

            if (!string.IsNullOrWhiteSpace(service.Served))
            {
                Assert.Equal(seriesLookup[service.Served][0], node.Metrics.Served);
            }

            if (!string.IsNullOrWhiteSpace(service.Errors))
            {
                Assert.Equal(seriesLookup[service.Errors][0], node.Metrics.Errors);
            }

            if (!string.IsNullOrWhiteSpace(service.ExternalDemand))
            {
                Assert.Equal(seriesLookup[service.ExternalDemand][0], node.Metrics.ExternalDemand);
            }

            if (!string.IsNullOrWhiteSpace(service.QueueDepth))
            {
                Assert.Equal(seriesLookup[service.QueueDepth][0], node.Metrics.Queue);
            }

            if (!string.IsNullOrWhiteSpace(service.Capacity))
            {
                Assert.Equal(seriesLookup[service.Capacity][0], node.Metrics.Capacity);
            }

            Assert.All(node.Telemetry.Sources, source => Assert.StartsWith("file://telemetry/", source, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(logger.Entries, e => e.EventId.Id == 3001 && e.Level == LogLevel.Information);

        var window = await stateService.GetStateWindowAsync(bundleResult.RunId, startBin: 0, endBin: definition.Bins - 1, CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.EventId.Id == 3002 && e.Level == LogLevel.Information);

        Assert.Equal("telemetry", window.Metadata.Mode);
        Assert.Equal(definition.Bins, window.Window.BinCount);

        foreach (var service in definition.Services)
        {
            var nodeSeries = window.Nodes.Single(n => n.Id == service.NodeId);
            if (!string.IsNullOrWhiteSpace(service.Arrivals))
            {
                AssertSeriesEqual(seriesLookup[service.Arrivals], RequireSeries(nodeSeries.Series, "arrivals"));
            }

            if (!string.IsNullOrWhiteSpace(service.Served))
            {
                AssertSeriesEqual(seriesLookup[service.Served], RequireSeries(nodeSeries.Series, "served"));
            }

            if (!string.IsNullOrWhiteSpace(service.Errors))
            {
                AssertSeriesEqual(seriesLookup[service.Errors], RequireSeries(nodeSeries.Series, "errors"));
            }

            if (!string.IsNullOrWhiteSpace(service.ExternalDemand))
            {
                AssertSeriesEqual(seriesLookup[service.ExternalDemand], RequireSeries(nodeSeries.Series, "externalDemand"));
            }

            if (!string.IsNullOrWhiteSpace(service.QueueDepth))
            {
                AssertSeriesEqual(seriesLookup[service.QueueDepth], RequireSeries(nodeSeries.Series, "queueDepth"));
            }

            if (!string.IsNullOrWhiteSpace(service.Capacity))
            {
                AssertSeriesEqual(seriesLookup[service.Capacity], RequireSeries(nodeSeries.Series, "capacity"));
            }

            Assert.All(nodeSeries.Telemetry.Sources, source => Assert.StartsWith("file://telemetry/", source, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task MissingTelemetryFile_SurfacesWarningsInState()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_missing", FixtureKind.OrderSystem, includeTopology: true);

        var captureDir = Path.Combine(temp.Path, "capture-missing");
        var capture = new TelemetryCapture();
        await capture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = captureDir
        });

        var templatePath = Path.Combine(temp.Path, "template-missing.yaml");
        var definition = TelemetryRunFactory.GetDefinition(FixtureKind.OrderSystem);
        await File.WriteAllTextAsync(templatePath, BuildTemplateYaml(captureDir, definition));

        var outputRoot = Path.Combine(temp.Path, "runs-missing");
        Directory.CreateDirectory(outputRoot);

        var builder = new TelemetryBundleBuilder();
        var bundle = await builder.BuildAsync(new TelemetryBundleOptions
        {
            CaptureDirectory = captureDir,
            ModelPath = templatePath,
            OutputRoot = outputRoot,
            DeterministicRunId = true
        });

        var missingPath = Path.Combine(bundle.RunDirectory, "model", "telemetry", "OrderService_arrivals.csv");
        File.Delete(missingPath);

        var logger = new ListLogger<StateQueryService>();
        var stateService = TestStateQueryServiceFactory.Create(bundle.RunDirectory, logger);
        var snapshot = await stateService.GetStateAsync(bundle.RunId, binIndex: 0, CancellationToken.None);

        Assert.False(snapshot.Metadata.TelemetrySourcesResolved);
        Assert.Contains(snapshot.Warnings, w => w.Code == "telemetry_sources_missing");

        var orderNode = snapshot.Nodes.Single(n => n.Id == "OrderService");
        Assert.Contains(orderNode.Telemetry.Warnings, w => w.Code == "telemetry_sources_unresolved");

        var window = await stateService.GetStateWindowAsync(bundle.RunId, 0, definition.Bins - 1, CancellationToken.None);
        var orderSeries = window.Nodes.Single(n => n.Id == "OrderService");
        Assert.Contains(orderSeries.Telemetry.Warnings, w => w.Code == "telemetry_sources_unresolved");

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Telemetry source missing"));
    }

    private static string BuildTemplateYaml(string captureDir, FixtureDefinition definition)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("schemaVersion: 1");
        sb.AppendLine("mode: telemetry");
        sb.AppendLine("metadata:");
        sb.AppendLine($"  id: {definition.MetadataId}");
        sb.AppendLine($"  title: {definition.MetadataTitle}");
        sb.AppendLine("  version: 1.0.0");
        sb.AppendLine("grid:");
        sb.AppendLine($"  bins: {definition.Bins}");
        sb.AppendLine($"  binSize: {definition.BinSize}");
        sb.AppendLine("  binUnit: minutes");
        sb.AppendLine("  startTimeUtc: \"2025-01-01T00:00:00Z\"");

        sb.AppendLine(BuildTopologySection(definition));
        sb.AppendLine();
        sb.AppendLine("nodes:");
        foreach (var series in definition.Series)
        {
            var uri = new Uri(Path.Combine(captureDir, series.OutputFileName)).AbsoluteUri;
            sb.AppendLine($"  - id: {series.SeriesId}");
            sb.AppendLine("    kind: const");
            sb.AppendLine($"    values: [{string.Join(", ", Enumerable.Repeat(0, definition.Bins))}]");
            sb.AppendLine($"    source: {uri}");
            sb.AppendLine();
        }

        sb.AppendLine("outputs:");
        foreach (var series in definition.Series)
        {
            sb.AppendLine($"  - series: {series.SeriesId}");
            sb.AppendLine($"    as: {series.OutputFileName}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildTopologySection(FixtureDefinition definition)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("topology:");
        sb.AppendLine("  nodes:");
        foreach (var service in definition.Services)
        {
            sb.AppendLine($"    - id: {service.NodeId}");
            sb.AppendLine("      kind: service");
            sb.AppendLine("      semantics:");
            sb.AppendLine($"        arrivals: {service.Arrivals}");
            sb.AppendLine($"        served: {service.Served}");
            if (!string.IsNullOrWhiteSpace(service.Errors))
            {
                sb.AppendLine($"        errors: {service.Errors}");
            }
            if (!string.IsNullOrWhiteSpace(service.ExternalDemand))
            {
                sb.AppendLine($"        externalDemand: {service.ExternalDemand}");
            }
            if (!string.IsNullOrWhiteSpace(service.QueueDepth))
            {
                sb.AppendLine($"        queueDepth: {service.QueueDepth}");
            }
            if (!string.IsNullOrWhiteSpace(service.Capacity))
            {
                sb.AppendLine($"        capacity: {service.Capacity}");
            }
        }

        if (definition.Edges.Count == 0)
        {
            sb.Append("  edges: []");
        }
        else
        {
            sb.AppendLine("  edges:");
            foreach (var edge in definition.Edges)
            {
                var edgeId = string.IsNullOrWhiteSpace(edge.Id) ? "edge" : edge.Id;
                sb.AppendLine($"    - id: {edgeId}");
                sb.AppendLine($"      from: {edge.Source}");
                sb.AppendLine($"      to: {edge.Target}");
            }
        }

        return sb.ToString();
    }

    private void AssertSeriesEqual(IReadOnlyList<double> expected, double?[]? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual!.Length);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    private static double?[] RequireSeries(IDictionary<string, double?[]> source, string key)
    {
        Assert.True(source.TryGetValue(key, out var values), $"Missing series '{key}' in window response.");
        return values ?? Array.Empty<double?>();
    }
}
