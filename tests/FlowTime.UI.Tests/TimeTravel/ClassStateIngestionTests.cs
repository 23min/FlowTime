using System;
using System.Collections.Generic;
using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ClassStateIngestionTests
{
    [Fact]
    public void Snapshot_MetadataAndByClass_AreAvailable()
    {
        var snapshot = new TimeTravelStateSnapshotDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-1",
                TemplateId = "order-system",
                Mode = "telemetry",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto { Id = "time-travel/v1", Version = "1", Hash = "sha256:abc" },
                Storage = new TimeTravelStorageDescriptorDto { ModelPath = "runs/run-1/model.yaml" },
                ClassCoverage = "partial"
            },
            Bin = new TimeTravelBinDetailDto { Index = 0, DurationMinutes = 5, StartUtc = DateTimeOffset.Parse("2025-01-01T00:00:00Z"), EndUtc = DateTimeOffset.Parse("2025-01-01T00:05:00Z") },
            Nodes = new[]
            {
                new TimeTravelNodeSnapshotDto
                {
                    Id = "OrderService",
                    Kind = "service",
                    Metrics = new TimeTravelNodeMetricsDto { Arrivals = 10, Served = 8, Errors = 1 },
                    ByClass = new Dictionary<string, TimeTravelClassMetricsDto>
                    {
                        ["Order"] = new TimeTravelClassMetricsDto { Arrivals = 7, Served = 6, Errors = 1, Capacity = 12, ProcessingTimeMsSum = 1200, ServedCount = 6, ServiceTimeMs = 200, CycleTimeMs = 200, FlowEfficiency = 1.0 },
                        ["Refund"] = new TimeTravelClassMetricsDto { Arrivals = 3, Served = 2 }
                    }
                }
            }
        };

        Assert.Equal("partial", snapshot.Metadata.ClassCoverage);
        Assert.True(snapshot.Nodes[0].ByClass.ContainsKey("Order"));
        Assert.Equal(7, snapshot.Nodes[0].ByClass["Order"].Arrivals);
        Assert.Equal(1200, snapshot.Nodes[0].ByClass["Order"].ProcessingTimeMsSum);

        // Analytical fields are available on the class DTO
        Assert.Equal(200, snapshot.Nodes[0].ByClass["Order"].ServiceTimeMs); // 1200/6
        Assert.Null(snapshot.Nodes[0].ByClass["Order"].QueueTimeMs); // no queue in fixture
        Assert.Null(snapshot.Nodes[0].ByClass["Refund"].ServiceTimeMs); // no processing data
    }

    [Fact]
    public void Snapshot_ClassAnalyticalFields_RoundTrip()
    {
        var dto = new TimeTravelClassMetricsDto
        {
            Arrivals = 10,
            Served = 5,
            Queue = 8,
            ProcessingTimeMsSum = 500,
            ServedCount = 10,
            QueueTimeMs = 120_000,
            ServiceTimeMs = 50,
            CycleTimeMs = 120_050,
            FlowEfficiency = 0.000416
        };

        Assert.Equal(120_000, dto.QueueTimeMs);
        Assert.Equal(50, dto.ServiceTimeMs);
        Assert.Equal(120_050, dto.CycleTimeMs);
        Assert.Equal(0.000416, dto.FlowEfficiency);
    }

    [Fact]
    public void Window_ByClassSeries_AreAvailable()
    {
        var window = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-1",
                TemplateId = "order-system",
                Mode = "telemetry",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto { Id = "time-travel/v1", Version = "1", Hash = "sha256:def" },
                Storage = new TimeTravelStorageDescriptorDto { ModelPath = "runs/run-1/model.yaml" },
                ClassCoverage = "full"
            },
            Window = new TimeTravelWindowSliceDto { StartBin = 0, EndBin = 1, BinCount = 2 },
            TimestampsUtc = new[]
            {
                DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2025-01-01T00:05:00Z")
            },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "OrderService",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>
                    {
                        ["arrivals"] = new double?[] { 10, 12 }
                    },
                    ByClass = new Dictionary<string, IReadOnlyDictionary<string, double?[]>>
                    {
                        ["Order"] = new Dictionary<string, double?[]>
                        {
                            ["arrivals"] = new double?[] { 6, 8 },
                            ["served"] = new double?[] { 5, 7 }
                        },
                        ["Refund"] = new Dictionary<string, double?[]>
                        {
                            ["arrivals"] = new double?[] { 4, 4 }
                        }
                    }
                }
            }
        };

        Assert.Equal("full", window.Metadata.ClassCoverage);
        Assert.Equal(2, window.Nodes[0].ByClass.Count);
        Assert.Equal(7, window.Nodes[0].ByClass["Order"]["served"][1]);
    }
}
