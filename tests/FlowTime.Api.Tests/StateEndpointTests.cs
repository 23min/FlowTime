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
    private const string retryEdgesRunId = "run_state_edges";
    private const string edgeFlowRunId = "run_state_edge_flow";
    private const string edgeFlowApproxRunId = "run_state_edge_flow_approx";
    private const string edgeFlowPartialRunId = "run_state_edge_flow_partial";
    private const string edgeFlowRouterSubsetRunId = "run_state_edge_flow_router_subset";
    private const string classRunId = "run_state_classes";
    private const string queueClassRunId = "run_state_classes_queue";
    private const string serviceWithBufferDerivedRunId = "run_state_servicewithbuffer_derived";
    private const string serviceWithBufferParallelismRunId = "run_state_servicewithbuffer_parallelism";
    private const string serviceWithBufferBehaviorBaselineRunId = "run_state_servicewithbuffer_behavior_base";
    private const string serviceWithBufferBehaviorParallelRunId = "run_state_servicewithbuffer_behavior_parallel";
    private const string serviceWithBufferPartialRunId = "run_state_servicewithbuffer_partial";
    private const string serviceTimeZeroRunId = "run_state_service_time_zero";
    private const string dispatchScheduleRunId = "run_state_dispatch_schedule";
    private const string throughputOverflowRunId = "run_state_throughput_overflow";
    private const string backlogWarningsRunId = "run_state_backlog_warnings";
    private const string backlogWarningsParallelismRunId = "run_state_backlog_warnings_parallelism";
    private const string sinkRunId = "run_state_sink";
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
        CreateRetryEdgesRun();
        CreateEdgeFlowRun();
        CreateEdgeFlowApproxRun();
        CreateEdgeFlowPartialRun();
        CreateEdgeFlowRouterSubsetRun();
        CreateClassRun();
        CreateServiceWithBufferClassRun();
        CreateServiceWithBufferDerivedRun();
        CreateServiceWithBufferParallelismRun();
        CreateServiceWithBufferBehaviorRuns();
        CreateServiceWithBufferPartialRun();
        CreateServiceTimeZeroRun();
        CreateDispatchScheduleRun();
        CreateThroughputOverflowRun();
        CreateBacklogWarningsRun();
        CreateBacklogWarningsParallelismRun();
        CreateSinkRun();

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
        Assert.Equal("missing", payload.Metadata.ClassCoverage);
        Assert.Equal("missing", payload.Metadata.EdgeQuality);
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
        Assert.Equal(0, service.Metrics.ExhaustedFailures);
        Assert.Equal(0.6, service.Metrics.RetryEcho!.Value, 5);
        Assert.Equal(3, service.Metrics.RetryBudgetRemaining);
        Assert.Equal(4, service.Metrics.MaxAttempts);
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
    public async Task GetStateWindow_IncludesEdgeQuality()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowRunId}/state_window?startBin=0&endBin=3");
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
        Assert.Equal("exact", payload!.Metadata.EdgeQuality);
    }

    [Fact]
    public async Task GetStateWindow_IncludesApproxEdgeQuality()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowApproxRunId}/state_window?startBin=0&endBin=3");
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
        Assert.Equal("approx", payload!.Metadata.EdgeQuality);
    }

    [Fact]
    public async Task GetStateWindow_IncludesPartialClassEdgeQuality()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowPartialRunId}/state_window?startBin=0&endBin=3");
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
        Assert.Equal("partialClass", payload!.Metadata.EdgeQuality);
    }

    [Fact]
    public async Task GetStateWindow_IncludesExactEdgeQuality_ForRouterClassSubset()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowRouterSubsetRunId}/state_window?startBin=0&endBin=3");
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
        Assert.Equal("exact", payload!.Metadata.EdgeQuality);
    }

    [Fact]
    public async Task GetState_ReturnsByClassBreakdown()
    {
        var response = await client.GetAsync($"/v1/runs/{classRunId}/state?binIndex=0");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<StateSnapshotResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        Assert.Equal("full", payload!.Metadata.ClassCoverage);

        var orderService = Assert.Single(payload.Nodes, n => n.Id == "OrderService");
        Assert.Equal("full", payload.Metadata.ClassCoverage);
        Assert.NotNull(orderService.ByClass);
        Assert.Equal(2, orderService.ByClass!.Count);

        var vip = orderService.ByClass["vip"];
        Assert.Equal(6d, vip.Arrivals);
        Assert.Equal(5d, vip.Served);
        Assert.Equal(1d, vip.Errors);

        var standard = orderService.ByClass["standard"];
        Assert.Equal(4d, standard.Arrivals);
        Assert.Equal(3d, standard.Served);
        Assert.Equal(0d, standard.Errors);

        Assert.Equal(10d, orderService.Metrics.Arrivals);
        Assert.Equal(8d, orderService.Metrics.Served);
        Assert.Equal(1d, orderService.Metrics.Errors);
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
        Assert.Equal("missing", payload.Metadata.ClassCoverage);
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
        Assert.Equal(new double?[] { 0.0, 0.0, 1.0, 1.0 }, serviceSeries.Series["exhaustedFailures"]);
        Assert.Equal(new double?[] { 3.0, 3.0, 2.0, 1.0 }, serviceSeries.Series["retryBudgetRemaining"]);
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
    public async Task GetStateWindow_SinkNode_EmitsCompletionSeries()
    {
        var payload = await client.GetFromJsonAsync<StateWindowResponse>($"/v1/runs/{sinkRunId}/state_window?startBin=0&endBin=3", new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new XunitException("Failed to deserialize sink state window payload");

        Assert.Equal(sinkRunId, payload.Metadata.RunId);

        var sinkSeries = Assert.Single(payload.Nodes, n => n.Id == "TerminalSuccess");
        Assert.DoesNotContain("queue", sinkSeries.Series.Keys);
        Assert.DoesNotContain("capacity", sinkSeries.Series.Keys);
        Assert.DoesNotContain("attempts", sinkSeries.Series.Keys);
        Assert.DoesNotContain("retryEcho", sinkSeries.Series.Keys);

        var slaSeries = sinkSeries.Sla;
        Assert.NotNull(slaSeries);
        var completion = Assert.Single(slaSeries!, s => string.Equals(s.Kind, "completion", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, completion.Values.Length);
        Assert.All(completion.Values, value => Assert.Equal(1d, value));

        var schedule = Assert.Single(slaSeries!, s => string.Equals(s.Kind, "scheduleAdherence", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new double?[] { 1d, null, 1d, null }, schedule.Values);
    }

    [Fact]
    public async Task ThroughputRatio_IsClampedToOneInSnapshot()
    {
        var response = await client.GetAsync($"/v1/runs/{throughputOverflowRunId}/state?binIndex=0");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateSnapshotResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var service = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");
        Assert.Equal(1d, service.Derived.ThroughputRatio);
        Assert.True(service.Metrics.Served > service.Metrics.Arrivals);
    }

    [Fact]
    public async Task ThroughputRatioSeries_IsClampedToOne()
    {
        var response = await client.GetAsync($"/v1/runs/{throughputOverflowRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var service = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");
        Assert.True(service.Series.TryGetValue("throughputRatio", out var ratios));
        Assert.NotNull(ratios);
        Assert.All(ratios!, value =>
        {
            if (!value.HasValue)
            {
                return;
            }

            Assert.InRange(value.Value, 0d, 1d);
        });
        Assert.Contains(ratios!, value => value.HasValue && Math.Abs(value.Value - 1d) < 1e-6);
    }

    [Fact]
    public async Task StateWindow_IncludesDispatchScheduleMetadata()
    {
        var response = await client.GetAsync($"/v1/runs/{dispatchScheduleRunId}/state_window?startBin=0&endBin=1");
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

        var queueNode = Assert.Single(payload!.Nodes, n => n.Id == "SupportQueue");
        Assert.NotNull(queueNode.DispatchSchedule);
        Assert.Equal("time-based", queueNode.DispatchSchedule!.Kind);
        Assert.Equal(4, queueNode.DispatchSchedule.PeriodBins);
        Assert.Equal(1, queueNode.DispatchSchedule.PhaseOffset);
        Assert.Equal("QueueCapacity", queueNode.DispatchSchedule.CapacitySeries);
    }

    [Fact]
    public async Task GetState_ReportsQueueLatencyStatusWhenGateClosed()
    {
        var response = await client.GetAsync($"/v1/runs/{dispatchScheduleRunId}/state?binIndex=0");
        response.EnsureSuccessStatusCode();

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);
        var queueNode = payload!["nodes"]!.AsArray().First(n => string.Equals(n?["id"]?.GetValue<string>(), "SupportQueue", StringComparison.Ordinal));

        Assert.Null(queueNode?["derived"]?["latencyMinutes"]);

        var statusNode = queueNode?["metrics"]?["queueLatencyStatus"];
        Assert.NotNull(statusNode);
        Assert.Equal("paused_gate_closed", statusNode!["code"]!.GetValue<string>());
        Assert.Contains("gate", statusNode["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StateWindow_IncludesQueueLatencyStatusSeries()
    {
        var response = await client.GetAsync($"/v1/runs/{dispatchScheduleRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);

        var queueNode = payload!["nodes"]!.AsArray().First(n => string.Equals(n?["id"]?.GetValue<string>(), "SupportQueue", StringComparison.Ordinal));
        var statuses = queueNode?["queueLatencyStatus"]?.AsArray();
        Assert.NotNull(statuses);
        Assert.Equal(4, statuses!.Count);

        Assert.Equal("paused_gate_closed", statuses![0]!["code"]!.GetValue<string>());
        Assert.Null(statuses[1]);
        Assert.Equal("paused_gate_closed", statuses[2]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetStateWindow_ReturnsByClassSeries()
    {
        var response = await client.GetAsync($"/v1/runs/{classRunId}/state_window?startBin=0&endBin=1");
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

        var orderService = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");
        Assert.NotNull(orderService.ByClass);

        var vip = orderService.ByClass!["vip"];
        Assert.Equal(new double?[] { 6d, 5d }, vip["arrivals"]);
        Assert.Equal(new double?[] { 5d, 4d }, vip["served"]);
        Assert.Equal(new double?[] { 1d, 0d }, vip["errors"]);

        var standard = orderService.ByClass["standard"];
        Assert.Equal(new double?[] { 4d, 5d }, standard["arrivals"]);
        Assert.Equal(new double?[] { 3d, 4d }, standard["served"]);
        Assert.Equal(new double?[] { 0d, 1d }, standard["errors"]);
    }

    [Fact]
    public async Task GetStateWindow_ReturnsByClassSeries_ForQueueNode()
    {
        var response = await client.GetAsync($"/v1/runs/{queueClassRunId}/state_window?startBin=0&endBin=1");
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

        var queueNode = Assert.Single(payload!.Nodes, n => n.Id == "SupportQueue");
        Assert.NotNull(queueNode.ByClass);

        var vip = queueNode.ByClass!["vip"];
        Assert.Equal(new double?[] { 5d, 4d }, vip["arrivals"]);
        Assert.Equal(new double?[] { 1d, 4d }, vip["queue"]);

        var standard = queueNode.ByClass["standard"];
        Assert.Equal(new double?[] { 4d, 3d }, standard["arrivals"]);
        Assert.Equal(new double?[] { 1d, 6d }, standard["queue"]);
    }

    [Fact]
    public async Task GetStateWindow_DerivesMetrics_ForServiceWithBuffer()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceWithBufferDerivedRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "BufferService");

        Assert.True(node.Series.TryGetValue("latencyMinutes", out var latencySeries));
        Assert.NotNull(latencySeries);
        Assert.Equal(4, latencySeries!.Length);
        Assert.Equal(25d, latencySeries[0]!.Value, 5);
        Assert.Equal(6.25d, latencySeries[1]!.Value, 5);
        Assert.Equal(0d, latencySeries[2]!.Value, 5);
        Assert.Equal(10d, latencySeries[3]!.Value, 5);

        Assert.True(node.Series.TryGetValue("utilization", out var utilizationSeries));
        Assert.NotNull(utilizationSeries);
        Assert.Equal(4, utilizationSeries!.Length);
        Assert.Equal(0.5d, utilizationSeries[0]!.Value, 5);
        Assert.Equal(1d, utilizationSeries[1]!.Value, 5);
        Assert.Equal(0.25d, utilizationSeries[2]!.Value, 5);
        Assert.Equal(0.5d, utilizationSeries[3]!.Value, 5);

        Assert.True(node.Series.TryGetValue("serviceTimeMs", out var serviceTimeSeries));
        Assert.NotNull(serviceTimeSeries);
        Assert.Equal(4, serviceTimeSeries!.Length);
        Assert.Equal(200d, serviceTimeSeries[0]!.Value, 5);
        Assert.Equal(200d, serviceTimeSeries[1]!.Value, 5);
        Assert.Equal(100d, serviceTimeSeries[2]!.Value, 5);
        Assert.Equal(300d, serviceTimeSeries[3]!.Value, 5);

        Assert.True(node.Series.TryGetValue("flowLatencyMs", out var flowLatencySeries));
        Assert.NotNull(flowLatencySeries);
        Assert.Equal(4, flowLatencySeries!.Length);
        Assert.Equal(1_500_200d, flowLatencySeries[0]!.Value, 5);
        Assert.Equal(375_200d, flowLatencySeries[1]!.Value, 5);
        Assert.Equal(100d, flowLatencySeries[2]!.Value, 5);
        Assert.Equal(600_300d, flowLatencySeries[3]!.Value, 5);
    }

    [Fact]
    public async Task GetStateWindow_ServiceTimeIsNull_WhenServedCountIsZero()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceTimeZeroRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "OrderService");

        Assert.True(node.Series.TryGetValue("serviceTimeMs", out var serviceTimeSeries));
        Assert.NotNull(serviceTimeSeries);
        Assert.Equal(4, serviceTimeSeries!.Length);
        Assert.Null(serviceTimeSeries[0]);
        Assert.Equal(200d, serviceTimeSeries[1]!.Value, 5);
        Assert.Null(serviceTimeSeries[2]);
        Assert.Equal(200d, serviceTimeSeries[3]!.Value, 5);

        Assert.True(node.Series.TryGetValue("flowLatencyMs", out var flowLatencySeries));
        Assert.NotNull(flowLatencySeries);
        Assert.Equal(4, flowLatencySeries!.Length);
        Assert.Null(flowLatencySeries[0]);
        Assert.Equal(200d, flowLatencySeries[1]!.Value, 5);
        Assert.Null(flowLatencySeries[2]);
        Assert.Equal(200d, flowLatencySeries[3]!.Value, 5);
    }

    [Fact]
    public async Task GetStateWindow_EmitsSeriesMetadata_ForDerivedSeries()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceWithBufferDerivedRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "BufferService");
        Assert.NotNull(node.SeriesMetadata);

        Assert.True(node.SeriesMetadata!.TryGetValue("latencyMinutes", out var latencyMetadata));
        Assert.Equal("avg", latencyMetadata!.Aggregation);
        Assert.Equal("derived", latencyMetadata.Origin);

        Assert.True(node.SeriesMetadata.TryGetValue("serviceTimeMs", out var serviceMetadata));
        Assert.Equal("avg", serviceMetadata!.Aggregation);
        Assert.Equal("derived", serviceMetadata.Origin);

        Assert.True(node.SeriesMetadata.TryGetValue("queue", out var queueMetadata));
        Assert.Equal("explicit", queueMetadata!.Origin);

        if (node.SeriesMetadata.TryGetValue("flowLatencyMs", out var flowMetadata))
        {
            Assert.Equal("avg", flowMetadata!.Aggregation);
            Assert.Equal("derived", flowMetadata.Origin);
        }
    }

    [Fact]
    public async Task GetStateWindow_UsesEffectiveCapacity_ForServiceWithBufferParallelism()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceWithBufferParallelismRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "BufferService");

        Assert.True(node.Series.TryGetValue("utilization", out var utilizationSeries));
        Assert.NotNull(utilizationSeries);
        Assert.Equal(4, utilizationSeries!.Length);
        Assert.Equal(0.25d, utilizationSeries[0]!.Value, 5);
        Assert.Equal(0.5d, utilizationSeries[1]!.Value, 5);
        Assert.Equal(0.125d, utilizationSeries[2]!.Value, 5);
        Assert.Equal(0.25d, utilizationSeries[3]!.Value, 5);
    }

    [Fact]
    public async Task GetStateWindow_ParallelismHalvesUtilization_VersusBaseline()
    {
        var baselineResponse = await client.GetAsync($"/v1/runs/{serviceWithBufferDerivedRunId}/state_window?startBin=0&endBin=3");
        baselineResponse.EnsureSuccessStatusCode();
        var baseline = await baselineResponse.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(baseline);

        var parallelResponse = await client.GetAsync($"/v1/runs/{serviceWithBufferParallelismRunId}/state_window?startBin=0&endBin=3");
        parallelResponse.EnsureSuccessStatusCode();
        var parallel = await parallelResponse.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(parallel);

        var baselineNode = Assert.Single(baseline!.Nodes, n => n.Id == "BufferService");
        var parallelNode = Assert.Single(parallel!.Nodes, n => n.Id == "BufferService");

        Assert.True(baselineNode.Series.TryGetValue("utilization", out var baselineUtilization));
        Assert.True(parallelNode.Series.TryGetValue("utilization", out var parallelUtilization));
        Assert.NotNull(baselineUtilization);
        Assert.NotNull(parallelUtilization);
        Assert.Equal(baselineUtilization!.Length, parallelUtilization!.Length);

        for (var i = 0; i < baselineUtilization.Length; i += 1)
        {
            var baseValue = baselineUtilization[i];
            var parallelValue = parallelUtilization[i];
            if (!baseValue.HasValue || !parallelValue.HasValue)
            {
                continue;
            }

            Assert.Equal(baseValue.Value / 2d, parallelValue.Value, 5);
        }
    }

    [Fact]
    public async Task GetStateWindow_ParallelismReducesQueueDepth_ForServiceWithBuffer()
    {
        var baselineResponse = await client.GetAsync($"/v1/runs/{serviceWithBufferBehaviorBaselineRunId}/state_window?startBin=0&endBin=3");
        baselineResponse.EnsureSuccessStatusCode();
        var baseline = await baselineResponse.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(baseline);

        var parallelResponse = await client.GetAsync($"/v1/runs/{serviceWithBufferBehaviorParallelRunId}/state_window?startBin=0&endBin=3");
        parallelResponse.EnsureSuccessStatusCode();
        var parallel = await parallelResponse.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(parallel);

        var baselineNode = Assert.Single(baseline!.Nodes, n => n.Id == "BufferService");
        var parallelNode = Assert.Single(parallel!.Nodes, n => n.Id == "BufferService");

        Assert.True(baselineNode.Series.TryGetValue("queue", out var baselineQueue));
        Assert.True(parallelNode.Series.TryGetValue("queue", out var parallelQueue));
        Assert.NotNull(baselineQueue);
        Assert.NotNull(parallelQueue);
        Assert.Equal(baselineQueue!.Length, parallelQueue!.Length);

        var sawStrictReduction = false;
        for (var i = 0; i < baselineQueue.Length; i += 1)
        {
            var baseValue = baselineQueue[i];
            var parallelValue = parallelQueue[i];
            if (!baseValue.HasValue || !parallelValue.HasValue)
            {
                continue;
            }

            Assert.True(parallelValue.Value <= baseValue.Value);
            if (parallelValue.Value < baseValue.Value)
            {
                sawStrictReduction = true;
            }
        }

        Assert.True(sawStrictReduction);
    }

    [Fact]
    public async Task GetStateWindow_SkipsServiceMetrics_ForServiceWithBuffer_WhenInputsMissing()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceWithBufferPartialRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "BufferService");

        Assert.True(node.Series.ContainsKey("latencyMinutes"));
        Assert.False(node.Series.ContainsKey("utilization"));
        Assert.False(node.Series.ContainsKey("serviceTimeMs"));
    }

    [Fact]
    public async Task GetStateWindow_SlaSeries_CarriesForward_ForDispatchSchedule()
    {
        var response = await client.GetAsync($"/v1/runs/{dispatchScheduleRunId}/state_window?startBin=0&endBin=3");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "SupportQueue");
        Assert.NotNull(node.Sla);

        var completion = Assert.Single(node.Sla!, s => s.Kind == "completion");
        Assert.Equal("ok", completion.Status);
        Assert.Equal(4, completion.Values.Length);
        Assert.Null(completion.Values[0]);
        Assert.Equal(5d / 7d, completion.Values[1]!.Value, 5);
        Assert.Equal(5d / 7d, completion.Values[2]!.Value, 5);
        Assert.Equal(5d / 7d, completion.Values[3]!.Value, 5);

        var backlog = Assert.Single(node.Sla!, s => s.Kind == "backlogAge");
        Assert.Equal("unavailable", backlog.Status);
        Assert.All(backlog.Values, value => Assert.Null(value));
    }

    [Fact]
    public async Task GetStateWindow_SlaPayload_IncludesKindAndStatus_WhenInputsMissing()
    {
        var response = await client.GetAsync($"/v1/runs/{serviceWithBufferPartialRunId}/state_window?startBin=0&endBin=0");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StateWindowResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(payload);

        var node = Assert.Single(payload!.Nodes, n => n.Id == "BufferService");
        Assert.NotNull(node.Sla);

        var completion = Assert.Single(node.Sla!, s => s.Kind == "completion");
        Assert.False(string.IsNullOrWhiteSpace(completion.Status));

        var backlog = Assert.Single(node.Sla!, s => s.Kind == "backlogAge");
        Assert.Equal("unavailable", backlog.Status);
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
    public async Task GetStateWindow_EmitsBacklogWarnings_ForSustainedRisk()
    {
        var response = await client.GetAsync($"/v1/runs/{backlogWarningsRunId}/state_window?startBin=0&endBin=3");
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
        var warnings = payload!.Warnings.Where(w => w.NodeId == "BufferService").ToArray();

        Assert.Contains(warnings, w =>
            w.Code == "backlog_growth_streak" &&
            w.StartBin == 0 &&
            w.EndBin == 3 &&
            w.Signal == "queueDepth");

        Assert.Contains(warnings, w =>
            w.Code == "backlog_overload_ratio" &&
            w.StartBin == 0 &&
            w.EndBin == 3 &&
            w.Signal == "overloadRatio");

        Assert.Contains(warnings, w =>
            w.Code == "backlog_age_risk" &&
            w.StartBin == 0 &&
            w.EndBin == 3 &&
            w.Signal == "latencyMinutes");
    }

    [Fact]
    public async Task GetStateWindow_SuppressesOverloadWarnings_WhenParallelismBoostsCapacity()
    {
        var response = await client.GetAsync($"/v1/runs/{backlogWarningsParallelismRunId}/state_window?startBin=0&endBin=3");
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
        var warnings = payload!.Warnings.Where(w => w.NodeId == "BufferService").ToArray();

        Assert.DoesNotContain(warnings, w => w.Code == "backlog_overload_ratio");
    }

    [Fact]
    public async Task GetStateWindow_BacklogWarnings_IncludeSignalFields()
    {
        var response = await client.GetAsync($"/v1/runs/{backlogWarningsRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(json);
        var warnings = json!["warnings"] as JsonArray;
        Assert.NotNull(warnings);
        Assert.NotEmpty(warnings);

        var warning = warnings!.FirstOrDefault(entry =>
            string.Equals(entry?["code"]?.ToString(), "backlog_growth_streak", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(warning);
        Assert.Equal(0, warning!["startBin"]?.GetValue<int>());
        Assert.Equal(3, warning["endBin"]?.GetValue<int>());
        Assert.Equal("queueDepth", warning["signal"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetStateWindow_IncludesRetryEdgesByDefault()
    {
        var response = await client.GetAsync($"/v1/runs/{retryEdgesRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonNode.Parse(json);

        Assert.NotNull(payload);
        var edges = payload!["edges"] as JsonArray;
        Assert.NotNull(edges);
        Assert.NotEmpty(edges);

        AssertGoldenResponse("state-window-edges-approved.json", payload!);
    }

    [Fact]
    public async Task GetStateWindow_IncludesEdgeFlowSeriesFromArtifacts()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);

        var edges = payload!["edges"] as JsonArray;
        Assert.NotNull(edges);
        Assert.NotEmpty(edges);

        var edge = edges!.FirstOrDefault(entry => string.Equals(entry?["id"]?.ToString(), "order_to_support", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(edge);

        var series = edge!["series"]?["flowVolume"] as JsonArray;
        Assert.NotNull(series);
        Assert.Equal(new[] { 9d, 6d, 9d, 4d }, series!.Select(v => v!.GetValue<double>()).ToArray());
    }

    [Fact]
    public async Task GetStateWindow_IncludesEdgeWarningsById()
    {
        var response = await client.GetAsync($"/v1/runs/{edgeFlowRunId}/state_window?startBin=0&endBin=3");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);

        var edgeWarnings = payload!["edgeWarnings"] as JsonObject;
        Assert.NotNull(edgeWarnings);
    }

    [Fact]
    public async Task GetStateWindow_WithoutEdges_ReturnsEmptyEdgeWarningsAndEdges()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/state_window?startBin=0&endBin=1");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);

        var edges = payload!["edges"] as JsonArray;
        Assert.NotNull(edges);
        Assert.Empty(edges!);

        var edgeWarnings = payload["edgeWarnings"] as JsonObject;
        Assert.NotNull(edgeWarnings);
        Assert.Empty(edgeWarnings!);
    }

    [Fact]
    public async Task GetState_IncludesRetryEdgesByDefault()
    {
        var response = await client.GetAsync($"/v1/runs/{retryEdgesRunId}/state?binIndex=1");
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new XunitException($"Expected 200 OK but got {(int)response.StatusCode}: {errorBody}");
        }

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(payload);

        var edges = payload!["edges"] as JsonArray;
        Assert.NotNull(edges);
        Assert.NotEmpty(edges);
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

    private void CreateRetryEdgesRun()
    {
        CreateRun(retryEdgesRunId, BuildRetryEdgesModelYaml(), mode: "telemetry");
    }

    private void CreateEdgeFlowRun()
    {
        var edgeSeries = new Dictionary<string, double[]>
        {
            ["edge_order_to_support_flowVolume@EDGE_ORDER_TO_SUPPORT_FLOWVOLUME@DEFAULT.csv"] = new[] { 9d, 6d, 9d, 4d }
        };

        var manifestSeries = edgeSeries.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            edgeFlowRunId,
            BuildEdgeFlowModelYaml(),
            mode: "telemetry",
            seriesOutputs: edgeSeries,
            manifestSeries: manifestSeries);
    }

    private void CreateEdgeFlowApproxRun()
    {
        var edgeSeries = new Dictionary<string, double[]>
        {
            ["edge_order_to_support_flowVolume@EDGE_ORDER_TO_SUPPORT_FLOWVOLUME@DEFAULT.csv"] = new[] { 9d, 6d, 9d, 4d }
        };

        var manifestSeries = edgeSeries.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            edgeFlowApproxRunId,
            BuildEdgeFlowModelYaml(),
            mode: "telemetry",
            seriesOutputs: edgeSeries,
            manifestSeries: manifestSeries,
            classIds: new[] { "vip", "standard" });
    }

    private void CreateEdgeFlowPartialRun()
    {
        var edgeSeries = new Dictionary<string, double[]>
        {
            ["edge_order_to_support_flowVolume@EDGE_ORDER_TO_SUPPORT_FLOWVOLUME@DEFAULT.csv"] = new[] { 9d, 6d, 9d, 4d }
        };

        var classSeries = new Dictionary<string, double[]>
        {
            ["OrderService_arrivals@ORDERSERVICE@vip.csv"] = new[] { 7d, 6d, 6d, 5d },
            ["OrderService_arrivals@ORDERSERVICE@standard.csv"] = new[] { 1d, 1d, 1d, 1d },
            ["OrderService_served@ORDERSERVICE@vip.csv"] = new[] { 6d, 5d, 5d, 4d },
            ["OrderService_served@ORDERSERVICE@standard.csv"] = new[] { 1d, 1d, 1d, 1d }
        };

        var seriesOutputs = edgeSeries
            .Concat(classSeries)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        var manifestSeries = seriesOutputs.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            edgeFlowPartialRunId,
            BuildEdgeFlowModelYaml(),
            mode: "telemetry",
            seriesOutputs: seriesOutputs,
            manifestSeries: manifestSeries,
            classIds: new[] { "vip", "standard" });
    }

    private void CreateEdgeFlowRouterSubsetRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["Router_arrivals.csv"] = new[] { 10d, 10d, 10d, 10d },
            ["Router_served.csv"] = new[] { 10d, 10d, 10d, 10d },
            ["QueueA_arrivals.csv"] = new[] { 6d, 6d, 6d, 6d },
            ["QueueA_served.csv"] = new[] { 6d, 6d, 6d, 6d },
            ["QueueB_arrivals.csv"] = new[] { 4d, 4d, 4d, 4d },
            ["QueueB_served.csv"] = new[] { 4d, 4d, 4d, 4d }
        };

        var edgeSeries = new Dictionary<string, double[]>
        {
            ["edge_router_to_a_flowVolume@EDGE_ROUTER_TO_A_FLOWVOLUME@vip.csv"] = new[] { 6d, 6d, 6d, 6d },
            ["edge_router_to_b_flowVolume@EDGE_ROUTER_TO_B_FLOWVOLUME@standard.csv"] = new[] { 4d, 4d, 4d, 4d }
        };

        var manifestSeries = edgeSeries.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            edgeFlowRouterSubsetRunId,
            BuildEdgeFlowRouterModelYaml(),
            mode: "telemetry",
            overrides: overrides,
            seriesOutputs: edgeSeries,
            manifestSeries: manifestSeries,
            classIds: new[] { "vip", "standard" });
    }

    private void CreateClassRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_arrivals.csv"] = new[] { 10d, 10d, 9d, 8d },
            ["OrderService_served.csv"] = new[] { 8d, 8d, 7d, 6d },
            ["OrderService_errors.csv"] = new[] { 1d, 1d, 1d, 1d }
        };

        var classSeries = new Dictionary<string, double[]>
        {
            ["OrderService_arrivals@ORDERSERVICE@vip.csv"] = new[] { 6d, 5d, 5d, 4d },
            ["OrderService_arrivals@ORDERSERVICE@standard.csv"] = new[] { 4d, 5d, 4d, 4d },
            ["OrderService_served@ORDERSERVICE@vip.csv"] = new[] { 5d, 4d, 4d, 3d },
            ["OrderService_served@ORDERSERVICE@standard.csv"] = new[] { 3d, 4d, 3d, 3d },
            ["OrderService_errors@ORDERSERVICE@vip.csv"] = new[] { 1d, 0d, 1d, 0d },
            ["OrderService_errors@ORDERSERVICE@standard.csv"] = new[] { 0d, 1d, 0d, 1d }
        };

        var manifestSeries = classSeries.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            classRunId,
            BuildValidModelYaml(),
            mode: "telemetry",
            overrides: overrides,
            seriesOutputs: classSeries,
            manifestSeries: manifestSeries);
    }

    private void CreateServiceWithBufferClassRun()
    {
        var classSeries = new Dictionary<string, double[]>
        {
            ["SupportQueue_arrivals@SUPPORTQUEUE@vip.csv"] = new[] { 5d, 4d, 5d, 3d },
            ["SupportQueue_arrivals@SUPPORTQUEUE@standard.csv"] = new[] { 4d, 3d, 4d, 2d },
            ["SupportQueue_queue@SUPPORTQUEUE@vip.csv"] = new[] { 1d, 4d, 8d, 0d },
            ["SupportQueue_queue@SUPPORTQUEUE@standard.csv"] = new[] { 1d, 6d, 12d, 0d }
        };

        var manifestSeries = classSeries.Keys
            .Select(fileName => (id: Path.GetFileNameWithoutExtension(fileName), path: $"series/{fileName}", unit: "entities/bin"))
            .ToArray();

        CreateRun(
            queueClassRunId,
            BuildValidModelYaml(),
            mode: "telemetry",
            seriesOutputs: classSeries,
            manifestSeries: manifestSeries);
    }

    private void CreateServiceWithBufferDerivedRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["BufferService_arrivals.csv"] = new[] { 5d, 4d, 3d, 2d },
            ["BufferService_served.csv"] = new[] { 2d, 4d, 1d, 2d },
            ["BufferService_errors.csv"] = new[] { 0d, 1d, 0d, 0d },
            ["BufferService_queue.csv"] = new[] { 10d, 5d, 0d, 4d },
            ["BufferService_capacity.csv"] = new[] { 4d, 4d, 4d, 4d },
            ["BufferService_processingTimeMsSum.csv"] = new[] { 400d, 800d, 100d, 600d },
            ["BufferService_servedCount.csv"] = new[] { 2d, 4d, 1d, 2d }
        };

        CreateRun(
            serviceWithBufferDerivedRunId,
            BuildServiceWithBufferDerivedModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateServiceWithBufferParallelismRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["BufferService_arrivals.csv"] = new[] { 5d, 4d, 3d, 2d },
            ["BufferService_served.csv"] = new[] { 2d, 4d, 1d, 2d },
            ["BufferService_errors.csv"] = new[] { 0d, 1d, 0d, 0d },
            ["BufferService_queue.csv"] = new[] { 10d, 5d, 0d, 4d },
            ["BufferService_capacity.csv"] = new[] { 4d, 4d, 4d, 4d },
            ["BufferService_processingTimeMsSum.csv"] = new[] { 400d, 800d, 100d, 600d },
            ["BufferService_servedCount.csv"] = new[] { 2d, 4d, 1d, 2d }
        };

        CreateRun(
            serviceWithBufferParallelismRunId,
            BuildServiceWithBufferParallelismModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateServiceWithBufferBehaviorRuns()
    {
        var arrivals = new[] { 8d, 8d, 8d, 8d };
        var capacity = new[] { 4d, 4d, 4d, 4d };
        var baseline = BuildQueueSimulation(arrivals, capacity, parallelism: 1d);
        var parallel = BuildQueueSimulation(arrivals, capacity, parallelism: 2d);

        CreateRun(
            serviceWithBufferBehaviorBaselineRunId,
            BuildServiceWithBufferBehaviorModelYaml(parallelism: null),
            mode: "telemetry",
            overrides: new Dictionary<string, double[]>
            {
                ["BufferService_arrivals.csv"] = arrivals,
                ["BufferService_served.csv"] = baseline.Served,
                ["BufferService_queue.csv"] = baseline.Queue,
                ["BufferService_capacity.csv"] = capacity,
                ["BufferService_errors.csv"] = new[] { 0d, 0d, 0d, 0d }
            });

        CreateRun(
            serviceWithBufferBehaviorParallelRunId,
            BuildServiceWithBufferBehaviorModelYaml(parallelism: 2d),
            mode: "telemetry",
            overrides: new Dictionary<string, double[]>
            {
                ["BufferService_arrivals.csv"] = arrivals,
                ["BufferService_served.csv"] = parallel.Served,
                ["BufferService_queue.csv"] = parallel.Queue,
                ["BufferService_capacity.csv"] = capacity,
                ["BufferService_errors.csv"] = new[] { 0d, 0d, 0d, 0d }
            });
    }

    private void CreateServiceWithBufferPartialRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["BufferService_arrivals.csv"] = new[] { 5d, 4d, 3d, 2d },
            ["BufferService_served.csv"] = new[] { 2d, 4d, 1d, 2d },
            ["BufferService_errors.csv"] = new[] { 0d, 1d, 0d, 0d },
            ["BufferService_queue.csv"] = new[] { 10d, 5d, 0d, 4d }
        };

        CreateRun(
            serviceWithBufferPartialRunId,
            BuildServiceWithBufferPartialModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateServiceTimeZeroRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_arrivals.csv"] = new[] { 0d, 10d, 0d, 5d },
            ["OrderService_served.csv"] = new[] { 0d, 10d, 0d, 5d },
            ["OrderService_processingTimeMsSum.csv"] = new[] { 0d, 2000d, 0d, 1000d },
            ["OrderService_servedCount.csv"] = new[] { 0d, 10d, 0d, 5d }
        };

        CreateRun(
            serviceTimeZeroRunId,
            BuildValidModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateDispatchScheduleRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["SupportQueue_served.csv"] = new[] { 0d, 5d, 0d, 0d },
            ["SupportQueue_queue.csv"] = new[] { 5d, 0d, 6d, 7d }
        };

        CreateRun(dispatchScheduleRunId, BuildDispatchScheduleModelYaml(), mode: "telemetry", overrides: overrides);
    }

    private void CreateThroughputOverflowRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["OrderService_arrivals.csv"] = new[] { 1d, 1d, 1d, 1d },
            ["OrderService_served.csv"] = new[] { 2d, 1.5d, 1.25d, 1.1d }
        };

        CreateRun(throughputOverflowRunId, BuildValidModelYaml(), mode: "telemetry", overrides: overrides);
    }

    private void CreateBacklogWarningsRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["BufferService_arrivals.csv"] = new[] { 10d, 12d, 14d, 16d },
            ["BufferService_served.csv"] = new[] { 2d, 2d, 2d, 2d },
            ["BufferService_errors.csv"] = new[] { 0d, 0d, 0d, 0d },
            ["BufferService_queue.csv"] = new[] { 5d, 8d, 12d, 15d },
            ["BufferService_capacity.csv"] = new[] { 5d, 5d, 5d, 5d },
            ["BufferService_processingTimeMsSum.csv"] = new[] { 400d, 400d, 400d, 400d },
            ["BufferService_servedCount.csv"] = new[] { 2d, 2d, 2d, 2d }
        };

        CreateRun(
            backlogWarningsRunId,
            BuildBacklogWarningsModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateBacklogWarningsParallelismRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["BufferService_arrivals.csv"] = new[] { 7d, 8d, 9d, 10d },
            ["BufferService_served.csv"] = new[] { 7d, 8d, 9d, 10d },
            ["BufferService_errors.csv"] = new[] { 0d, 0d, 0d, 0d },
            ["BufferService_queue.csv"] = new[] { 0d, 0d, 0d, 0d },
            ["BufferService_capacity.csv"] = new[] { 5d, 5d, 5d, 5d },
            ["BufferService_processingTimeMsSum.csv"] = new[] { 350d, 400d, 450d, 500d },
            ["BufferService_servedCount.csv"] = new[] { 7d, 8d, 9d, 10d }
        };

        CreateRun(
            backlogWarningsParallelismRunId,
            BuildBacklogWarningsParallelismModelYaml(),
            mode: "telemetry",
            overrides: overrides);
    }

    private void CreateSinkRun()
    {
        var overrides = new Dictionary<string, double[]>
        {
            ["TerminalSuccess_arrivals.csv"] = new[] { 10d, 10d, 10d, 10d },
            ["TerminalSuccess_served.csv"] = new[] { 10d, 10d, 10d, 10d },
            ["TerminalSuccess_errors.csv"] = new[] { 0d, 0d, 0d, 0d }
        };

        CreateRun(
            sinkRunId,
            BuildSinkModelYaml(),
            mode: "telemetry",
            overrides: overrides);
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
        IReadOnlyCollection<(string id, string path, string unit)>? manifestSeries = null,
        IReadOnlyCollection<string>? classIds = null)
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
        WriteSeriesIndex(seriesDir, manifestSeries, classIds);
        File.WriteAllText(Path.Combine(runDir, "run.json"), BuildRunJson(runIdentifier, mode, manifestSeries), System.Text.Encoding.UTF8);
    }

    private static void WriteBaseSeries(string modelDir)
    {
        WriteSeries(modelDir, "OrderService_arrivals.csv", new double[] { 10, 10, 10, 10 });
        WriteSeries(modelDir, "OrderService_served.csv", new double[] { 9, 6, 9, 4 });
        WriteSeries(modelDir, "OrderService_errors.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_attempts.csv", new double[] { 10, 7, 10, 5 });
        WriteSeries(modelDir, "OrderService_failures.csv", new double[] { 1, 1, 1, 1 });
        WriteSeries(modelDir, "OrderService_exhaustedFailures.csv", new double[] { 0, 0, 1, 1 });
        WriteSeries(modelDir, "OrderService_retryEcho.csv", new double[] { 0.0, 0.6, 0.9, 1.0 });
        WriteSeries(modelDir, "OrderService_retryBudgetRemaining.csv", new double[] { 3, 3, 2, 1 });
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
        exhaustedFailures: "file:OrderService_exhaustedFailures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: "file:OrderService_retryBudgetRemaining.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: null
        servedCount: null
        slaMin: null
        maxAttempts: 4
        exhaustedPolicy: "dlq"
        backoffStrategy: "linear"
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

    private static string BuildRetryEdgesModelYaml()
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
        exhaustedFailures: "file:OrderService_exhaustedFailures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: "file:OrderService_retryBudgetRemaining.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        maxAttempts: 4
        exhaustedPolicy: "dlq"
        backoffStrategy: "linear"
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
  edges:
    - id: "edge_order_support_attempts"
      from: "OrderService:attempts"
      to: "SupportQueue:arrivals"
      type: "effort"
      measure: "attempts"
      multiplier: 2.0
      lag: 1
    - id: "edge_order_support_failures"
      from: "OrderService:failures"
      to: "SupportQueue:errors"
      type: "effort"
      measure: "failures"
      multiplier: 0.5
      lag: 0
""";
    }

    private static string BuildEdgeFlowModelYaml()
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
    - id: "SupportQueue"
      kind: "queue"
      semantics:
        arrivals: "file:SupportQueue_arrivals.csv"
        served: "file:SupportQueue_served.csv"
        errors: "file:SupportQueue_errors.csv"
        queueDepth: "file:SupportQueue_queue.csv"
  edges:
    - id: "order_to_support"
      from: "OrderService:out"
      to: "SupportQueue:in"

""";
    }

    private static string BuildEdgeFlowRouterModelYaml()
    {
        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

classes:
  - id: "vip"
  - id: "standard"

topology:
  nodes:
    - id: "Router"
      kind: "router"
      semantics:
        arrivals: "file:Router_arrivals.csv"
        served: "file:Router_served.csv"
    - id: "QueueA"
      kind: "service"
      semantics:
        arrivals: "file:QueueA_arrivals.csv"
        served: "file:QueueA_served.csv"
    - id: "QueueB"
      kind: "service"
      semantics:
        arrivals: "file:QueueB_arrivals.csv"
        served: "file:QueueB_served.csv"
  edges:
    - id: "router_to_a"
      from: "Router:out"
      to: "QueueA:in"
    - id: "router_to_b"
      from: "Router:out"
      to: "QueueB:in"

nodes:
  - id: "Router"
    kind: router
    inputs:
      queue: "file:Router_served.csv"
    routes:
      - target: "file:QueueA_arrivals.csv"
        classes: ["vip"]
      - target: "file:QueueB_arrivals.csv"
        classes: ["standard"]

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

    private static void WriteSeriesIndex(
        string seriesDirectory,
        IReadOnlyCollection<(string id, string path, string unit)>? entries,
        IReadOnlyCollection<string>? classIds = null)
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
                @class = ExtractClassId(entry.id),
                points = binCount,
                hash = $"sha256:{entry.id}"
            }).ToArray(),
            classes = (classIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new { id })
                .ToArray()
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

    private static string ExtractClassId(string id)
    {
        var firstAt = id.IndexOf('@');
        if (firstAt < 0)
        {
            return "DEFAULT";
        }

        var remainder = id[(firstAt + 1)..];
        var secondAt = remainder.IndexOf('@');
        if (secondAt < 0 || secondAt == remainder.Length - 1)
        {
            return "DEFAULT";
        }

        return remainder[(secondAt + 1)..];
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
        exhaustedFailures: "file:OrderService_exhaustedFailures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: "file:OrderService_retryBudgetRemaining.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        maxAttempts: 4
        exhaustedPolicy: "dlq"
        backoffStrategy: "linear"
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

    private static string BuildSinkModelYaml()
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
    - id: "TerminalSuccess"
      kind: "sink"
      semantics:
        arrivals: "file:TerminalSuccess_arrivals.csv"
        served: "file:TerminalSuccess_served.csv"
        errors: "file:TerminalSuccess_errors.csv"
      dispatchSchedule:
        kind: "time-based"
        periodBins: 2
        phaseOffset: 0
  edges: []

""";
    }

    private static string BuildDispatchScheduleModelYaml()
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
        exhaustedFailures: "file:OrderService_exhaustedFailures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: "file:OrderService_retryBudgetRemaining.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        maxAttempts: 4
        exhaustedPolicy: "dlq"
        backoffStrategy: "linear"
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

nodes:
  - id: "QueueInflow"
    kind: "const"
    values: [1, 1, 1, 1]
  - id: "QueueOutflow"
    kind: "const"
    values: [1, 1, 1, 1]
  - id: "QueueCapacity"
    kind: "const"
    values: [5, 5, 5, 5]
  - id: "SupportQueue"
    kind: "serviceWithBuffer"
    inflow: "QueueInflow"
    outflow: "QueueOutflow"
    dispatchSchedule:
      periodBins: 4
      phaseOffset: 5
      capacitySeries: "QueueCapacity"

""";
    }

    private static string BuildServiceWithBufferDerivedModelYaml()
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
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
        capacity: "file:BufferService_capacity.csv"
        processingTimeMsSum: "file:BufferService_processingTimeMsSum.csv"
        servedCount: "file:BufferService_servedCount.csv"
  edges: []

