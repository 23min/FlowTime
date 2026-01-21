using System.Collections.Generic;
using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class MetricProvenanceCatalogTests
{
    public static IEnumerable<object[]> RequiredCatalogEntries => new List<object[]>
    {
        new object[]
        {
            "service",
            new[] { "successRate", "utilization", "serviceTimeMs", "latencyMinutes", "flowLatencyMs", "errorRate" }
        },
        new object[]
        {
            "serviceWithBuffer",
            new[] { "queue", "latencyMinutes", "arrivals", "served", "successRate", "utilization", "serviceTimeMs", "flowLatencyMs", "errorRate" }
        },
        new object[]
        {
            "queue",
            new[] { "queue", "latencyMinutes", "arrivals", "served" }
        },
        new object[]
        {
            "dlq",
            new[] { "queue", "latencyMinutes", "arrivals", "served" }
        },
        new object[]
        {
            "router",
            new[] { "successRate", "utilization", "serviceTimeMs", "flowLatencyMs", "errorRate" }
        },
        new object[]
        {
            "sink",
            new[] { "successRate", "utilization", "serviceTimeMs", "flowLatencyMs", "errorRate" }
        }
    };

    [Theory]
    [MemberData(nameof(RequiredCatalogEntries))]
    public void MetricProvenanceCatalog_KindsHaveRequiredEntries(string nodeKind, string[] requiredMetrics)
    {
        var catalog = MetricProvenanceCatalog.GetForNodeKind(nodeKind);

        foreach (var metric in requiredMetrics)
        {
            Assert.True(catalog.TryGetValue(metric, out var definition), $"Missing provenance definition for {nodeKind}:{metric}.");
            Assert.NotNull(definition);
            Assert.NotEmpty(definition.Formulas);
            Assert.False(string.IsNullOrWhiteSpace(definition.Unit));
            Assert.True(definition.Formulas[0].Inputs.Count > 0);
        }
    }

    [Fact]
    public void MetricProvenance_ReportsMissingInputs()
    {
        var definition = MetricProvenanceCatalog.GetForNodeKind("service")["utilization"];
        var evaluation = definition.Evaluate(new[] { "served" });

        Assert.Contains("capacity", evaluation.MissingInputs);
    }

    [Fact]
    public void MetricProvenanceCatalog_FlowLatencyMentionsDerived()
    {
        var definition = MetricProvenanceCatalog.GetForNodeKind("service")["flowLatencyMs"];

        Assert.NotNull(definition.Meaning);
        Assert.Contains("derived", definition.Meaning!, StringComparison.OrdinalIgnoreCase);
    }
}
