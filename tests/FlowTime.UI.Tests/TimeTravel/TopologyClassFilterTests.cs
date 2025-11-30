using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyClassFilterTests
{
    [Fact]
    public void UpdateActiveMetrics_DimsNode_WhenSelectedClassHasNoVolume()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = CreateWindow(totalArrivals: 10, totalServed: 8, classArrivals: 10, classServed: 8, classId: "alpha");
        topology.TestSetWindowData(window);
        topology.TestSetClassSelection(new[] { "beta" });

        topology.TestUpdateActiveMetrics(0);

        var dimmed = topology.TestGetClassFilteredNodes();
        Assert.Contains("svc", dimmed);
    }

    [Fact]
    public void UpdateActiveMetrics_UsesSelectedClassMetrics()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var window = CreateWindow(totalArrivals: 10, totalServed: 8, classArrivals: 4, classServed: 2, classId: "alpha");
        topology.TestSetWindowData(window);
        topology.TestSetClassSelection(new[] { "alpha" });

        topology.TestUpdateActiveMetrics(0);

        var metrics = topology.TestGetActiveMetrics();
        var snapshot = Assert.Contains("svc", metrics);
        Assert.True(snapshot.SuccessRate.HasValue);
        Assert.Equal(0.5d, snapshot.SuccessRate.Value, 3);

        var dimmed = topology.TestGetClassFilteredNodes();
        Assert.DoesNotContain("svc", dimmed);
    }

    [Fact]
    public void ClassContributionsExposePerClassMetrics()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var extraClasses = new Dictionary<string, (double Arrivals, double Served, double Errors, double Queue)>
        {
            ["beta"] = (6, 5, 1, 2)
        };
        var window = CreateWindow(16, 13, 10, 8, "alpha", extraClasses);
        topology.TestSetWindowData(window);
        topology.TestSetClassSelection(Array.Empty<string>());

        topology.TestUpdateActiveMetrics(0);

        var contributions = topology.TestGetClassContributions("svc");
        Assert.Equal(2, contributions.Count);
        var alpha = Assert.Single(contributions, c => c.Id == "alpha");
        Assert.Equal(10, alpha.Arrivals);
        Assert.Equal(8, alpha.Served);
        var beta = Assert.Single(contributions, c => c.Id == "beta");
        Assert.Equal(6, beta.Arrivals);
        Assert.Equal(2, beta.Queue);
    }

    [Fact]
    public void BuildFilteredCsv_RespectsSelectedClasses()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var extraClasses = new Dictionary<string, (double Arrivals, double Served, double Errors, double Queue)>
        {
            ["beta"] = (6, 5, 1, 2)
        };
        var window = CreateWindow(totalArrivals: 16, totalServed: 13, classArrivals: 10, classServed: 8, classId: "alpha", extraClasses: extraClasses);
        topology.TestSetWindowData(window);
        topology.TestSetClassSelection(new[] { "alpha" });
        topology.TestUpdateActiveMetrics(0);

        var csv = topology.TestBuildFilteredCsv();
        Assert.Contains("svc,alpha,arrivals,0,10", csv);
        Assert.DoesNotContain("beta", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFilteredCsv_ExportsAllClasses_WhenNoneSelected()
    {
        var topology = new Topology();
        topology.TestSetTopologyGraph(new TopologyGraph(
            new[]
            {
                new TopologyNode("svc", "service", "service", Array.Empty<string>(), Array.Empty<string>(), 0, 0, 0, 0, false, EmptySemantics())
            },
            Array.Empty<TopologyEdge>()));

        var extraClasses = new Dictionary<string, (double Arrivals, double Served, double Errors, double Queue)>
        {
            ["beta"] = (6, 5, 1, 2)
        };
        var window = CreateWindow(totalArrivals: 16, totalServed: 13, classArrivals: 10, classServed: 8, classId: "alpha", extraClasses: extraClasses);
        topology.TestSetWindowData(window);
        topology.TestUpdateActiveMetrics(0);

        var csv = topology.TestBuildFilteredCsv();
        Assert.Contains("svc,alpha,arrivals,0,10", csv);
        Assert.Contains("svc,beta,arrivals,0,6", csv);
    }

    private static TimeTravelStateWindowDto CreateWindow(double totalArrivals, double totalServed, double classArrivals, double classServed, string classId, Dictionary<string, (double Arrivals, double Served, double Errors, double Queue)>? extraClasses = null)
    {
        var byClass = new Dictionary<string, IReadOnlyDictionary<string, double?[]>>(StringComparer.OrdinalIgnoreCase)
        {
            [classId] = new Dictionary<string, double?[]>
            {
                ["arrivals"] = new double?[] { classArrivals },
                ["served"] = new double?[] { classServed },
                ["errors"] = new double?[] { 0d },
                ["queue"] = new double?[] { 0d }
            }
        };

        if (extraClasses is not null)
        {
            foreach (var pair in extraClasses)
            {
                byClass[pair.Key] = new Dictionary<string, double?[]>
                {
                    ["arrivals"] = new double?[] { pair.Value.Arrivals },
                    ["served"] = new double?[] { pair.Value.Served },
                    ["errors"] = new double?[] { pair.Value.Errors },
                    ["queue"] = new double?[] { pair.Value.Queue }
                };
            }
        }

        return new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run",
                TemplateId = "template",
                Mode = "telemetry",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto { Id = "time-travel/v1", Version = "1", Hash = "hash" },
                Storage = new TimeTravelStorageDescriptorDto { ModelPath = "runs/run/model.yaml" },
                ClassCoverage = "full"
            },
            Window = new TimeTravelWindowSliceDto
            {
                StartBin = 0,
                EndBin = 0,
                BinCount = 1
            },
            TimestampsUtc = new[] { DateTimeOffset.Parse("2025-01-01T00:00:00Z") },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "svc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>
                    {
                        ["arrivals"] = new double?[] { totalArrivals },
                        ["served"] = new double?[] { totalServed },
                        ["queue"] = new double?[] { 0d }
                    },
                    ByClass = byClass
                }
            }
        };
    }

    private static TopologyNodeSemantics EmptySemantics() => new(
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
        Aliases: null);
}
