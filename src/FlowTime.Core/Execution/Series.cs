namespace FlowTime.Core.Execution;

/// <summary>
/// Immutable numeric series aligned to a TimeGrid.
/// Values are fixed at construction and cannot be modified.
/// Use <see cref="ToArray"/> to get a mutable copy for transformations.
/// </summary>
public sealed class Series
{
    private readonly double[] data;
    public int Length => data.Length;

    public Series(double[] data)
    {
        this.data = (double[])data.Clone();
    }

    public double this[int t] => data[t];

    public double[] ToArray() => (double[])data.Clone();
}