""";
    }

    private static (double[] Served, double[] Queue) BuildQueueSimulation(
        IReadOnlyList<double> arrivals,
        IReadOnlyList<double> capacity,
        double parallelism)
    {
        if (arrivals.Count != capacity.Count)
        {
            throw new ArgumentException("Arrivals and capacity series length must match.");
        }

        if (parallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parallelism), "Parallelism must be positive.");
        }

        var served = new double[arrivals.Count];
        var queue = new double[arrivals.Count];
        var pending = 0d;

        for (var i = 0; i < arrivals.Count; i += 1)
        {
            var available = capacity[i] * parallelism;
            var demand = pending + arrivals[i];
            var completed = Math.Min(available, demand);
            pending = Math.Max(demand - completed, 0d);
            served[i] = completed;
            queue[i] = pending;
        }

        return (served, queue);
    }

    private static string BuildServiceWithBufferParallelismModelYaml()
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
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
        capacity: "file:BufferService_capacity.csv"
        parallelism: 2
        processingTimeMsSum: "file:BufferService_processingTimeMsSum.csv"
        servedCount: "file:BufferService_servedCount.csv"
  edges: []

""";
    }

    private static string BuildServiceWithBufferBehaviorModelYaml(double? parallelism)
    {
        var parallelismLine = parallelism.HasValue
            ? $"        parallelism: {parallelism.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

        return $"""
schemaVersion: 1

grid:
  bins: {binCount}
  binSize: {binSizeMinutes}
  binUnit: minutes
  startTimeUtc: "{startTimeUtc:O}"

topology:
  nodes:
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
        capacity: "file:BufferService_capacity.csv"
{parallelismLine}
  edges: []

""";
    }

    private static string BuildBacklogWarningsModelYaml()
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
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
        capacity: "file:BufferService_capacity.csv"
        processingTimeMsSum: "file:BufferService_processingTimeMsSum.csv"
        servedCount: "file:BufferService_servedCount.csv"
        slaMin: 10
  edges: []

