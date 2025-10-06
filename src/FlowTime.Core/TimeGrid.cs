namespace FlowTime.Core;

/// <summary>
/// Time unit for bin specifications
/// </summary>
public enum TimeUnit
{
    Minutes,
    Hours,
    Days,
    Weeks
}

/// <summary>
/// Extension methods for TimeUnit conversions
/// </summary>
public static class TimeUnitExtensions
{
    public static int ToMinutes(this TimeUnit unit, int binSize)
    {
        if (binSize <= 0)
            throw new ArgumentException("binSize must be positive", nameof(binSize));

        return unit switch
        {
            TimeUnit.Minutes => binSize,
            TimeUnit.Hours => binSize * 60,
            TimeUnit.Days => binSize * 1440,
            TimeUnit.Weeks => binSize * 10080,
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };
    }

    public static TimeUnit Parse(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("TimeUnit value cannot be empty", nameof(value));

        return value.ToLowerInvariant() switch
        {
            "minutes" => TimeUnit.Minutes,
            "hours" => TimeUnit.Hours,
            "days" => TimeUnit.Days,
            "weeks" => TimeUnit.Weeks,
            _ => throw new ArgumentException($"Invalid TimeUnit value: {value}", nameof(value))
        };
    }
}

/// <summary>
/// Canonical time grid with fixed-size bins.
/// </summary>
public readonly record struct TimeGrid
{
    public int Bins { get; }
    public int BinSize { get; }
    public TimeUnit BinUnit { get; }
    public int BinMinutes { get; }
    public int TotalMinutes => Bins * BinMinutes;
    public int Length => Bins;

    public TimeGrid(int bins, int binSize, TimeUnit binUnit)
    {
        if (bins < 0 || bins > 10000)
            throw new ArgumentException("bins must be between 0 and 10000", nameof(bins));
        if (binSize <= 0 || binSize > 1000)
            throw new ArgumentException("binSize must be between 1 and 1000", nameof(binSize));

        Bins = bins;
        BinSize = binSize;
        BinUnit = binUnit;
        BinMinutes = binUnit.ToMinutes(binSize);
    }
}
