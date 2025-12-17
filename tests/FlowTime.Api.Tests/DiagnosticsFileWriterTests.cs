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
            6.9);

        var csv = row.ToCsvLine();
        var columns = csv.Split(',');

        Assert.Equal(35, columns.Length);
        Assert.Equal("4", columns[^6]); // sceneRebuilds
        Assert.Equal("9", columns[^5]); // overlayUpdates
        Assert.Equal("3", columns[^4]); // layoutReads
        Assert.Equal("4", columns[^3]); // pointer INP sample count
        Assert.Equal("3", columns[^2]); // pointer INP avg ms
        Assert.Equal("6.9", columns[^1]); // pointer INP max ms
    }
}
