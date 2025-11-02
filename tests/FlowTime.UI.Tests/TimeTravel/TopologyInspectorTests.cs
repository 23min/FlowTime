using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
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
            ["latencyMinutes"] = new double?[] { 1.2, 1.1, 1.0 },
            ["errorRate"] = new double?[] { 0.01, 0.02, 0.03 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("svc");

        Assert.Collection(metrics,
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
                Assert.Equal("Latency", block.Title);
                Assert.False(block.IsPlaceholder);
            },
            block =>
            {
                Assert.Equal("Error rate", block.Title);
                Assert.False(block.IsPlaceholder);
            });
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
                        Queue: null,
                        Capacity: null,
                        Series: null,
                        Expression: null,
                        Distribution: new TopologyNodeDistribution(
                            new double[] { 1, 2, 3 },
                            new double[] { 0.2, 0.3, 0.5 }),
                        InlineValues: null))
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
            block => Assert.Equal("Values", block.Title),
            block => Assert.Equal("E[Output]", block.Title));
        Assert.All(metrics, block => Assert.False(block.IsPlaceholder));
    }

    private static TopologyNodeSemantics EmptySemantics() =>
        new(null, null, null, null, null, null, null, null, null);

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
