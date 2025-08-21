namespace FlowTime.Core;

/// <summary>
/// Immutable numeric series aligned to a TimeGrid.
/// </summary>
public sealed class Series
{
    private readonly double[] data;
    public int Length => data.Length;

    public Series(int length)
    {
        data = new double[length];
    }
    public Series(double[] data)
    {
        this.data = data;
    }

    public double this[int t]
    {
        get => data[t];
        set => data[t] = value;
    }

    public double[] ToArray() => (double[])data.Clone();
}
