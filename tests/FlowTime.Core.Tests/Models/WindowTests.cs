using System;
using FlowTime.Core.Models;
using FlowTime.Core;
using Xunit;

namespace FlowTime.Core.Tests.Models;

public class WindowTests
{
    [Fact]
    public void GetBinStartTime_WithStartTime_ReturnsCorrectTimestamps()
    {
        var window = new Window
        {
            Bins = 4,
            BinSize = 5,
            BinUnit = TimeUnit.Minutes,
            StartTime = new DateTime(2025, 10, 7, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal(new DateTime(2025, 10, 7, 0, 0, 0, DateTimeKind.Utc), window.GetBinStartTime(0));
        Assert.Equal(new DateTime(2025, 10, 7, 0, 5, 0, DateTimeKind.Utc), window.GetBinStartTime(1));
        Assert.Equal(new DateTime(2025, 10, 7, 0, 15, 0, DateTimeKind.Utc), window.GetBinStartTime(3));
    }

    [Fact]
    public void GetBinStartTime_WithoutStartTime_ReturnsNull()
    {
        var window = new Window
        {
            Bins = 10,
            BinSize = 15,
            BinUnit = TimeUnit.Minutes,
            StartTime = null
        };

        Assert.Null(window.GetBinStartTime(0));
        Assert.Null(window.GetBinStartTime(5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void GetBinStartTime_InvalidIndex_Throws(int index)
    {
        var window = new Window
        {
            Bins = 4,
            BinSize = 5,
            BinUnit = TimeUnit.Minutes,
            StartTime = new DateTime(2025, 10, 7, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = window.GetBinStartTime(index));
    }
}
