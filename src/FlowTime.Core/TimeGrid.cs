namespace FlowTime.Core;

/// <summary>
/// Canonical time grid with fixed-size bins in minutes.
/// </summary>
public readonly record struct TimeGrid(int Bins, int BinMinutes)
{
    public int Length => Bins;
}