""";
    }

    private static string BuildBacklogWarningsParallelismModelYaml()
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
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
        capacity: "file:BufferService_capacity.csv"
        parallelism: 2
        processingTimeMsSum: "file:BufferService_processingTimeMsSum.csv"
        servedCount: "file:BufferService_servedCount.csv"
        slaMin: 10
  edges: []

""";
    }

    private static string BuildServiceWithBufferPartialModelYaml()
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
    - id: "BufferService"
      kind: "serviceWithBuffer"
      semantics:
        arrivals: "file:BufferService_arrivals.csv"
        served: "file:BufferService_served.csv"
        errors: "file:BufferService_errors.csv"
        queueDepth: "file:BufferService_queue.csv"
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
        exhaustedFailures: "file:OrderService_exhaustedFailures.csv"
        retryEcho: "file:OrderService_retryEcho.csv"
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: "file:OrderService_retryBudgetRemaining.csv"
        externalDemand: null
        queueDepth: null
        capacity: "file:OrderService_capacity.csv"
        processingTimeMsSum: "file:OrderService_processingTimeMsSum.csv"
        servedCount: "file:OrderService_servedCount.csv"
        slaMin: null
        maxAttempts: 4
        exhaustedPolicy: "dlq"
        backoffStrategy: "linear"
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
