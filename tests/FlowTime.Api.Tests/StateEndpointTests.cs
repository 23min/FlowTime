using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using FlowTime.API.Services;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Core.TimeTravel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Sdk;

namespace FlowTime.Api.Tests;

public class StateEndpointTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string runId = "run_state_fixture";
    private const string invalidRunId = "run_state_invalid";
    private const string telemetryWarningRunId = "run_state_warning";
    private const string simulationInvalidRunId = "run_state_sim_invalid";
    private const string telemetryMissingSourceRunId = "run_state_missing_source";
    private const string fullModeRunId = "run_state_full";
    private const string queueLatencyNullRunId = "run_state_queue_zero_served";
    private const string kernelPolicyRunId = "run_state_retry_kernel_policy";
    private const int binCount = 4;
    private const int binSizeMinutes = 5;
    private static readonly DateTime startTimeUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions goldenSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string artifactsRoot;
    private readonly HttpClient client;
    private readonly TestLogCollector logCollector;
    private readonly bool deleteArtifactsOnDispose;

    public StateEndpointTests(TestWebApplicationFactory factory)
    {
        var overrideRoot = Environment.GetEnvironmentVariable("FLOWTIME_TEST_ARTIFACT_ROOT");
        deleteArtifactsOnDispose = string.IsNullOrWhiteSpace(overrideRoot);
        artifactsRoot = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(Path.GetTempPath(), $"flowtime_state_fixture_{Guid.NewGuid():N}")
            : overrideRoot;
        Directory.CreateDirectory(artifactsRoot);
        CreateFixtureRun();
        CreateInvalidRun();
        CreateTelemetryWarningRun();
        CreateSimulationInvalidRun();
        CreateTelemetryMissingSourceRun();
        CreateFullModeRun();
        CreateQueueLatencyNullRun();
        CreateKernelPolicyRun();

        logCollector = new TestLogCollector();
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsRoot);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(_ => logCollector);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetState_ReturnsSnapshotWithDerivedMetrics()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state?binIndex=1");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"telemetry\"", responseBody);

        var payload = JsonSerializer.Deserialize<StateSnapshotResponse>(responseBody, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);
        var manifestMetadata = await new RunManifestReader().ReadAsync(Path.Combine(artifactsRoot, runId, "model"), CancellationToken.None);
        Assert.NotEmpty(manifestMetadata.TelemetrySources);

        Assert.Equal(runId, payload!.Metadata.RunId);
        Assert.Equal("telemetry", payload.Metadata.Mode);
        Assert.True(payload.Metadata.TelemetrySourcesResolved);
        Assert.Empty(payload.Warnings);

        Assert.Equal(1, payload.Bin.Index);
        Assert.Equal(startTimeUtc.AddMinutes(binSizeMinutes), payload.Bin.StartUtc?.UtcDateTime);

        var service = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Equal("service", service.Kind);
        Assert.Equal(10, service.Metrics.Arrivals);
        Assert.Equal(6, service.Metrics.Served);
        Assert.Equal(1, service.Metrics.Errors);
        Assert.Equal(7, service.Metrics.Attempts);
        Assert.Equal(1, service.Metrics.Failures);
        Assert.Equal(0.6, service.Metrics.RetryEcho!.Value, 5);
        Assert.Equal(0.85714, service.Derived.Utilization!.Value, 5);
        Assert.Equal(300, service.Derived.ServiceTimeMs!.Value, 5);
        Assert.Equal("yellow", service.Derived.Color);
        Assert.Contains(service.Telemetry.Sources, s => s.Contains("OrderService_arrivals") || s.Contains("OrderService_served"));
        Assert.Empty(service.Telemetry.Warnings);
        Assert.NotNull(service.Aliases);
        Assert.Equal("Ticket submissions", service.Aliases!["attempts"]);

        var queue = Assert.Single(payload.Nodes, n => n.Id == "SupportQueue");
        Assert.Equal("queue", queue.Kind);
        Assert.Equal(10, queue.Metrics.Queue);
        Assert.Equal(8.33333, queue.Derived.LatencyMinutes!.Value, 5);
        Assert.Equal("red", queue.Derived.Color);
        Assert.Null(queue.Derived.ServiceTimeMs);
        Assert.Contains(queue.Telemetry.Sources, s => s.Contains("SupportQueue_arrivals"));
        Assert.Empty(queue.Telemetry.Warnings);
        Assert.NotNull(queue.Aliases);
        Assert.Equal("Open backlog", queue.Aliases!["queue"]);
    }

    [Fact]
    public async Task GetStateWindow_ReturnsAlignedSeries()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        Assert.Equal(runId, payload!.Metadata.RunId);
        Assert.Equal(4, payload.Window.BinCount);
        Assert.Equal(4, payload.TimestampsUtc.Count);
        Assert.Equal(startTimeUtc, payload.TimestampsUtc[0].UtcDateTime);
        Assert.Equal(startTimeUtc.AddMinutes(3 * binSizeMinutes), payload.TimestampsUtc[^1].UtcDateTime);

        var serviceSeries = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Equal(new double?[] { 10.0, 10.0, 10.0, 10.0 }, serviceSeries.Series["arrivals"]);
        Assert.Equal(new double?[] { 9.0, 6.0, 9.0, 4.0 }, serviceSeries.Series["served"]);
        Assert.Equal(new double?[] { 12.0, 7.0, 9.0, 4.0 }, serviceSeries.Series["capacity"]);
        Assert.Equal(new double?[] { 10.0, 7.0, 10.0, 5.0 }, serviceSeries.Series["attempts"]);
        Assert.Equal(new double?[] { 1.0, 1.0, 1.0, 1.0 }, serviceSeries.Series["failures"]);
        Assert.True(serviceSeries.Series.ContainsKey("serviceTimeMs"));
        Assert.Equal(new double?[] { 250.0, 300.0, 300.0, 300.0 }, serviceSeries.Series["serviceTimeMs"]);
        var retryEcho = serviceSeries.Series["retryEcho"];
        Assert.Equal(4, retryEcho.Length);
        Assert.Equal(0.0, retryEcho[0]!.Value, 5);
        Assert.Equal(0.6, retryEcho[1]!.Value, 5);
        Assert.Equal(0.9, retryEcho[2]!.Value, 5);
        Assert.Equal(1.0, retryEcho[3]!.Value, 5);
        Assert.Empty(payload.Warnings);
        Assert.Empty(serviceSeries.Telemetry.Warnings);
        Assert.NotNull(serviceSeries.Aliases);
        Assert.Equal("Orders fulfilled", serviceSeries.Aliases!["served"]);

        var utilization = serviceSeries.Series.ContainsKey("utilization") ? serviceSeries.Series["utilization"] : Array.Empty<double?>();
        Assert.Equal(4, utilization.Length);
        Assert.Equal(0.75, utilization[0]!.Value, 5);
        Assert.Equal(1.0, utilization[2]!.Value, 5);

        var queueSeries = Assert.Single(payload.Nodes, n => n.Id == "SupportQueue");
        Assert.True(queueSeries.Series.ContainsKey("latencyMinutes"));
        var latency = queueSeries.Series["latencyMinutes"];
        Assert.Equal(4, latency.Length);
        Assert.Equal(1.11111, latency[0]!.Value, 5);
        Assert.Equal(8.33333, latency[1]!.Value, 5);
        Assert.Empty(queueSeries.Telemetry.Warnings);
        Assert.NotNull(queueSeries.Aliases);
        Assert.Equal("Open backlog", queueSeries.Aliases!["queue"]);
    }

    [Fact]
    public async Task GetStateWindow_PreservesAttemptConservation()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        const double tolerance = 1e-4;

        foreach (var node in payload!.Nodes)
        {
            if (!node.Series.TryGetValue("attempts", out var attempts))
            {
                continue;
            }

            if (!node.Series.TryGetValue("served", out var served))
            {
                continue;
            }

            if (!node.Series.TryGetValue("failures", out var failures) &&
                !node.Series.TryGetValue("errors", out failures))
            {
                continue;
            }

            var binCount = Math.Min(attempts.Length, Math.Min(served.Length, failures.Length));
            for (var i = 0; i < binCount; i++)
            {
                var attempt = attempts[i];
                var success = served[i];
                var failure = failures[i];

                if (!attempt.HasValue || !success.HasValue || !failure.HasValue)
                {
                    continue;
                }

                var expected = success.Value + failure.Value;
                var delta = Math.Abs(attempt.Value - expected);
                Assert.True(delta <= tolerance, $"Attempts were not conserved for node '{node.Id}' at bin {i}: attempts={attempt.Value}, served={success.Value}, failures={failure.Value}");
            }
        }
    }

    [Fact]
    public async Task GetStateWindow_NullsQueueLatencyWhenServedIsZero()
    {
        var response = await client.GetAsync($"/v1/runs/{queueLatencyNullRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        var queueSeries = Assert.Single(payload!.Nodes, n => n.Id == "SupportQueue");
        Assert.True(queueSeries.Series.TryGetValue("latencyMinutes", out var latency));

        Assert.Equal(4, latency.Length);
        Assert.Equal(1.11111, latency[0]!.Value, 5);
        Assert.Null(latency[1]);
        Assert.Equal(11.11111, latency[2]!.Value, 5);
        Assert.Equal(0, latency[3]!.Value, 5);

        // Flow latency should track queue latency + upstream service; null when served is zero.
        Assert.True(queueSeries.Series.TryGetValue("flowLatencyMs", out var flowLatency));
        Assert.Equal(4, flowLatency.Length);
        Assert.Equal(66666.666667, flowLatency[0]!.Value, 6);
        Assert.Null(flowLatency[1]);
        Assert.Equal(666666.666667, flowLatency[2]!.Value, 6);
        Assert.Equal(0, flowLatency[3]!.Value, 6);

        AssertGoldenResponse("state-window-queue-null-approved.json", payload);
    }

    [Fact]
    public async Task GetStateWindow_RetryKernelViolationsEmitWarnings()
    {
        var response = await client.GetAsync($"/v1/runs/{kernelPolicyRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        var orderNode = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");
        var kernelWarnings = orderNode.Telemetry.Warnings
            .Where(w => string.Equals(w.Code, "retry_kernel_policy", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, kernelWarnings.Length);
        Assert.Contains(kernelWarnings, w => w.Message.Contains("trimmed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(kernelWarnings, w => w.Message.Contains("scaled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStateWindow_FullMode_IncludesComputedNodes()
    {
        var response = await client.GetAsync($"/v1/runs/{fullModeRunId}/state_window?startBin=0&endBin=3&mode=full");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var computed = Assert.Single(payload!.Nodes, n => n.Id == "expr_output");
        Assert.Equal("expr", computed.Kind);
        Assert.True(computed.Series.TryGetValue("values", out var values));
        Assert.Equal(new double?[] { 2.0, 4.0, 6.0, 8.0 }, values);
        Assert.True(computed.Series.ContainsKey("series:expr_output"));

        var constantNode = Assert.Single(payload.Nodes, n => n.Id == "base_input");
        Assert.Equal("const", constantNode.Kind);
        Assert.True(constantNode.Series.TryGetValue("values", out var baseValues));
        Assert.Equal(new double?[] { 1.0, 2.0, 3.0, 4.0 }, baseValues);
    }

    [Fact]
    public async Task GetState_EmitsStructuredObservabilityLog()
    {
        logCollector.Clear();

        var response = await client.GetAsync($"/v1/runs/{runId}/state?binIndex=1");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var entry = GetObservabilityEntry(new EventId(3001, "StateSnapshotObservability"));
        var state = ExtractState(entry);

        Assert.Equal(runId, AssertEntryValue(state, "RunId"));
        Assert.Equal("telemetry", AssertEntryValue(state, "Mode"));
        Assert.Equal(1, Convert.ToInt32(AssertEntryValue(state, "BinIndex")));
        Assert.Equal(binCount, Convert.ToInt32(AssertEntryValue(state, "TotalBins")));
    }

    [Fact]
    public async Task GetStateWindow_EmitsStructuredObservabilityLog()
    {
        logCollector.Clear();

        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=2");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var entry = GetObservabilityEntry(new EventId(3002, "StateWindowObservability"));
        var state = ExtractState(entry);

        Assert.Equal(runId, AssertEntryValue(state, "RunId"));
        Assert.Equal("telemetry", AssertEntryValue(state, "Mode"));
        Assert.Equal(0, Convert.ToInt32(AssertEntryValue(state, "StartBin")));
        Assert.Equal(2, Convert.ToInt32(AssertEntryValue(state, "EndBin")));
        Assert.Equal(3, Convert.ToInt32(AssertEntryValue(state, "RequestedBins")));
        Assert.Equal(binCount, Convert.ToInt32(AssertEntryValue(state, "TotalBins")));
    }

    [Fact]
    public async Task GetState_TelemetryWithInvalidSeries_ReturnsWarnings()
    {
        var response = await client.GetAsync($"/v1/runs/{telemetryWarningRunId}/state?binIndex=0");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateSnapshotResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new XunitException("Failed to deserialize state response");

        Assert.Empty(payload.Warnings);

        var service = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Contains(service.Telemetry.Warnings, w => w.Code == "telemetry_series_invalid");
        Assert.Equal("warning", Assert.Single(service.Telemetry.Warnings).Severity);
        Assert.Throws<XunitException>(() => AssertGoldenResponse("state-warning.json", payload));
    }

    [Fact]
    public async Task GetState_SimulationWithInvalidSeries_ReturnsModeValidationError()
    {
        var response = await client.GetAsync($"/v1/runs/{simulationInvalidRunId}/state?binIndex=0");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mode_validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("invalid values", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetState_TelemetryMissingSource_ReturnsUnresolvedWarning()
    {
        var response = await client.GetAsync($"/v1/runs/{telemetryMissingSourceRunId}/state?binIndex=0");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateSnapshotResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new XunitException("Failed to deserialize missing-source payload");

        Assert.Contains(payload.Warnings, w => w.Code == "telemetry_sources_missing");

        var service = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Empty(service.Telemetry.Sources);
        Assert.Contains(service.Telemetry.Warnings, w => w.Code == "telemetry_sources_unresolved");
        Assert.Null(service.Derived.ServiceTimeMs);
    }

    [Fact]
    public async Task State_GoldenResponse_MatchesApprovedSnapshot()
    {
        var payload = await client.GetFromJsonAsync<StateSnapshotResponse>($"/v1/runs/{runId}/state?binIndex=1", new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new XunitException("Failed to deserialize state payload");

        AssertGoldenResponse("state-approved.json", payload);
    }

    [Fact]
    public async Task StateWindow_GoldenResponse_MatchesApprovedSnapshot()
    {
        var payload = await client.GetFromJsonAsync<StateWindowResponse>($"/v1/runs/{runId}/state_window?startBin=0&endBin=3", new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new XunitException("Failed to deserialize state window payload");

        AssertGoldenResponse("state-window-approved.json", payload);
    }

    [Fact]
    public async Task GetState_WithUnknownRun_ReturnsNotFound()
    {
        var response = await client.GetAsync("/v1/runs/unknown/state?binIndex=0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task GetState_WithSelfShiftMissingInitial_ReturnsConflict()
    {
        var response = await client.GetAsync($"/v1/runs/{invalidRunId}/state?binIndex=0");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = error.GetProperty("error").GetString();
        Assert.Contains("Model metadata for run", message);
        Assert.Contains("uses SHIFT on itself and requires an initial condition", message);
    }

    private void CreateFixtureRun()
    {
        CreateRun(runId, BuildValidModelYaml(), mode: "telemetry");
    }

    private void CreateInvalidRun()
    {
        CreateRun(invalidRunId, BuildInvalidModelYaml(), mode: "telemetry");
    }

    private void CreateTelemetryWarningRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_served.csv"] = new double[] { double.NaN, 6, 9, 4 }
        };

        CreateRun(telemetryWarningRunId, BuildValidModelYaml(), mode: "telemetry", overrides);
    }

    private void CreateSimulationInvalidRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_served.csv"] = new double[] { double.NaN, double.NaN, double.NaN, double.NaN }
        };

        CreateRun(simulationInvalidRunId, BuildValidModelYaml(), mode: "simulation", overrides);
    }

    private void CreateTelemetryMissingSourceRun()
    {
        CreateRun(telemetryMissingSourceRunId, BuildValidModelYamlWithMissingServeTelemetry(), mode: "telemetry");
    }

    private void CreateQueueLatencyNullRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["SupportQueue_served.csv"] = new double[] { 9, 0, 9, 4 },
            ["SupportQueue_queue.csv"] = new double[] { 2, 15, 20, 0 }
        };

        CreateRun(queueLatencyNullRunId, BuildValidModelYaml(), mode: "telemetry", overrides);
    }

    private void CreateKernelPolicyRun()
    {
        CreateRun(kernelPolicyRunId, BuildKernelPolicyModelYaml(), mode: "simulation");
    }

    private void CreateFullModeRun()
    {
        var seriesOutputs = new Dictionary<string, double[]>
        {
            ["base_input@BASE_INPUT@DEFAULT.csv"] = new double[] { 1, 2, 3, 4 },
            ["expr_output@EXPR_OUTPUT@DEFAULT.csv"] = new double[] { 2, 4, 6, 8 }
        };

        var manifestSeries = new (string id, string path, string unit)[]
        {
            ("base_input@BASE_INPUT@DEFAULT", "series/base_input@BASE_INPUT@DEFAULT.csv", "units"),
            ("expr_output@EXPR_OUTPUT@DEFAULT", "series/expr_output@EXPR_OUTPUT@DEFAULT.csv", "units")
        };

        CreateRun(
            fullModeRunId,
            BuildFullModeModelYaml(),
            mode: "telemetry",
            overrides: null,
            seriesOutputs: seriesOutputs,
            manifestSeries: manifestSeries);
    }

    private void CreateRun(
        string runIdentifier,
        string modelYaml,
        string mode,
        Dictionary<string, double[]>? overrides = null,
        Dictionary<string, double[]>? seriesOutputs = null,
        IReadOnlyCollection<(string id, string path, string unit)>? manifestSeries = null)
    {
        var runDir = Path.Combine(artifactsRoot, runIdentifier);
        var modelDir = Path.Combine(runDir, "model");
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(modelDir);
        Directory.CreateDirectory(seriesDir);

        WriteBaseSeries(modelDir);
        if (overrides != null)
        {
            foreach (var (fileName, values) in overrides)
            {
                WriteSeries(modelDir, fileName, values);
            }
        }
        if (seriesOutputs != null)
        {
            foreach (var (fileName, values) in seriesOutputs)
            {
                WriteSeries(seriesDir, fileName, values);
            }
        }
        File.WriteAllText(Path.Combine(modelDir, "model.yaml"), modelYaml, System.Text.Encoding.UTF8);
        WriteMetadata(modelDir, runIdentifier, mode);
        WriteSeriesIndex(seriesDir, manifestSeries);
        File.WriteAllText(Path.Combine(runDir, "run.json"), BuildRunJson(runIdentifier, mode, manifestSeries), System.Text.Encoding.UTF8);
    }

    private static void WriteBaseSeries(string modelDir)
    {
        WriteSeries(modelDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(modelDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_attempts.csv", new double[] { 10, 7, 10, 5 });
        WriteSeries(modelDir, "OrderService_failures.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_retryEcho.csv", new double[] { 0.0, 0.6, 0.9, 1.0 });
        WriteSeries(modelDir, "OrderService_capacity.csv", new double[] { 12, 7, 9, 4 });
        WriteSeries(modelDir, "OrderService_processingTimeMsSum.csv", new double[] { 2250, 1800, 2700, 1200 });
        WriteSeries(modelDir, "OrderService_servedCount.csv", new double[] { 9, 6, 9, 4 });

        WriteSeries(modelDir, "SupportQueue_arrivals.csv", new double[] { 9, 7, 9, 5 });
        WriteSeries(modelDir, "SupportQueue_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "SupportQueue_errors.csv", new double[] { 0, 0, 0, 0 });
        WriteSeries(modelDir, "SupportQueue_queue.csv", new double[] { 2, 10, 20, 0 });
    }

    private static string BuildValidModelYamlWithMissingServeTelemetry()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served_missing.csv"
        errors: "file:OrderService_errors.csv"
        attempts: "file:OrderService_attempts.csv"
        failures: "file:OrderService_failures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: null
        servedCount: null
        slaMin: null
        aliases:
          attempts: "Ticket submissions"
          served: "Orders fulfilled"
          retryEcho: "Retry backlog"
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queueDepth: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
        aliases:
          queue: "Open backlog"
  edges: []

""";
    }

    private static string BuildKernelPolicyModelYaml()
    {
        var kernelValues = string.Join(", ", Enumerable.Repeat("0.5", 40));

        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        attempts: "file:OrderService_attempts.csv"
        failures: "file:OrderService_failures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [{kernelValues}]
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        aliases:
          attempts: "Ticket submissions"
          served: "Orders fulfilled"
          retryEcho: "Retry backlog"
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queueDepth: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
        aliases:
          queue: "Open backlog"
  edges: []

""";
    }

private static void WriteSeries(string directory, string fileName, IReadOnlyList<double> values)
{
    var path = Path.Combine(directory, fileName);
    using var writer = new StreamWriter(path);
    writer.NewLine = "\n";
        writer.WriteLine("bin_index,value");
        for (var i = 0; i < values.Count; i++)
        {
        writer.WriteLine(FormattableString.Invariant($"{i},{values[i]}"));
    }
}

    private static void WriteSeriesIndex(string seriesDirectory, IReadOnlyCollection<(string id, string path, string unit)>? entries)
    {
        Directory.CreateDirectory(seriesDirectory);

        var payload = new
        {
            schemaVersion = 1,
            grid = new
            {
                bins = binCount,
                binSize = binSizeMinutes,
                binUnit = "minutes"
            },
            series = (entries ?? Array.Empty<(string id, string path, string unit)>()).Select(entry => new
            {
                id = entry.id,
                kind = "derived",
                path = entry.path,
                unit = entry.unit,
                componentId = ExtractComponentId(entry.id),
                @class = "DEFAULT",
                points = binCount,
                hash = $"sha256:{entry.id}"
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(seriesDirectory, "index.json"), json);
    }

    private static string ExtractComponentId(string id)
    {
        var firstAt = id.IndexOf('@');
        if (firstAt < 0 || firstAt == id.Length - 1)
        {
            return id;
        }

        var remainder = id[(firstAt + 1)..];
        var secondAt = remainder.IndexOf('@');
        return secondAt < 0 ? remainder : remainder[..secondAt];
    }

    private static void AssertGoldenResponse<T>(string fileName, T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var goldenDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Golden"));
        Directory.CreateDirectory(goldenDir);
        var path = Path.Combine(goldenDir, fileName);

        var json = SerializeSanitized(payload);

        if (!File.Exists(path))
        {
            throw new XunitException($"Golden snapshot '{fileName}' does not exist. New content:\n{json}");
        }

        var expected = File.ReadAllText(path);
        var normalizedExpected = Normalize(expected);
        var normalizedActual = Normalize(json);
        if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            File.WriteAllText(path + ".actual", json);
            throw new XunitException($"Golden snapshot '{fileName}' mismatch.{Environment.NewLine}Expected:{Environment.NewLine}{expected}{Environment.NewLine}Actual:{Environment.NewLine}{json}");
        }
    }

    private static string SerializeSanitized<T>(T payload)
    {
        var node = JsonSerializer.SerializeToNode(payload, goldenSerializerOptions)
            ?? throw new XunitException("Failed to serialize payload for golden comparison.");

        if (node is JsonObject root && root["metadata"] is JsonObject metadata && metadata["storage"] is JsonObject storage)
        {
            storage["modelPath"] = "<dynamic>";
            storage["metadataPath"] = "<dynamic>";
            storage["provenancePath"] = "<dynamic>";
        }

        return node.ToJsonString(goldenSerializerOptions);
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n").Trim();

    private static void WriteMetadata(string modelDirectory, string runIdentifier, string mode)
    {
        var metadata = new
        {
            templateId = "order-system",
            templateTitle = "Order System Fixture",
            templateVersion = "1.0.0",
            schemaVersion = 1,
            mode,
            modelHash = $"sha256:{runIdentifier}"
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "metadata.json"), metadataJson, System.Text.Encoding.UTF8);

        var provenance = new
        {
            source = "flowtime-sim",
            templateId = "order-system",
            templateVersion = "1.0.0",
            mode = "telemetry",
            modelId = runIdentifier,
            schemaVersion = 1
        };

        var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(Path.Combine(modelDirectory, "provenance.json"), provenanceJson, System.Text.Encoding.UTF8);
    }

    private static string BuildRunJson(string runIdentifier, string mode, IReadOnlyCollection<(string id, string path, string unit)>? seriesEntries)
    {
        var grid = new
        {
            bins = binCount,
            binSize = binSizeMinutes,
            binUnit = "minutes",
            binMinutes = binSizeMinutes,
            timezone = "UTC",
            align = "left"
        };

        var manifest = new
        {
            schemaVersion = 1,
            runId = runIdentifier,
            engineVersion = "0.0-test",
            source = mode,
            grid,
            modelHash = $"sha256:{runIdentifier}",
            scenarioHash = "sha256:test",
            createdUtc = startTimeUtc.ToString("o", CultureInfo.InvariantCulture),
            warnings = Array.Empty<string>(),
            series = (seriesEntries ?? Array.Empty<(string id, string path, string unit)>())
                .Select(entry => new
                {
                    id = entry.id,
                    path = entry.path,
                    unit = entry.unit
                })
                .ToArray()
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static string BuildValidModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        attempts: "file:OrderService_attempts.csv"
        failures: "file:OrderService_failures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        aliases:
          attempts: "Ticket submissions"
          served: "Orders fulfilled"
          retryEcho: "Retry backlog"
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queueDepth: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
        aliases:
          queue: "Open backlog"
  edges: []

""";
    }

    private static string BuildFullModeModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        attempts: "file:OrderService_attempts.csv"
        failures: "file:OrderService_failures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queueDepth: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
  edges: []

nodes:
  - id: "base_input"
    kind: "const"
    values: [1, 2, 3, 4]
    source: ""
  - id: "expr_output"
    kind: "expr"
    expr: "base_input * 2"

outputs:
  - series: base_input
    as: base_input.csv
  - series: expr_output
    as: expr_output.csv

""";
    }

    private static string BuildInvalidModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

nodes:
  - id: "SupportQueue"
    kind: "expr"
    expr: "SHIFT(SupportQueue, 1)"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
        errors: "file:OrderService_errors.csv"
        attempts: "file:OrderService_attempts.csv"
        failures: "file:OrderService_failures.csv"
        retryEcho: "OrderService_retryEcho"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: null
        servedCount: null
        slaMin: null
        aliases:
          attempts: "Ticket submissions"
          served: "Orders fulfilled"
          retryEcho: "Retry backlog"
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        externalDemand: null
        queueDepth: "file:SupportQueue_queue.csv"
        capacity: null
        slaMin: 5
        aliases:
          queue: "Open backlog"
  edges: []

""";
    }

    public void Dispose()
    {
        client.Dispose();
        logCollector.Dispose();
        if (deleteArtifactsOnDispose && Directory.Exists(artifactsRoot))
        {
            Directory.Delete(artifactsRoot, recursive: true);
        }
    }

    private TestLogEntry GetObservabilityEntry(EventId expectedEvent)
    {
        var entry = logCollector.Entries.LastOrDefault(e =>
            e.Category == typeof(StateQueryService).FullName &&
            e.EventId.Id == expectedEvent.Id &&
            string.Equals(e.EventId.Name, expectedEvent.Name, StringComparison.Ordinal));

        if (entry == null)
        {
            var available = string.Join(Environment.NewLine, logCollector.Entries.Select(e =>
                $"{e.Category} | {e.EventId.Id}:{e.EventId.Name} | {e.Message}"));
            throw new XunitException($"Expected to find log {expectedEvent.Id}:{expectedEvent.Name} but none were captured. Logs:{Environment.NewLine}{available}");
        }

        return entry;
    }

    private static Dictionary<string, object?> ExtractState(TestLogEntry entry)
    {
        return entry.State
            .Where(kvp => !string.Equals(kvp.Key, "{OriginalFormat}", StringComparison.Ordinal))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static object AssertEntryValue(Dictionary<string, object?> state, string key)
    {
        if (!state.TryGetValue(key, out var value))
        {
            throw new XunitException($"Expected structured log state to contain '{key}'. Keys: {string.Join(", ", state.Keys)}");
        }

        return value ?? throw new XunitException($"Structured log value '{key}' was null.");
    }
}
