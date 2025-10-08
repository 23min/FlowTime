using System;
using FlowTime.Core;

namespace FlowTime.Core.Models;

public sealed record Window
{
    private int bins;
    private int binSize;
    private TimeUnit binUnit;
    private DateTime? startTime;

    public required int Bins
    {
        get => bins;
        init
        {
            if (value <= 0 || value > 10000)
                throw new ArgumentOutOfRangeException(nameof(Bins), value, "Bins must be between 1 and 10000.");
            bins = value;
        }
    }

    public required int BinSize
    {
        get => binSize;
        init
        {
            if (value <= 0 || value > 1000)
                throw new ArgumentOutOfRangeException(nameof(BinSize), value, "BinSize must be between 1 and 1000.");
            binSize = value;
        }
    }

    public required TimeUnit BinUnit
    {
        get => binUnit;
        init => binUnit = value;
    }

    public DateTime? StartTime
    {
        get => startTime;
        init
        {
            if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("StartTime must be specified in UTC", nameof(StartTime));
            startTime = value;
        }
    }

    public TimeSpan BinDuration => BinUnit switch
    {
        TimeUnit.Minutes => TimeSpan.FromMinutes(BinSize),
        TimeUnit.Hours => TimeSpan.FromHours(BinSize),
        TimeUnit.Days => TimeSpan.FromDays(BinSize),
        TimeUnit.Weeks => TimeSpan.FromDays(BinSize * 7),
        _ => throw new ArgumentOutOfRangeException(nameof(BinUnit), BinUnit, "Unsupported bin unit")
    };

    public DateTime? GetBinStartTime(int binIndex)
    {
        if (!StartTime.HasValue)
            return null;

        if (binIndex < 0 || binIndex >= Bins)
            throw new ArgumentOutOfRangeException(nameof(binIndex), binIndex, "Bin index out of range");

        return StartTime.Value.Add(BinDuration * binIndex);
    }
}
