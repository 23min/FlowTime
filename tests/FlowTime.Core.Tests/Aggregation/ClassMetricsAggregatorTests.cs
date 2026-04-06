using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Aggregation;

public class ClassMetricsAggregatorTests
{
    [Fact]
    public void Aggregate_PerClassCounts_TracksEachClass()
    {
        var data = new NodeData
        {
            NodeId = "service",
            Arrivals = new[] { 10d },
            Served = new[] { 8d },
            Errors = new[] { 2d },
            ByClass = new Dictionary<string, NodeClassData>(StringComparer.OrdinalIgnoreCase)
            {
                ["vip"] = new NodeClassData
                {
                    Arrivals = new[] { 6d },
                    Served = new[] { 5d },
                    Errors = new[] { 1d }
                },
                ["standard"] = new NodeClassData
                {
                    Arrivals = new[] { 4d },
                    Served = new[] { 3d },
                    Errors = new[] { 1d }
                }
            }
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Equal(ClassCoverage.Full, result.Coverage);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.ClassEntries, entry => entry.Kind == ClassEntryKind.Fallback);
        Assert.Equal(6d, result.ClassEntries.Single(entry => entry.ContractKey == "vip").Payload.Arrivals);
        Assert.Equal(5d, result.ClassEntries.Single(entry => entry.ContractKey == "vip").Payload.Served);
        Assert.Equal(1d, result.ClassEntries.Single(entry => entry.ContractKey == "vip").Payload.Errors);
        Assert.Equal(4d, result.ClassEntries.Single(entry => entry.ContractKey == "standard").Payload.Arrivals);
    }

    [Fact]
    public void Aggregate_NoClassData_ProducesNoClassEntries()
    {
        var data = new NodeData
        {
            NodeId = "service",
            Arrivals = new[] { 5d },
            Served = new[] { 4d },
            Errors = new[] { 1d },
            ByClass = null
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Empty(result.ClassEntries);
        Assert.Equal(ClassCoverage.Missing, result.Coverage);
    }

    [Fact]
    public void Aggregate_InconsistentTotals_FlagsWarning()
    {
        var data = new NodeData
        {
            NodeId = "service",
            Arrivals = new[] { 10d },
            Served = new[] { 7d },
            Errors = new[] { 1d },
            ByClass = new Dictionary<string, NodeClassData>(StringComparer.OrdinalIgnoreCase)
            {
                ["vip"] = new NodeClassData
                {
                    Arrivals = new[] { 2d },
                    Served = new[] { 1d },
                    Errors = new[] { 0d }
                },
                ["standard"] = new NodeClassData
                {
                    Arrivals = new[] { 3d },
                    Served = new[] { 2d },
                    Errors = new[] { 1d }
                }
            }
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Equal(ClassCoverage.Partial, result.Coverage);
        Assert.Contains(result.Warnings, w => w.Code == "class_totals_mismatch");
    }

    [Fact]
    public void Aggregate_MixedRealAndFallback_KeepsFallbackSeparateFromRealClasses()
    {
        var data = new NodeData
        {
            NodeId = "service",
            Arrivals = new[] { 10d },
            Served = new[] { 8d },
            Errors = new[] { 2d },
            ClassEntries = new[]
            {
                ClassEntry<NodeClassData>.Specific("vip", new NodeClassData
                {
                    Arrivals = new[] { 10d },
                    Served = new[] { 8d },
                    Errors = new[] { 2d }
                }),
                ClassEntry<NodeClassData>.Fallback(new NodeClassData
                {
                    Arrivals = new[] { 10d },
                    Served = new[] { 8d },
                    Errors = new[] { 2d }
                })
            }
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Equal(ClassCoverage.Full, result.Coverage);
        Assert.Single(result.ClassEntries.Where(entry => entry.Kind == ClassEntryKind.Specific));
        Assert.Contains(result.ClassEntries, entry => entry.Kind == ClassEntryKind.Specific && entry.ContractKey == "vip");
        Assert.Contains(result.ClassEntries, entry => entry.Kind == ClassEntryKind.Fallback);
        Assert.DoesNotContain(result.ClassEntries, entry => string.Equals(entry.ContractKey, "DEFAULT", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Aggregate_OrdersClassKeysDeterministically()
    {
        var data = new NodeData
        {
            NodeId = "service",
            Arrivals = new[] { 3d },
            Served = new[] { 2d },
            Errors = new[] { 1d },
            ByClass = new Dictionary<string, NodeClassData>(StringComparer.OrdinalIgnoreCase)
            {
                ["zeta"] = new NodeClassData { Arrivals = new[] { 1d }, Served = new[] { 1d }, Errors = new[] { 0d } },
                ["alpha"] = new NodeClassData { Arrivals = new[] { 2d }, Served = new[] { 1d }, Errors = new[] { 1d } }
            }
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Equal(new[] { "alpha", "zeta" }, result.ClassEntries.Select(entry => entry.ContractKey));
    }
}
