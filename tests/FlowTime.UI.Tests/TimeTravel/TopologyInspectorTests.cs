using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyInspectorTests
{
    [Fact]
    public void InspectorRemainsOpenDuringMetricsUpdate()
    {
        var topology = new Topology();

        topology.TestOnNodeFocused("service-node");
        Assert.True(topology.TestIsInspectorOpen());

        topology.TestUpdateActiveMetrics(0);

        Assert.True(topology.TestIsInspectorOpen());
        Assert.Equal("service-node", topology.TestGetInspectorNodeId());
    }

    [Fact]
    public void InspectorClosesWhenFocusClears()
    {
        var topology = new Topology();

        topology.TestOnNodeFocused("service-node");
        Assert.True(topology.TestIsInspectorOpen());

        topology.TestOnNodeFocused(null);

        Assert.False(topology.TestIsInspectorOpen());
        Assert.Null(topology.TestGetInspectorNodeId());
    }

    [Fact]
    public void InspectorReopensWhenNodeRefocused()
    {
        var topology = new Topology();

        topology.TestOnNodeFocused("service-node");
        topology.TestOnNodeFocused(null);
        topology.TestOnNodeFocused("service-node");

        Assert.True(topology.TestIsInspectorOpen());
        Assert.Equal("service-node", topology.TestGetInspectorNodeId());
    }

    [Fact]
    public void ComputedNodeWithoutSuccessSeries_ShowsOnlyOutput()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { new TopologyNode("expr-1", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["values"] = new double?[] { 0.2, 0.4, 0.6 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["expr-1"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("expr-1");

        Assert.Single(metrics);
        Assert.Equal("Output", metrics[0].Title);
    }

    [Fact]
    public void ComputedNodeWithSuccessSeries_StillShowsOnlyOutputAndError()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { new TopologyNode("expr-2", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["values"] = new double?[] { 0.5, 0.6, 0.7 },
            ["successRate"] = new double?[] { 0.5, 0.6, 0.7 },
            ["errorRate"] = new double?[] { 0.1, 0.08, 0.05 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["expr-2"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("expr-2");

        Assert.Collection(metrics,
            block => Assert.Equal("Output", block.Title),
            block => Assert.Equal("Error rate", block.Title));
    }

    [Fact]
    public void BuildInspectorMetrics_ServiceNode_ReturnsExpectedStack()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.95, 0.97, 0.99 },
            ["utilization"] = new double?[] { 0.42, 0.58, 0.61 },
            ["serviceTimeMs"] = new double?[] { 240, 255, 265 },
            ["latencyMinutes"] = new double?[] { 1.2, 1.1, 1.0 },
            ["errorRate"] = new double?[] { 0.01, 0.02, 0.03 },
            ["attempts"] = new double?[] { 12, 11, 10 },
            ["served"] = new double?[] { 10, 9, 8 },
            ["failures"] = new double?[] { 2, 2, 2 },
            ["retryEcho"] = new double?[] { 0.0, 0.4, 0.2 },
            ["retryTax"] = new double?[] { 0.05, 0.08, 0.12 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc"] = sparkline
        });

        var sparklineData = topology.TestGetNodeSparklines()["svc"];
        Assert.Contains("attempts", sparklineData.Series.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("retryEcho", sparklineData.Series.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("retryTax", sparklineData.Series.Keys, StringComparer.OrdinalIgnoreCase);

        var metrics = topology.TestBuildInspectorMetrics("svc");

        Assert.Collection(metrics,
            block =>
            {
                Assert.Equal("Attempts", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Served", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Failed retries", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Retry echo", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Retry tax", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.NotNull(block.Sparkline);
            },
            block =>
            {
                Assert.Equal("Success rate", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.NotNull(block.Sparkline);
            },
            block =>
            {
                Assert.Equal("Utilization", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Service time", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.NotNull(block.Sparkline);
            },
            block =>
            {
                Assert.Equal("Flow latency", block.Title);
                Assert.True(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Error rate", block.Title);
                Assert.False(block.IsPlaceholder);
            });
    }

    [Fact]
    public void BuildInspectorMetrics_UsesAliasesWhenPresent()
    {
        var aliasSemantics = new TopologyNodeSemantics(
            Arrivals: "arrivals",
            Served: "served",
            Errors: "errors",
            Attempts: "attempts",
            Failures: null,
            ExhaustedFailures: null,
            RetryEcho: "retryEcho",
            RetryBudgetRemaining: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["attempts"] = "Ticket submissions",
                ["served"] = "Incidents resolved",
                ["retryEcho"] = "Echo retries"
            });

        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, aliasSemantics)
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.95, 0.96, 0.97 },
            ["utilization"] = new double?[] { 0.4, 0.5, 0.6 },
            ["serviceTimeMs"] = new double?[] { 200, 210, 220 },
            ["latencyMinutes"] = new double?[] { 1.2, 1.1, 1.0 },
            ["errorRate"] = new double?[] { 0.02, 0.01, 0.01 },
            ["attempts"] = new double?[] { 15, 14, 13 },
            ["served"] = new double?[] { 14, 13, 12 },
            ["retryEcho"] = new double?[] { 0.1, 0.2, 0.3 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("svc");
        Assert.Contains(blocks, block => string.Equals(block.Title, "Attempts: Ticket submissions", StringComparison.Ordinal));
        Assert.Contains(blocks, block => string.Equals(block.Title, "Served: Incidents resolved", StringComparison.Ordinal));
        Assert.Contains(blocks, block => string.Equals(block.Title, "Retry echo: Echo retries", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildInspectorDependencies_RespectsOverlayFilters()
    {
        var topology = new Topology();
        var nodes = new[]
        {
            new TopologyNode("producer", "service", Array.Empty<string>(), new[] { "svc" }, 0, 0, 0, 0, false, EmptySemantics()),
            new TopologyNode("svc", "service", new[] { "producer" }, Array.Empty<string>(), 1, 0, 100, 120, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("dep_attempts", "producer", "svc", 1, "dependency", "attempts"),
            new TopologyEdge("dep_arrivals", "producer", "svc", 1, "dependency", "arrivals")
        };

        topology.TestSetTopologyGraph(new TopologyGraph(nodes, edges));
        topology.TestSetOverlaySettings(new TopologyOverlaySettings
        {
            ShowArrivalsDependencies = false,
            ShowRetryMetrics = true
        });

        var dependencies = topology.TestBuildInspectorDependencies("svc");

        var dependency = Assert.Single(dependencies);
        Assert.Equal("dep_attempts", dependency.EdgeId);
        Assert.Equal("Attempts", dependency.Label);
    }

    [Fact]
    public void BuildInspectorDependencies_UsesProducerAliases()
    {
        var topology = new Topology();
        var producerSemantics = new TopologyNodeSemantics(
            Arrivals: null,
            Served: null,
            Errors: null,
            Attempts: null,
            Failures: null,
            ExhaustedFailures: null,
            RetryEcho: null,
            RetryBudgetRemaining: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["served"] = "Cases closed"
            });

        var nodes = new[]
        {
            new TopologyNode("producer", "service", Array.Empty<string>(), new[] { "svc" }, 0, 0, 0, 0, false, producerSemantics),
            new TopologyNode("svc", "service", new[] { "producer" }, Array.Empty<string>(), 1, 0, 0, 0, false, EmptySemantics())
        };

        var edges = new[]
        {
            new TopologyEdge("dep_served", "producer:out", "svc:in", 1, "dependency", "served")
        };

        topology.TestSetTopologyGraph(new TopologyGraph(nodes, edges));

        var dependencies = topology.TestBuildInspectorDependencies("svc");
        var dependency = Assert.Single(dependencies);
        Assert.Equal("Served: Cases closed", dependency.Label);
    }

    [Fact]
    public void BuildInspectorMetrics_QueueNodeWithMissingSeries_UsesPlaceholderAndLogsOnce()
    {
        var topology = new Topology();
        var logger = new TestLogger<Topology>();
        topology.TestSetLogger(logger);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("queue-a", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()),
                new TopologyNode("queue-b", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 1, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var queueSparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 10, 12, 8 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue-a"] = queueSparkline,
            ["queue-b"] = queueSparkline
        });

        var metricsA = topology.TestBuildInspectorMetrics("queue-a");
        var metricsB = topology.TestBuildInspectorMetrics("queue-b");

        Assert.Equal(4, metricsA.Count);
        Assert.Equal("Queue depth", metricsA[0].Title);
        Assert.False(metricsA[0].IsPlaceholder);

        Assert.True(metricsA[1].IsPlaceholder);
        Assert.Equal(Topology.InspectorMissingSeriesMessage, metricsA[1].Placeholder);
        Assert.True(metricsA[2].IsPlaceholder);
        Assert.True(metricsA[3].IsPlaceholder);

        Assert.Equal(4, metricsB.Count);

        var warningCount = logger.Entries.Count(entry => entry.Level == LogLevel.Warning);
        Assert.Equal(1, warningCount);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("queue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildInspectorMetrics_QueueNode_ReturnsQueueLatencyArrivalsServed()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode(
                    "queue-good",
                    "queue",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    0,
                    0,
                    0,
                    false,
                    new TopologyNodeSemantics(
                        Arrivals: "queue_in",
                        Served: "queue_out",
                        Errors: "queue_err",
                        Attempts: null,
                        Failures: null,
                        ExhaustedFailures: null,
                        RetryEcho: null,
                        RetryBudgetRemaining: null,
                        Queue: "queue_depth",
                        Capacity: null,
                        Series: null,
                        Expression: null,
                        Distribution: null,
                        InlineValues: null,
                        Aliases: null))
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 12, 15, 10 },
            ["latencyMinutes"] = new double?[] { 6, null, 5 },
            ["arrivals"] = new double?[] { 10, 12, 8 },
            ["served"] = new double?[] { 8, 0, 9 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue-good"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("queue-good");
        Assert.Equal(4, blocks.Count);

        Assert.Collection(blocks,
            block =>
            {
                Assert.Equal("Queue depth", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.Equal("queue", block.SeriesKey);
            },
            block =>
            {
                Assert.Equal("Latency", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.Equal("latencyMinutes", block.SeriesKey);
            },
            block =>
            {
                Assert.Equal("Arrivals", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.Equal("arrivals", block.SeriesKey);
            },
            block =>
            {
                Assert.Equal("Served", block.Title);
                Assert.False(block.IsPlaceholder);
                Assert.Equal("served", block.SeriesKey);
            });
    }

    [Fact]
    public void BuildInspectorMetrics_PmfNode_IncludesDistribution()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode(
                    "pmf-1",
                    "pmf",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    0,
                    0,
                    0,
                    false,
                    new TopologyNodeSemantics(
                        Arrivals: null,
                        Served: null,
                        Errors: null,
                        Attempts: null,
                        Failures: null,
                        ExhaustedFailures: null,
                        RetryEcho: null,
                        RetryBudgetRemaining: null,
                        Queue: null,
                        Capacity: null,
                        Series: null,
                        Expression: null,
                        Distribution: new TopologyNodeDistribution(
                            new double[] { 1, 2, 3 },
                            new double[] { 0.2, 0.3, 0.5 }),
                        InlineValues: null,
                        Aliases: null))
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["probability"] = new double?[] { 0.2, 0.3, 0.5 },
            ["values"] = new double?[] { 1.0, 2.0, 3.0 },
            ["expectation"] = new double?[] { 2.3, 2.3, 2.3 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["pmf-1"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("pmf-1");

        Assert.Collection(metrics,
            block => Assert.Equal("Probability", block.Title),
            block => Assert.Equal("Values", block.Title));
        Assert.All(metrics, block => Assert.False(block.IsPlaceholder));
    }

    [Fact]
    public void InspectorSparklineStroke_FollowsColorBasis()
    {
        var topology = new Topology();

        // Service node with a single metric slice; select Utilization basis
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { new TopologyNode("svc-x", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.97 },
            ["utilization"] = new double?[] { 0.96 }
        };

        var sparkline = CreateSparkline(series);
        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-x"] = sparkline
        });

        topology.TestUpdateActiveMetrics(0);

        // First, with SLA basis expect green for success rate
        var settingsSla = new TopologyOverlaySettings
        {
            ColorBasis = TopologyColorBasis.Sla,
            UtilizationWarningThreshold = 0.90,
            ErrorRateAlertThreshold = 0.05,
            SlaWarningThreshold = 0.95
        };
        topology.TestSetOverlaySettings(settingsSla);
        var blocksSla = topology.TestBuildInspectorMetrics("svc-x");
        Assert.NotEmpty(blocksSla);
        var successBlock = Assert.Single(blocksSla, b => string.Equals(b.Title, "Success rate", StringComparison.OrdinalIgnoreCase));
        Assert.False(successBlock.IsPlaceholder);
        Assert.NotNull(successBlock.Sparkline);
        Assert.Equal(0.97, successBlock.Sparkline!.Values[0]);
        Assert.Equal(0, successBlock.Sparkline.StartIndex);
        var thresholds = ColorScale.ColorThresholds.FromOverlay(settingsSla);
        var expectedColor = ColorScale.GetFill(new NodeBinMetrics(0.97, null, null, null, null, null), TopologyColorBasis.Sla, thresholds);
        Assert.Equal("#009E73", expectedColor);
        Assert.Equal("#009E73", successBlock.Stroke); // Success rate under SLA basis

        // Now switch to Utilization basis; color should not be green and not neutral
        var settingsUtil = new TopologyOverlaySettings
        {
            ColorBasis = TopologyColorBasis.Utilization,
            UtilizationWarningThreshold = 0.90,
            ErrorRateAlertThreshold = 0.05,
            SlaWarningThreshold = 0.95
        };
        topology.TestSetOverlaySettings(settingsUtil);
        var blocksUtil = topology.TestBuildInspectorMetrics("svc-x");
        Assert.NotEmpty(blocksUtil);
        var utilizationBlock = Assert.Single(blocksUtil, b => string.Equals(b.Title, "Utilization", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("#009E73", utilizationBlock.Stroke); // not success green
        Assert.NotEqual("#CBD5E1", utilizationBlock.Stroke); // not neutral gray
    }

    [Fact]
    public void BuildInspectorMetrics_HonorsRetryToggle()
    {
        var topology = new Topology();

        var semantics = new TopologyNodeSemantics(
            Arrivals: "arrivals",
            Served: "served",
            Errors: "errors",
            Attempts: "attempts",
            Failures: "failures",
            ExhaustedFailures: null,
            RetryEcho: "retryEcho",
            RetryBudgetRemaining: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { new TopologyNode("svc-retry", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics) },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.9 },
            ["served"] = new double?[] { 10.0 },
            ["attempts"] = new double?[] { 12.0 },
            ["failures"] = new double?[] { 2.0 },
            ["retryEcho"] = new double?[] { 1.0 },
            ["exhaustedFailures"] = new double?[] { 0.5 },
            ["retryBudgetRemaining"] = new double?[] { 2.0 }
        };

        var sparkline = CreateSparkline(series);
        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-retry"] = sparkline
        });

        topology.TestSetOverlaySettings(TopologyOverlaySettings.Default.Clone());
        var defaultBlocks = topology.TestBuildInspectorMetrics("svc-retry");
        Assert.Contains(defaultBlocks, block => string.Equals(block.Title, "Attempts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(defaultBlocks, block => string.Equals(block.Title, "Failed retries", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(defaultBlocks, block => string.Equals(block.Title, "Retry echo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(defaultBlocks, block => string.Equals(block.Title, "Exhausted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(defaultBlocks, block => string.Equals(block.Title, "Retry budget remaining", StringComparison.OrdinalIgnoreCase));

        topology.TestSetOverlaySettings(new TopologyOverlaySettings { ShowRetryMetrics = false });
        var hiddenBlocks = topology.TestBuildInspectorMetrics("svc-retry");
        Assert.DoesNotContain(hiddenBlocks, block => string.Equals(block.Title, "Attempts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hiddenBlocks, block => string.Equals(block.Title, "Failed retries", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hiddenBlocks, block => string.Equals(block.Title, "Retry echo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hiddenBlocks, block => string.Equals(block.Title, "Exhausted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hiddenBlocks, block => string.Equals(block.Title, "Retry budget remaining", StringComparison.OrdinalIgnoreCase));

        topology.TestSetOverlaySettings(new TopologyOverlaySettings { ShowRetryBudget = false });
        var budgetHidden = topology.TestBuildInspectorMetrics("svc-retry");
        Assert.DoesNotContain(budgetHidden, block => string.Equals(block.Title, "Retry budget remaining", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildInspectorMetrics_UsesRetryValuesForEarlyBins()
    {
        var topology = new Topology();

        var semantics = new TopologyNodeSemantics(
            Arrivals: "arrivals",
            Served: "served",
            Errors: "errors",
            Attempts: "attempts",
            Failures: "failures",
            ExhaustedFailures: null,
            RetryEcho: "retryEcho",
            RetryBudgetRemaining: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc-retry", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>()));

        var attemptsSeries = Enumerable.Range(0, 24).Select(i => (double?)(14 + i)).ToArray();
        var arrivalsSeries = Enumerable.Range(0, 24).Select(i => (double?)(18 + i)).ToArray();
        var servedSeries = Enumerable.Range(0, 24).Select(i => (double?)(17 + i)).ToArray();
        var failuresSeries = Enumerable.Range(0, 24).Select(i => (double?)(i % 3)).ToArray();
        var retryEchoSeries = Enumerable.Range(0, 24).Select(i => (double?)(i * 0.4)).ToArray();

        var stateSeries = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = arrivalsSeries,
            ["served"] = servedSeries,
            ["attempts"] = attemptsSeries,
            ["failures"] = failuresSeries,
            ["retryEcho"] = retryEchoSeries
        };

        var baseTimestamp = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var timestamps = Enumerable.Range(0, 24)
            .Select(i => baseTimestamp.AddHours(i))
            .ToArray();

        var windowData = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run_id",
                TemplateId = "template_id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "dummy-hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 23,
                BinCount = 24
            },
            TimestampsUtc = timestamps,
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc-retry",
                    Kind = "service",
                    Series = stateSeries
                }
            }
        };

        topology.TestSetWindowData(windowData);
        topology.TestUpdateActiveMetrics(0);
        topology.TestBuildNodeSparklines();
        topology.TestUpdateActiveMetrics(0);

        var sparklines = topology.TestGetNodeSparklines();
        var sparkline = Assert.Single(sparklines).Value;
        Assert.Equal(-47, sparkline.StartIndex);
        Assert.True(sparkline.Series.TryGetValue("attempts", out var attemptsSlice));
        Assert.NotNull(attemptsSlice);
        Assert.Equal(-47, attemptsSlice!.StartIndex);
        Assert.Equal(48, attemptsSlice.Values.Count);
        Assert.Equal(attemptsSeries[0], attemptsSlice.Values[47]);

        var inspectorBlocks = topology.TestBuildInspectorMetrics("svc-retry");
        var attemptsBlock = Assert.Single(inspectorBlocks, block => string.Equals(block.Title, "Attempts", StringComparison.OrdinalIgnoreCase));
        Assert.False(attemptsBlock.IsPlaceholder);
        Assert.NotNull(attemptsBlock.Sparkline);
        var offset = 0 - attemptsBlock.Sparkline!.StartIndex;
        Assert.InRange(offset, 0, attemptsBlock.Sparkline.Values.Count - 1);
        Assert.Equal(attemptsSeries[0], attemptsBlock.Sparkline.Values[offset]);
    }

    private static TopologyNodeSemantics EmptySemantics() =>
        new(
            Arrivals: null,
            Served: null,
            Errors: null,
            Attempts: null,
            Failures: null,
            ExhaustedFailures: null,
            RetryEcho: null,
            RetryBudgetRemaining: null,
            Queue: null,
            Capacity: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null,
            Metadata: null,
            MaxAttempts: null,
            BackoffStrategy: null,
            ExhaustedPolicy: null);

    private static NodeSparklineData CreateSparkline(IDictionary<string, double?[]> seriesMap)
    {
        double?[]? baseSeries = null;

        if (seriesMap.TryGetValue("probability", out var probabilitySeries) && probabilitySeries is { Length: > 0 })
        {
            baseSeries = probabilitySeries;
        }
        else if (seriesMap.TryGetValue("values", out var valueSeries) && valueSeries is { Length: > 0 })
        {
            baseSeries = valueSeries;
        }
        else if (seriesMap.TryGetValue("successRate", out var successSeries) && successSeries is { Length: > 0 })
        {
            baseSeries = successSeries;
        }

        baseSeries ??= seriesMap.Values.First(value => value is { Length: > 0 });

        var values = ToNullableList(baseSeries);
        var utilization = seriesMap.TryGetValue("utilization", out var util) ? ToNullableList(util) : Array.Empty<double?>();
        var errorRate = seriesMap.TryGetValue("errorRate", out var err) ? ToNullableList(err) : Array.Empty<double?>();
        var queue = seriesMap.TryGetValue("queue", out var queueSeries) ? ToNullableList(queueSeries) : Array.Empty<double?>();

        var additional = new Dictionary<string, SparklineSeriesSlice>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in seriesMap)
        {
            additional[pair.Key] = new SparklineSeriesSlice(ToNullableList(pair.Value), 0);
        }

        return NodeSparklineData.Create(
            values,
            utilization,
            errorRate,
            queue,
            startIndex: 0,
            additionalSeries: additional);
    }

    private static IReadOnlyList<double?> ToNullableList(double?[] values) => values;

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
