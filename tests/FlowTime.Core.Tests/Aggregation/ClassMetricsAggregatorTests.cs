using System.Collections.Generic;
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
        Assert.Equal(6d, result.ByClass["vip"].Arrivals);
        Assert.Equal(5d, result.ByClass["vip"].Served);
        Assert.Equal(1d, result.ByClass["vip"].Errors);
        Assert.Equal(4d, result.ByClass["standard"].Arrivals);
    }

    [Fact]
    public void Aggregate_NoClassData_DefaultsToWildcard()
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

        var entry = Assert.Single(result.ByClass);
        Assert.Equal("*", entry.Key);
        var entryRecord = Assert.Single(result.ClassEntries);
        Assert.Equal(ClassEntryKind.Fallback, entryRecord.Kind);
        Assert.Equal("*", entryRecord.ContractKey);
        Assert.Equal(5d, entry.Value.Arrivals);
        Assert.Equal(4d, entry.Value.Served);
        Assert.Equal(1d, entry.Value.Errors);
        Assert.Equal(ClassCoverage.Missing, result.Coverage);
    }

    [Fact]
    public void Aggregate_DistinguishesRealClasses_FromFallbackTruth()
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
                    Arrivals = new[] { 4d },
                    Served = new[] { 3d },
                    Errors = new[] { 1d }
                },
                ["standard"] = new NodeClassData
                {
                    Arrivals = new[] { 4d },
                    Served = new[] { 3d },
                    Errors = new[] { 1d }
                },
                ["*"] = new NodeClassData
                {
                    Arrivals = new[] { 10d },
                    Served = new[] { 8d },
                    Errors = new[] { 2d }
                }
            }
        };

        var result = ClassMetricsAggregator.Aggregate(data, binIndex: 0);

        Assert.Equal(ClassCoverage.Partial, result.Coverage);
        Assert.Contains(result.Warnings, warning => warning.Code == "class_totals_mismatch");
        Assert.Equal(new[] { "standard", "vip", "*" }, result.ClassEntries.Select(fact => fact.ContractKey));
        Assert.Equal(
            new[] { ClassEntryKind.Specific, ClassEntryKind.Specific, ClassEntryKind.Fallback },
            result.ClassEntries.Select(fact => fact.Kind));
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

        Assert.Equal(new[] { "alpha", "zeta" }, result.ByClass.Keys);
        Assert.Equal(new[] { "alpha", "zeta" }, result.ClassEntries.Select(fact => fact.ContractKey));
    }
}
