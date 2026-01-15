using System;
using FlowTime.API.Services;
using Xunit;

namespace FlowTime.Api.Tests;

public class DiagnosticsFileWriterTests
{
    [Fact]
    public void HoverRow_ToCsvLine_IncludesExtendedMetrics()
    {
        var timestamp = DateTime.Parse("2025-12-17T12:34:56.789Z", null, System.Globalization.DateTimeStyles.RoundtripKind);

        var row = new HoverDiagnosticsRow(
            timestamp,
            "run123",
            "hash456",
            "sig789",
            10,
            15,
            123.45,
            0.5,
            "manual",
            800,
            600,
            true,
            "operational",
            true,
            81.5,
            "NodeA",
            "NodeB",
            76,
            135,
            true,
            2,
            100,
            90,
            5,
            1,
            12,
            45.6,
            3.8,
            6.9,
            4,
            9,
            3,
            4,
            3,
            6.9,
            6,
            5,
            4,
            3,
            120,
            2,
            1);

        var csv = row.ToCsvLine();
        var columns = csv.Split(',');

        Assert.Equal(42, columns.Length);
        Assert.Equal("4", columns[^13]); // sceneRebuilds
        Assert.Equal("9", columns[^12]); // overlayUpdates
        Assert.Equal("3", columns[^11]); // layoutReads
        Assert.Equal("4", columns[^10]); // pointer INP sample count
        Assert.Equal("3", columns[^9]); // pointer INP avg ms
        Assert.Equal("6.9", columns[^8]); // pointer INP max ms
        Assert.Equal("6", columns[^7]); // edgeCandidatesLast
        Assert.Equal("5", columns[^6]); // edgeCandidatesAverage
        Assert.Equal("4", columns[^5]); // edgeCandidateSamples
        Assert.Equal("3", columns[^4]); // edgeCandidateFallbacks
        Assert.Equal("120", columns[^3]); // edgeGridCellSize
        Assert.Equal("2", columns[^2]); // edgeCacheHits
        Assert.Equal("1", columns[^1]); // edgeCacheMisses
    }
}
