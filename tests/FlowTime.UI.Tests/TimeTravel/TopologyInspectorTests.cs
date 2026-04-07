using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
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
    public void Inspector_ShowsBacklogWarnings()
    {
        var topology = new Topology();
        var warnings = new[]
        {
            new TimeTravelStateWarningDto
            {
                Code = "backlog_growth",
                Severity = "warning",
                NodeId = "svc-a",
                Message = "Queue depth increased for 4 bins.",
                StartBin = 2,
                EndBin = 5,
                Signal = "1.4"
            },
            new TimeTravelStateWarningDto
            {
                Code = "queue_latency_gate_closed",
                Severity = "info",
                NodeId = "svc-a",
                Message = "Gate closed."
            },
            new TimeTravelStateWarningDto
            {
                Code = "backlog_overload",
                Severity = "warning",
                NodeId = "svc-b",
                Message = "Arrivals exceeded capacity."
            }
        };

        topology.TestSetWindowWarnings(warnings);

        var inspectorWarnings = topology.TestGetInspectorWarnings("svc-a");

        Assert.Single(inspectorWarnings);
        var warning = inspectorWarnings[0];
        Assert.Equal("backlog_growth", warning.Code);
        Assert.Equal("svc-a", warning.NodeId);
        Assert.Equal("Queue depth increased for 4 bins.", warning.Message);
        Assert.Equal(2, warning.StartBin);
        Assert.Equal(5, warning.EndBin);
        Assert.Equal("1.4", warning.Signal);
    }

    [Fact]
    public void Topology_ShowsSinkBadge_WhenNodeRoleSink()
    {
        var topology = new Topology();
        var nodes = new[]
        {
            CreateTopologyNode(
                "terminal-1", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                0,
                0,
                0,
                false,
                EmptySemantics(),
                NodeRole: "sink"),
            CreateTopologyNode(
                "svc", "service", "service", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                1,
                0,
                0,
                false,
                EmptySemantics())
        };

        topology.TestSetTopologyGraph(new TopologyGraph(nodes, Array.Empty<TopologyEdge>()));

        Assert.Equal("Terminal", topology.TestResolveNodeRoleLabel("terminal-1"));
        Assert.Null(topology.TestResolveNodeRoleLabel("svc"));
    }

    [Fact]
    public void Topology_ShowsSinkBadge_WhenKindSink()
    {
        var topology = new Topology();
        var nodes = new[]
        {
            CreateTopologyNode(
                "terminal-2", "sink", "sink", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                0,
                0,
                0,
                false,
                EmptySemantics())
        };

        topology.TestSetTopologyGraph(new TopologyGraph(nodes, Array.Empty<TopologyEdge>()));

        Assert.Equal("Terminal", topology.TestResolveNodeRoleLabel("terminal-2"));
    }

    [Fact]
    public void BuildInspectorMetrics_SinkNode_SuppressesUtilizationAndErrorRateWhenMissing()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("sink-node", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics(), NodeRole: "sink")
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.8, 0.9 },
            ["served"] = new double?[] { 10, 11 },
            ["serviceTimeMs"] = new double?[] { 120, 130 },
            ["flowLatencyMs"] = new double?[] { 300, 310 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["sink-node"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("sink-node");

        Assert.DoesNotContain(metrics, block => block.Title == "Utilization");
        Assert.DoesNotContain(metrics, block => block.Title == "Error rate");
    }

    [Fact]
    public void BuildInspectorMetrics_SinkNode_IncludesUtilizationAndErrorRateWhenPresent()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("sink-node", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics(), NodeRole: "sink")
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.9, 0.95 },
            ["served"] = new double?[] { 10, 11 },
            ["utilization"] = new double?[] { 0.4, 0.5 },
            ["serviceTimeMs"] = new double?[] { 120, 130 },
            ["flowLatencyMs"] = new double?[] { 300, 310 },
            ["errorRate"] = new double?[] { 0.05, 0.02 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["sink-node"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("sink-node");

        Assert.Contains(metrics, block => block.Title == "Utilization");
        Assert.Contains(metrics, block => block.Title == "Error rate");
    }

    [Fact]
    public void BuildInspectorMetrics_SinkNode_IncludesFlowLatencyWhenPresent()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("sink-node", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics(), NodeRole: "sink")
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.9, 0.95 },
            ["served"] = new double?[] { 10, 11 },
            ["flowLatencyMs"] = new double?[] { 3000, 3100 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["sink-node"] = sparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("sink-node");

        Assert.Contains(metrics, block => block.Title == "Flow latency");
    }

    [Fact]
    public void BinDump_UsesClassSelectionAndUnfilteredSeries()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 1,
                BinCount = 2
            },
            TimestampsUtc = new[]
            {
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5)
            },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "queue",
                    Kind = "serviceWithBuffer",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arrivals"] = new double?[] { 10, 20 },
                        ["served"] = new double?[] { 5, 8 },
                        ["queue"] = new double?[] { 3, 4 }
                    },
                    ByClass = new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Downtown"] = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["arrivals"] = new double?[] { 2, 4 },
                            ["served"] = new double?[] { 1, 2 },
                            ["queue"] = new double?[] { 1, 1 }
                        }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        topology.TestSetClassSelection(new[] { "Downtown" });
        topology.TestUpdateActiveMetrics(1);

        var dump = topology.TestBuildBinDump("queue");

        Assert.NotNull(dump);
        Assert.Equal(1, dump!.SelectedBin);
        Assert.Equal(2d, dump.SelectedSeries["served"]);
        Assert.Equal(8d, dump.UnfilteredSeries["served"]);
        Assert.Equal(1d, dump.SelectedSeries["queue"]);
        Assert.Equal(4d, dump.UnfilteredSeries["queue"]);
    }

    [Fact]
    public void BinDump_IncludesProvenanceCatalog()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "queue",
                    Kind = "serviceWithBuffer",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arrivals"] = new double?[] { 10 },
                        ["served"] = new double?[] { 8 },
                        ["queue"] = new double?[] { 3 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        topology.TestUpdateActiveMetrics(0);

        var dump = topology.TestBuildBinDump("queue");

        Assert.NotNull(dump);
        Assert.NotNull(dump!.Provenance);
        Assert.Equal("serviceWithBuffer", dump.Provenance!.NodeKind);
        Assert.Contains("arrivals", dump.Provenance.Metrics.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputedNodeWithoutSuccessSeries_ShowsOnlyOutput()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { CreateTopologyNode("expr-1", "expr", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
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
            new[] { CreateTopologyNode("expr-2", "expr", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
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
    public void BuildNodeSparklines_UsesSuccessRateSeries()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 2,
                BinCount = 3
            },
            TimestampsUtc = new[]
            {
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow.AddMinutes(10)
            },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["throughputRatio"] = new double?[] { 0.6, 0.8, 1.0 },
                        ["arrivals"] = new double?[] { 100, 120, 140 },
                        ["served"] = new double?[] { 60, 96, 140 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        topology.TestBuildNodeSparklines(anchorBin: 2);

        var sparklines = topology.TestGetNodeSparklines();
        var sparkline = Assert.Single(sparklines).Value;
        Assert.True(sparkline.Series.TryGetValue("successRate", out var slice));
        Assert.NotNull(slice);
        var nonNullValues = slice!.Values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        Assert.Equal(new[] { 0.6, 0.8, 1.0 }, nonNullValues);
    }

    [Fact]
    public void BuildNodeSparklines_ComputesServiceWithBufferDerivedSeries()
    {
        var topology = new Topology();

        var node = new TimeTravelNodeSeriesDto
        {
            Id = "svc-buffer",
            Kind = "serviceWithBuffer",
            Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["served"] = new double?[] { 10, 18, 24 },
                ["capacity"] = new double?[] { 20, 18, 24 },
                ["processingTimeMsSum"] = new double?[] { 1000, 1800, 2400 },
                ["servedCount"] = new double?[] { 10, 18, 24 }
            }
        };

        var derivedUtilization = topology.TestBuildUtilizationSeries(node);
        Assert.Equal(new double?[] { 0.5, 1d, 1d }, derivedUtilization);

        var derivedServiceTime = topology.TestBuildServiceTimeSeries(node);
        Assert.Equal(new double?[] { 100d, 100d, 100d }, derivedServiceTime);
    }

    [Fact]
    public void BuildServiceTimeSeries_SkipsBinsWithZeroServedCount()
    {
        var topology = new Topology();

        var node = new TimeTravelNodeSeriesDto
        {
            Id = "svc",
            Kind = "service",
            Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["processingTimeMsSum"] = new double?[] { 0, 1000 },
                ["servedCount"] = new double?[] { 0, 5 }
            }
        };

        var derivedServiceTime = topology.TestBuildServiceTimeSeries(node);

        Assert.Equal(new double?[] { null, 200d }, derivedServiceTime);
    }

    [Fact]
    public void Inspector_ProvidesMetricProvenanceTooltip()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["served"] = new double?[] { 10 },
                        ["capacity"] = new double?[] { 20 },
                        ["processingTimeMsSum"] = new double?[] { 40 },
                        ["servedCount"] = new double?[] { 2 },
                        ["flowLatencyMs"] = new double?[] { 120 },
                        ["arrivals"] = new double?[] { 10 },
                        ["errors"] = new double?[] { 0 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        var tooltip = topology.TestBuildProvenanceTooltip("svc", "utilization");

        Assert.Contains("Formula: utilization = served / capacity", tooltip);
        Assert.Contains("Inputs: served, capacity", tooltip);
        Assert.Contains("Units: percent", tooltip);
    }

    [Fact]
    public void AggregationIndicator_DefaultsToAverage()
    {
        var topology = new Topology();

        var label = topology.TestGetAggregationIndicatorLabel();

        Assert.Equal("Avg", label);
    }

    [Fact]
    public void Inspector_ProvidesAggregationMetadataInTooltip()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["processingTimeMsSum"] = new double?[] { 40 },
                        ["servedCount"] = new double?[] { 2 },
                        ["serviceTimeMs"] = new double?[] { 20 }
                    },
                    SeriesMetadata = new Dictionary<string, TimeTravelSeriesSemanticsDto>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["serviceTimeMs"] = new TimeTravelSeriesSemanticsDto
                        {
                            Aggregation = "avg",
                            Origin = "derived"
                        }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        var tooltip = topology.TestBuildProvenanceTooltip("svc", "serviceTimeMs");

        Assert.Contains("Aggregation: avg", tooltip);
    }

    [Fact]
    public void Inspector_ProvidesMetricMeaningInTooltip()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["served"] = new double?[] { 10 },
                        ["capacity"] = new double?[] { 20 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        var tooltip = topology.TestBuildProvenanceTooltip("svc", "utilization");

        Assert.Contains("Meaning: Fraction of capacity used.", tooltip);
    }

    [Fact]
    public void InspectorRows_ProvideEffectiveCapacityProvenance()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc-buffer",
                    Kind = "serviceWithBuffer",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["capacity"] = new double?[] { 10 },
                        ["parallelism"] = new double?[] { 3 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);

        var metrics = new NodeBinMetrics(
            SuccessRate: null,
            Utilization: null,
            ErrorRate: null,
            QueueDepth: null,
            LatencyMinutes: null,
            Timestamp: DateTimeOffset.UtcNow,
            NodeKind: "serviceWithBuffer",
            RawMetrics: new Dictionary<string, double?>
            {
                ["effectiveCapacity"] = 30
            });

        var rows = topology.TestBuildInspectorBinMetrics("svc-buffer", metrics);
        var effectiveCapacity = rows.FirstOrDefault(row => row.Label == "Effective capacity");

        Assert.NotNull(effectiveCapacity);
        Assert.NotNull(effectiveCapacity!.Provenance);
        Assert.Equal("effectiveCapacity = capacity * parallelism", effectiveCapacity.Provenance!.SelectedFormula?.Formula);
    }

    [Fact]
    public void Inspector_AlwaysShowsQueueLatencyRow_WhenUnavailable()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue-1", "queue", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "queue-1",
                    Kind = "queue",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        topology.TestSetWindowData(window);

        var metrics = new NodeBinMetrics(
            SuccessRate: null,
            Utilization: null,
            ErrorRate: null,
            QueueDepth: null,
            LatencyMinutes: null,
            Timestamp: DateTimeOffset.UtcNow,
            NodeKind: "queue",
            QueueLatencyStatus: new QueueLatencyStatus("gate_closed", "Gate closed."));

        var rows = topology.TestBuildInspectorBinMetrics("queue-1", metrics);
        var queueLatency = rows.FirstOrDefault(row => row.Label == "Queue latency");

        Assert.NotNull(queueLatency);
        Assert.Equal("-", queueLatency!.Value);
    }

    [Fact]
    public void Inspector_ShowsParallelismAndEffectiveCapacity()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var metrics = new NodeBinMetrics(
            SuccessRate: 0.9,
            Utilization: 0.4,
            ErrorRate: 0.02,
            QueueDepth: null,
            LatencyMinutes: 1.2,
            Timestamp: DateTimeOffset.UtcNow,
            NodeKind: "serviceWithBuffer",
            RawMetrics: new Dictionary<string, double?>
            {
                ["capacity"] = 10,
                ["parallelism"] = 3,
                ["effectiveCapacity"] = 30
            });

        var rows = topology.TestBuildInspectorBinMetrics("svc-buffer", metrics);

        Assert.Contains(rows, row => row.Label == "Instances" && row.Value == "3.0");
        Assert.Contains(rows, row => row.Label == "Effective capacity" && row.Value == "30.0");
    }

    [Fact]
    public void Inspector_SuppressesLatencyRowsForRouterNodes()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("router-1", "router", "router", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "router-1",
                    Kind = "router",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        topology.TestSetWindowData(window);

        var metrics = new NodeBinMetrics(
            SuccessRate: 0.9,
            Utilization: 0.4,
            ErrorRate: 0.02,
            QueueDepth: null,
            LatencyMinutes: 1.2,
            Timestamp: DateTimeOffset.UtcNow,
            NodeKind: "router",
            ServiceTimeMs: 50,
            FlowLatencyMs: 200);

        var rows = topology.TestBuildInspectorBinMetrics("router-1", metrics);

        Assert.DoesNotContain(rows, row => row.Label == "Latency" || row.Label == "Queue latency");
        Assert.DoesNotContain(rows, row => row.Label == "Service time");
        Assert.DoesNotContain(rows, row => row.Label == "Flow latency");
    }

    [Fact]
    public void Inspector_SuppressesLatencyRowsForServiceNodes()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc-1",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        topology.TestSetWindowData(window);

        var metrics = new NodeBinMetrics(
            SuccessRate: 0.9,
            Utilization: 0.4,
            ErrorRate: 0.02,
            QueueDepth: null,
            LatencyMinutes: 1.2,
            Timestamp: DateTimeOffset.UtcNow,
            NodeKind: "service",
            ServiceTimeMs: 50,
            FlowLatencyMs: 200);

        var rows = topology.TestBuildInspectorBinMetrics("svc-1", metrics);

        Assert.DoesNotContain(rows, row => row.Label == "Latency" || row.Label == "Queue latency");
    }

    [Fact]
    public void InspectorProperties_UsesProvenanceForUtilizationRow()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["served"] = new double?[] { 10 },
                        ["capacity"] = new double?[] { 20 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);

        var metrics = new NodeBinMetrics(null, 0.5, null, null, null, null, NodeKind: "service");
        var rows = topology.TestBuildInspectorBinMetrics("svc", metrics);
        var utilization = rows.First(row => string.Equals(row.Label, "Utilization", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(utilization.Provenance);
        Assert.NotNull(utilization.Provenance!.SelectedFormula);
        Assert.Contains("served / capacity", utilization.Provenance.SelectedFormula!.Formula);
        Assert.Equal("percent", utilization.Provenance.Definition.Unit);
    }

    [Fact]
    public async Task BinDump_AltKeyOpensTab()
    {
        var js = new RecordingJSRuntime();
        var topology = new Topology();
        topology.TestSetJsRuntime(js);
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.UtcNow },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "queue",
                    Kind = "serviceWithBuffer",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arrivals"] = new double?[] { 10 },
                        ["served"] = new double?[] { 8 },
                        ["queue"] = new double?[] { 3 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        topology.TestUpdateActiveMetrics(0);

        await topology.TestDumpInspectorBinAsync("queue", openInNewTab: true);

        Assert.Equal("FlowTime.openTextInNewTab", js.LastIdentifier);
    }

    private sealed class RecordingJSRuntime : IJSRuntime
    {
        public string? LastIdentifier { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            LastIdentifier = identifier;
            return new ValueTask<TValue>(default(TValue)!);
        }
    }

    [Fact]
    public void BuildInspectorMetrics_ServiceNode_ReturnsExpectedStack()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
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
                Assert.Equal("Arrivals", block.Title);
                Assert.True(block.IsPlaceholder);
            },
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
                Assert.NotNull(block.Sparkline);
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
    public void BuildInspectorMetrics_QueueDepthUsesQueueBasisColor()
    {
        var topology = new Topology();
        topology.TestSetOverlaySettings(new TopologyOverlaySettings
        {
            ColorBasis = TopologyColorBasis.Utilization
        });

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue-node", "queue", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var queueSparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 0.9, 0.9, 0.9 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue-node"] = queueSparkline
        });

        var metrics = topology.TestBuildInspectorMetrics("queue-node");
        var queueBlock = Assert.Single(metrics, block => block.Title == "Queue depth");
        Assert.Equal(ColorScale.ErrorColor, queueBlock.Stroke);
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
            Parallelism: null,
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
                CreateTopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, aliasSemantics)
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
    public void InspectorBinMetrics_MatchWindowSeriesForSelectedBin()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 2,
                BinCount = 3
            },
            TimestampsUtc = new[]
            {
                DateTimeOffset.UtcNow.AddMinutes(-2),
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow
            },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc-1",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arrivals"] = new double?[] { 10, 20, 30 },
                        ["served"] = new double?[] { 6, 10, 15 },
                        ["errors"] = new double?[] { 1, 1, 2 },
                        ["utilization"] = new double?[] { 0.4, 0.5, 0.6 },
                        ["serviceTimeMs"] = new double?[] { 100, 200, 300 },
                        ["flowLatencyMs"] = new double?[] { 500, 600, 700 }
                    }
                }
            }
        };

        topology.TestSetWindowData(window);
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-1"];
        Assert.NotNull(metrics.SuccessRate);
        Assert.Equal(0.5, metrics.SuccessRate!.Value, 5);
        Assert.NotNull(metrics.ErrorRate);
        Assert.Equal(0.05, metrics.ErrorRate!.Value, 5);

        var rows = topology.TestBuildInspectorBinMetrics("svc-1", metrics);
        Assert.Equal("50%", rows.First(row => row.Label == "Success rate").Value);
        Assert.Equal("5%", rows.First(row => row.Label == "Error rate").Value);
        Assert.Equal("20.0", rows.First(row => row.Label == "Arrivals").Value);
        Assert.Equal("10.0", rows.First(row => row.Label == "Served").Value);
        Assert.Equal("50%", rows.First(row => row.Label == "Utilization").Value);
        Assert.Equal("200.0 ms", rows.First(row => row.Label == "Service time").Value);
        Assert.Equal("600.0 ms", rows.First(row => row.Label == "Flow latency").Value);
    }

    [Fact]
    public void InspectorBinMetrics_ServiceNode_MatchesAllDerivedRows()
    {
        var topology = new Topology();
        var semantics = EmptySemantics() with { MaxAttempts = 5 };
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 10, 20, 30 },
            ["served"] = new double?[] { 5, 10, 15 },
            ["errors"] = new double?[] { 1, 1, 2 },
            ["utilization"] = new double?[] { 0.4, 0.5, 0.6 },
            ["serviceTimeMs"] = new double?[] { 100, 200, 300 },
            ["flowLatencyMs"] = new double?[] { 500, 600, 700 },
            ["attempts"] = new double?[] { 6, 12, 18 },
            ["failures"] = new double?[] { 1, 2, 3 },
            ["retryEcho"] = new double?[] { 0.1, 0.2, 0.3 },
            ["retryBudgetRemaining"] = new double?[] { 0.7, 0.6, 0.5 },
            ["retryTax"] = new double?[] { 0.2, 0.1, 0.3 },
            ["capacity"] = new double?[] { 40, 40, 40 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("svc-1", "service", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-1"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-1", metrics);

        AssertRowValue(rows, "Arrivals", "20.0");
        AssertRowValue(rows, "Served", "10.0");
        AssertRowValue(rows, "Errors", "1.0");
        AssertRowValue(rows, "Attempts", "12.0");
        AssertRowValue(rows, "Failed retries", "2.0");
        AssertRowValue(rows, "Retry echo", "0.2");
        AssertRowValue(rows, "Retry budget remaining", "0.6");
        AssertRowValue(rows, "Capacity", "40.0");
        AssertRowValue(rows, "Max attempts", "5.0");
        AssertRowValue(rows, "Success rate", "50%");
        AssertRowValue(rows, "Utilization", "50%");
        AssertRowValue(rows, "Error rate", "5%");
        AssertRowValue(rows, "Service time", "200.0 ms");
        AssertRowValue(rows, "Flow latency", "600.0 ms");
        AssertRowValue(rows, "Retry tax", "10%");
        AssertRowMissing(rows, "Queue latency");
    }

    [Fact]
    public void InspectorBinMetrics_ServiceTime_UsesMinutesForLargeValues()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceTimeMs"] = new double?[] { 60_000, 120_000, 180_000 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("svc-1", "service", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-1"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-1", metrics);

        AssertRowValue(rows, "Service time", "2.0 min");
    }

    [Fact]
    public void InspectorBinMetrics_FlowLatency_UsesMinutesForLargeValues()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["flowLatencyMs"] = new double?[] { 60_000, 120_000, 180_000 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("svc-1", "service", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-1"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-1", metrics);

        AssertRowValue(rows, "Flow latency", "2.0 min");
    }

    [Fact]
    public void InspectorBinMetrics_ServiceWithBufferNode_MatchesQueueAndLatencyRows()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buf", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue"] = new double?[] { 5, 6, 7 },
            ["latencyMinutes"] = new double?[] { 2, 3, 4 },
            ["arrivals"] = new double?[] { 10, 12, 14 },
            ["served"] = new double?[] { 8, 10, 12 },
            ["errors"] = new double?[] { 0, 1, 1 },
            ["utilization"] = new double?[] { 0.3, 0.4, 0.5 },
            ["serviceTimeMs"] = new double?[] { 100, 110, 120 },
            ["flowLatencyMs"] = new double?[] { 500, 600, 700 }
        };

        var status = new TimeTravelQueueLatencyStatusDto?[]
        {
            null,
            new TimeTravelQueueLatencyStatusDto { Code = "queue_latency_gate_closed" },
            null
        };

        var node = CreateSeriesNode("svc-buf", "serviceWithBuffer", series) with
        {
            QueueLatencyStatus = status
        };

        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-buf"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-buf", metrics);

        AssertRowValue(rows, "Queue depth", "6.0");
        AssertRowValue(rows, "Queue latency", "3.0 min");
        AssertRowValue(rows, "Arrivals", "12.0");
        AssertRowValue(rows, "Served", "10.0");
        AssertRowValue(rows, "Success rate", "83%");
        AssertRowValue(rows, "Utilization", "40%");
        AssertRowValue(rows, "Error rate", "8%");
        AssertRowValue(rows, "Service time", "110.0 ms");
        AssertRowValue(rows, "Flow latency", "600.0 ms");
        AssertRowValue(rows, "Latency status", "Paused (gate closed)");
    }

    [Fact]
    public void InspectorBinMetrics_QueueNode_MatchesQueueRows()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("queue-1", "queue", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue"] = new double?[] { 10, 12, 14 },
            ["latencyMinutes"] = new double?[] { 5, 6, 7 },
            ["arrivals"] = new double?[] { 20, 24, 28 },
            ["served"] = new double?[] { 10, 12, 14 },
            ["errors"] = new double?[] { 2, 3, 4 },
            ["utilization"] = new double?[] { 0.6, 0.7, 0.8 }
        };

        var status = new TimeTravelQueueLatencyStatusDto?[]
        {
            null,
            new TimeTravelQueueLatencyStatusDto { Code = "queue_latency_unreported" },
            null
        };

        var node = CreateSeriesNode("queue-1", "queue", series) with
        {
            QueueLatencyStatus = status
        };

        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["queue-1"];
        var rows = topology.TestBuildInspectorBinMetrics("queue-1", metrics);

        AssertRowValue(rows, "Queue depth", "12.0");
        AssertRowValue(rows, "Queue latency", "6.0 min");
        AssertRowValue(rows, "Arrivals", "24.0");
        AssertRowValue(rows, "Served", "12.0");
        AssertRowValue(rows, "Success rate", "50%");
        AssertRowValue(rows, "Utilization", "70%");
        AssertRowValue(rows, "Error rate", "13%");
        AssertRowValue(rows, "Latency status", "Latency unavailable");
    }

    [Fact]
    public void InspectorBinMetrics_RouterNode_MatchesRoutingRows()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("router-1", "router", "router", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 30, 40, 50 },
            ["served"] = new double?[] { 20, 32, 40 },
            ["errors"] = new double?[] { 3, 4, 5 },
            ["utilization"] = new double?[] { 0.2, 0.3, 0.4 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("router-1", "router", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["router-1"];
        var rows = topology.TestBuildInspectorBinMetrics("router-1", metrics);

        AssertRowValue(rows, "Arrivals", "40.0");
        AssertRowValue(rows, "Served", "32.0");
        AssertRowValue(rows, "Errors", "4.0");
        AssertRowValue(rows, "Success rate", "80%");
        AssertRowValue(rows, "Utilization", "30%");
        AssertRowValue(rows, "Error rate", "10%");
        AssertRowMissing(rows, "Service time");
        AssertRowMissing(rows, "Flow latency");
        AssertRowMissing(rows, "Queue latency");
    }

    [Fact]
    public void InspectorBinMetrics_SinkNode_WithSchedule_MatchesScheduleRows()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode(
                    "sink-1", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false,
                    EmptySemantics(),
                    NodeRole: "sink",
                    DispatchSchedule: new GraphDispatchScheduleModel("time-based", 4, 1, "capSeries"))
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 10, 10, 10 },
            ["served"] = new double?[] { 10, 10, 10 },
            ["errors"] = new double?[] { 0, 1, 0 }
        };

        var sla = new[]
        {
            new TimeTravelSlaSeriesDto
            {
                Kind = "scheduleAdherence",
                Values = new double?[] { 0.9, 0.8, 1.0 }
            }
        };

        var schedule = new TimeTravelDispatchScheduleDto
        {
            Kind = "time-based",
            PeriodBins = 4,
            PhaseOffset = 1,
            CapacitySeries = "capSeries"
        };

        var node = CreateSeriesNode("sink-1", "service", series, sla, schedule);
        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["sink-1"];
        var rows = topology.TestBuildInspectorBinMetrics("sink-1", metrics);

        AssertRowValue(rows, "Arrival schedule", "Every 4 bins (phase 1)");
        AssertRowValue(rows, "Schedule capacity", "capSeries");
        AssertRowValue(rows, "Schedule SLA", "80%");
        AssertRowValue(rows, "Arrivals", "10.0");
        AssertRowValue(rows, "Served", "10.0");
        AssertRowValue(rows, "Errors", "1.0");
        AssertRowMissing(rows, "Utilization");
    }

    [Fact]
    public void InspectorBinMetrics_ServiceNode_WithSchedule_ShowsCompletionAndScheduleSla()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode(
                    "svc-schedule", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false,
                    EmptySemantics(),
                    DispatchSchedule: new GraphDispatchScheduleModel("time-based", 2, 0, null))
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 10, 10, 10 },
            ["served"] = new double?[] { 10, 8, 10 },
            ["errors"] = new double?[] { 0, 1, 0 }
        };

        var sla = new[]
        {
            new TimeTravelSlaSeriesDto
            {
                Kind = "completion",
                Values = new double?[] { 0.9, 0.8, 1.0 }
            },
            new TimeTravelSlaSeriesDto
            {
                Kind = "scheduleAdherence",
                Values = new double?[] { 0.7, 0.6, 0.9 }
            }
        };

        var schedule = new TimeTravelDispatchScheduleDto
        {
            Kind = "time-based",
            PeriodBins = 2,
            PhaseOffset = 0
        };

        var node = CreateSeriesNode("svc-schedule", "service", series, sla, schedule);
        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-schedule"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-schedule", metrics);

        AssertRowValue(rows, "Completion SLA", "80%");
        AssertRowValue(rows, "Schedule SLA", "60%");
    }

    [Fact]
    public void InspectorBinMetrics_DlqNode_MatchesQueueRows()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("dlq-1", "dlq", "dlq", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue"] = new double?[] { 1, 2, 3 },
            ["latencyMinutes"] = new double?[] { 4, 5, 6 },
            ["arrivals"] = new double?[] { 5, 6, 7 },
            ["served"] = new double?[] { 0, 1, 2 },
            ["errors"] = new double?[] { 0, 1, 1 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("dlq-1", "dlq", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["dlq-1"];
        var rows = topology.TestBuildInspectorBinMetrics("dlq-1", metrics);

        AssertRowValue(rows, "Queue depth", "2.0");
        AssertRowValue(rows, "Queue latency", "5.0 min");
        AssertRowValue(rows, "Arrivals", "6.0");
        AssertRowValue(rows, "Served", "1.0");
        AssertRowValue(rows, "Success rate", "17%");
        AssertRowValue(rows, "Error rate", "17%");
    }

    [Fact]
    public void InspectorBinMetrics_ExpressionNode_UsesSeriesValue()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("expr-1", "expr", "expr", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = new double?[] { 2, 3, 4 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("expr-1", "expr", series)));
        topology.TestBuildNodeSparklines();
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["expr-1"];
        var rows = topology.TestBuildInspectorBinMetrics("expr-1", metrics);

        AssertRowValue(rows, "bin(t)", "3.0");
    }

    [Fact]
    public void InspectorBinMetrics_ConstNode_UsesSeriesValue()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("const-1", "const", "const", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = new double?[] { 9, 8, 7 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("const-1", "const", series)));
        topology.TestBuildNodeSparklines();
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["const-1"];
        var rows = topology.TestBuildInspectorBinMetrics("const-1", metrics);

        AssertRowValue(rows, "bin(t)", "8.0");
    }

    [Fact]
    public void InspectorBinMetrics_PmfNode_UsesExpectationValue()
    {
        var topology = new Topology();
        var distribution = new TopologyNodeDistribution(
            new double[] { 1, 2, 3 },
            new double[] { 0.2, 0.3, 0.5 });
        var semantics = EmptySemantics() with { Distribution = distribution };

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("pmf-1", "pmf", "pmf", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = new double?[] { 1, 2, 3 }
        };

        topology.TestSetWindowData(CreateWindowData(CreateSeriesNode("pmf-1", "pmf", series)));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["pmf-1"];
        var rows = topology.TestBuildInspectorBinMetrics("pmf-1", metrics);

        AssertRowValue(rows, "Value", "2.3");
        AssertRowValue(rows, "bin(t)", "-");
    }

    [Fact]
    public void InspectorBinMetrics_ClassSelection_UsesClassSeries()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-class", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var baseSeries = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 100, 100, 100 },
            ["served"] = new double?[] { 90, 90, 90 },
            ["errors"] = new double?[] { 2, 2, 2 }
        };

        var byClass = new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arrivals"] = new double?[] { 20, 20, 20 },
                ["served"] = new double?[] { 10, 12, 14 },
                ["errors"] = new double?[] { 1, 1, 2 }
            },
            ["B"] = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arrivals"] = new double?[] { 80, 80, 80 },
                ["served"] = new double?[] { 80, 78, 76 },
                ["errors"] = new double?[] { 1, 1, 0 }
            }
        };

        var node = CreateSeriesNode("svc-class", "service", baseSeries) with { ByClass = byClass };
        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestSetClassSelection(new[] { "A" });
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["svc-class"];
        var rows = topology.TestBuildInspectorBinMetrics("svc-class", metrics);

        AssertRowValue(rows, "Success rate", "60%");
        AssertRowValue(rows, "Error rate", "5%");
        AssertRowValue(rows, "Arrivals", "20.0");
        AssertRowValue(rows, "Served", "12.0");
        AssertRowValue(rows, "Errors", "1.0");
    }

    [Fact]
    public void InspectorBinMetrics_ScheduleSla_StatusUsesStatusLabel()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode(
                    "sink-2", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false,
                    EmptySemantics(),
                    NodeRole: "sink",
                    DispatchSchedule: new GraphDispatchScheduleModel("time-based", 2, 0, null))
            },
            Array.Empty<TopologyEdge>()));

        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = new double?[] { 5, 5, 5 },
            ["served"] = new double?[] { 5, 5, 5 }
        };

        var sla = new[]
        {
            new TimeTravelSlaSeriesDto
            {
                Kind = "scheduleAdherence",
                Status = "unavailable",
                Values = new double?[] { null, null, null }
            }
        };

        var node = CreateSeriesNode(
            "sink-2",
            "service",
            series,
            sla,
            new TimeTravelDispatchScheduleDto
            {
                Kind = "time-based",
                PeriodBins = 2,
                PhaseOffset = 0
            });

        topology.TestSetWindowData(CreateWindowData(node));
        topology.TestUpdateActiveMetrics(1);

        var metrics = topology.TestGetActiveMetrics()["sink-2"];
        var rows = topology.TestBuildInspectorBinMetrics("sink-2", metrics);

        AssertRowValue(rows, "Schedule SLA", "Unavailable");
    }

    [Fact]
    public void InspectorMetricBlocks_ShowPlaceholderWhenSeriesMissing()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-missing", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["successRate"] = new double?[] { 0.9, 0.9, 0.9 },
            ["utilization"] = new double?[] { 0.5, 0.5, 0.5 },
            ["serviceTimeMs"] = new double?[] { 100, 120, 140 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-missing"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("svc-missing");
        var flowLatency = blocks.First(block => block.Title == "Flow latency");

        Assert.True(flowLatency.IsPlaceholder);
        Assert.Equal(Topology.InspectorMissingSeriesMessage, flowLatency.Placeholder);
    }

    [Fact]
    public void BuildInspectorMetrics_ServiceWithBuffer_IncludesQueueMetrics()
    {
        var topology = new Topology();
        var semantics = new TopologyNodeSemantics(
            Arrivals: "arrivals",
            Served: "served",
            Errors: "errors",
            Attempts: null,
            Failures: null,
            ExhaustedFailures: null,
            RetryEcho: null,
            RetryBudgetRemaining: null,
            Queue: "queue_depth",
            Capacity: null,
            Parallelism: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 3, 4, 2 },
            ["latencyMinutes"] = new double?[] { 1.5, 1.2, 1.1 },
            ["arrivals"] = new double?[] { 10, 12, 9 },
            ["served"] = new double?[] { 9, 11, 8 },
            ["successRate"] = new double?[] { 0.9, 0.92, 0.94 },
            ["utilization"] = new double?[] { 0.4, 0.5, 0.45 },
            ["serviceTimeMs"] = new double?[] { 200, 210, 190 },
            ["flowLatencyMs"] = new double?[] { 300, 280, 260 },
            ["errorRate"] = new double?[] { 0.05, 0.04, 0.03 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-buffer"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("svc-buffer");
        Assert.Contains(blocks, block => block.Title == "Queue depth");
        Assert.Contains(blocks, block => block.Title == "Queue latency");
        Assert.Contains(blocks, block => block.Title == "Success rate");
        Assert.Contains(blocks, block => block.Title == "Utilization");
        Assert.Contains(blocks, block => block.Title == "Service time");
        Assert.Contains(blocks, block => block.Title == "Flow latency");
        Assert.Contains(blocks, block => block.Title == "Error rate");
    }

    [Fact]
    public void BuildInspectorMetrics_ServiceWithBuffer_UsesQueueAlias()
    {
        var topology = new Topology();
        var semantics = new TopologyNodeSemantics(
            Arrivals: "arrivals",
            Served: "served",
            Errors: "errors",
            Attempts: null,
            Failures: null,
            ExhaustedFailures: null,
            RetryEcho: null,
            RetryBudgetRemaining: null,
            Queue: "queue_depth",
            Capacity: null,
            Parallelism: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["queueDepth"] = "Open backlog"
            });

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 1, 2, 3 },
            ["latencyMinutes"] = new double?[] { 0.5, 0.6, 0.7 },
            ["arrivals"] = new double?[] { 5, 6, 7 },
            ["served"] = new double?[] { 4, 5, 6 },
            ["successRate"] = new double?[] { 0.8, 0.82, 0.85 },
            ["utilization"] = new double?[] { 0.3, 0.35, 0.4 },
            ["serviceTimeMs"] = new double?[] { 150, 160, 170 },
            ["flowLatencyMs"] = new double?[] { 220, 210, 200 },
            ["errorRate"] = new double?[] { 0.02, 0.02, 0.01 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-buffer"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("svc-buffer");
        var queueBlock = Assert.Single(blocks, block => block.Title.StartsWith("Queue depth", StringComparison.Ordinal));
        Assert.Equal("Queue depth: Open backlog", queueBlock.Title);
    }

    [Fact]
    public void BuildInspectorMetrics_ServiceWithBuffer_ExcludesRetryMetricsWhenAbsent()
    {
        var topology = new Topology();
        topology.TestSetOverlaySettings(new TopologyOverlaySettings
        {
            ShowRetryMetrics = true,
            ShowRetryBudget = true
        });

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-buffer", "serviceWithBuffer", "serviceWithBuffer", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["queue"] = new double?[] { 4, 3, 2 },
            ["latencyMinutes"] = new double?[] { 1.1, 1.0, 0.9 },
            ["served"] = new double?[] { 8, 7, 6 },
            ["successRate"] = new double?[] { 0.9, 0.91, 0.92 },
            ["utilization"] = new double?[] { 0.5, 0.55, 0.6 },
            ["serviceTimeMs"] = new double?[] { 180, 175, 170 },
            ["flowLatencyMs"] = new double?[] { 240, 230, 220 },
            ["errorRate"] = new double?[] { 0.03, 0.02, 0.02 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["svc-buffer"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("svc-buffer");
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Attempts", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Failed retries", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Retry echo", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Exhausted", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Retry budget remaining", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Title.StartsWith("Retry tax", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildInspectorDependencies_RespectsOverlayFilters()
    {
        var topology = new Topology();
        var nodes = new[]
        {
            CreateTopologyNode("producer", "service", "service", Array.Empty<string>(), new[] { "svc" }, 0, 0, 0, 0, false, EmptySemantics()),
            CreateTopologyNode("svc", "service", "service", new[] { "producer" }, Array.Empty<string>(), 1, 0, 100, 120, false, EmptySemantics())
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
            Parallelism: null,
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
            CreateTopologyNode("producer", "service", "service", Array.Empty<string>(), new[] { "svc" }, 0, 0, 0, 0, false, producerSemantics),
            CreateTopologyNode("svc", "service", "service", new[] { "producer" }, Array.Empty<string>(), 1, 0, 0, 0, false, EmptySemantics())
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
                CreateTopologyNode("queue-a", "queue", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()),
                CreateTopologyNode("queue-b", "queue", "queue", Array.Empty<string>(), Array.Empty<string>(), 0, 1, 0, 0, false, EmptySemantics())
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
                CreateTopologyNode(
                    "queue-good", "queue", "queue", Array.Empty<string>(),
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
                        Parallelism: null,
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
                Assert.Equal("Queue latency", block.Title);
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
    public void BuildInspectorMetrics_RouterNode_IncludesArrivalsWhenSeriesPresent()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode(
                    "router-1", "router", "router", Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    0,
                    0,
                    0,
                    false,
                    new TopologyNodeSemantics(
                        Arrivals: "arrivals",
                        Served: "served",
                        Errors: "errors",
                        Attempts: null,
                        Failures: null,
                        ExhaustedFailures: null,
                        RetryEcho: null,
                        RetryBudgetRemaining: null,
                        Queue: null,
                        Capacity: null,
                        Parallelism: null,
                        Series: null,
                        Expression: null,
                        Distribution: null,
                        InlineValues: null,
                        Aliases: null))
            },
            Array.Empty<TopologyEdge>()));

        var sparkline = CreateSparkline(new Dictionary<string, double?[]>
        {
            ["arrivals"] = new double?[] { 12, 15, 10 },
            ["served"] = new double?[] { 11, 14, 9 }
        });

        topology.TestSetNodeSparklines(new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase)
        {
            ["router-1"] = sparkline
        });

        var blocks = topology.TestBuildInspectorMetrics("router-1");

        Assert.Contains(blocks, block => block.Title == "Arrivals" && !block.IsPlaceholder && block.SeriesKey == "arrivals");
        Assert.Contains(blocks, block => block.Title == "Served" && !block.IsPlaceholder && block.SeriesKey == "served");
    }

    [Fact]
    public void BuildInspectorMetrics_PmfNode_IncludesDistribution()
    {
        var topology = new Topology();

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode(
                    "pmf-1", "pmf", "pmf", Array.Empty<string>(),
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
                        Parallelism: null,
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
            new[] { CreateTopologyNode("svc-x", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics()) },
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
            Parallelism: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[] { CreateTopologyNode("svc-retry", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics) },
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
            Parallelism: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null);

        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                CreateTopologyNode("svc-retry", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, semantics)
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

    [Fact]
    public void DispatchScheduleOverlayReflectsGraph()
    {
        var topology = new Topology();
        var nodes = new[]
        {
            CreateTopologyNode(
                "queue-a", "queue", "queue", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                0,
                0,
                0,
                false,
                EmptySemantics(),
                0,
                new GraphDispatchScheduleModel("time-based", 6, 1, "cap_a")),
            CreateTopologyNode(
                "service-node", "service", "service", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                1,
                0,
                0,
                false,
                EmptySemantics()),
            CreateTopologyNode(
                "queue-b", "queue", "queue", Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                2,
                0,
                0,
                false,
                EmptySemantics(),
                0,
                new GraphDispatchScheduleModel("time-based", 4, 0, null))
        };

        topology.TestSetTopologyGraph(new TopologyGraph(nodes, Array.Empty<TopologyEdge>()));
        topology.TestUpdateDispatchEntries();

        var entries = topology.TestGetDispatchEntries();

        Assert.Collection(entries,
            entry =>
            {
                Assert.Equal("queue-a", entry.NodeId);
                Assert.Equal("Every 6 bins (phase 1)", entry.Summary);
                Assert.Equal("cap_a", entry.CapacityLabel);
            },
            entry =>
            {
                Assert.Equal("queue-b", entry.NodeId);
                Assert.Equal("Every 4 bins (phase 0)", entry.Summary);
                Assert.Equal("Unbounded", entry.CapacityLabel);
            });
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
            Parallelism: null,
            Series: null,
            Expression: null,
            Distribution: null,
            InlineValues: null,
            Aliases: null,
            Metadata: null,
            MaxAttempts: null,
            BackoffStrategy: null,
            ExhaustedPolicy: null);

    private static TopologyNode CreateTopologyNode(
        string id,
        string kind,
        string semanticKind,
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> outputs,
        int layer,
        int index,
        double x,
        double y,
        bool isPositionFixed,
        TopologyNodeSemantics semantics,
        int lane = 0,
        GraphDispatchScheduleModel? DispatchSchedule = null,
        string? NodeRole = null)
    {
        return new TopologyNode(id, kind, inputs, outputs, layer, index, x, y, isPositionFixed, semantics, lane, DispatchSchedule, NodeRole)
        {
            Category = ResolveCategory(semanticKind),
            Analytical = CreateAnalytical(semanticKind)
        };
    }

    private static TimeTravelStateWindowDto CreateWindowData(params TimeTravelNodeSeriesDto[] nodes)
    {
        var baseTimestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timestamps = new[]
        {
            baseTimestamp,
            baseTimestamp.AddMinutes(1),
            baseTimestamp.AddMinutes(2)
        };

        return new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-id",
                TemplateId = "template-id",
                Mode = "simulation",
                Schema = new TimeTravelSchemaMetadataDto
                {
                    Id = "time-travel/v1",
                    Version = "1",
                    Hash = "hash"
                },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 2,
                BinCount = 3
            },
            TimestampsUtc = timestamps,
            Nodes = nodes
        };
    }

    private static TimeTravelNodeSeriesDto CreateSeriesNode(
        string id,
        string kind,
        IReadOnlyDictionary<string, double?[]> series,
        IReadOnlyList<TimeTravelSlaSeriesDto>? sla = null,
        TimeTravelDispatchScheduleDto? dispatchSchedule = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double?[]>>? byClass = null,
        string? semanticKind = null)
    {
        return new TimeTravelNodeSeriesDto
        {
            Id = id,
            Kind = kind,
            Category = ResolveCategory(semanticKind ?? kind),
            Analytical = CreateAnalyticalFacts(semanticKind ?? kind),
            Series = series,
            ByClass = byClass ?? new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase),
            Sla = sla,
            DispatchSchedule = dispatchSchedule
        };
    }

    private static string ResolveCategory(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "queue" => "queue",
            "dlq" => "dlq",
            "router" => "router",
            "dependency" => "dependency",
            "sink" => "sink",
            "const" or "constant" or "pmf" => "constant",
            "expr" or "expression" => "expression",
            _ => "service"
        };
    }

    private static GraphNodeAnalyticalModel CreateAnalytical(string kind)
    {
        var normalized = kind.ToLowerInvariant();
        var category = ResolveCategory(kind);
        var hasQueueSemantics = normalized is "queue" or "dlq" or "servicewithbuffer";
        var hasServiceSemantics = category == "service";

        return new GraphNodeAnalyticalModel
        {
            Identity = normalized switch
            {
                "const" => "constant",
                "expr" => "expression",
                _ => kind
            },
            HasQueueSemantics = hasQueueSemantics,
            HasServiceSemantics = hasServiceSemantics,
            HasCycleTimeDecomposition = hasQueueSemantics && hasServiceSemantics,
            StationarityWarningApplicable = hasQueueSemantics
        };
    }

    private static TimeTravelNodeAnalyticalFactsDto CreateAnalyticalFacts(string kind)
    {
        var analytical = CreateAnalytical(kind);
        return new TimeTravelNodeAnalyticalFactsDto
        {
            Identity = analytical.Identity,
            HasQueueSemantics = analytical.HasQueueSemantics,
            HasServiceSemantics = analytical.HasServiceSemantics,
            HasCycleTimeDecomposition = analytical.HasCycleTimeDecomposition,
            StationarityWarningApplicable = analytical.StationarityWarningApplicable
        };
    }

    private static void AssertRowValue(IReadOnlyList<Topology.InspectorBinMetric> rows, string label, string expected)
    {
        Assert.Equal(expected, rows.First(row => row.Label == label).Value);
    }

    private static void AssertRowMissing(IReadOnlyList<Topology.InspectorBinMetric> rows, string label)
    {
        Assert.DoesNotContain(rows, row => row.Label == label);
    }

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
