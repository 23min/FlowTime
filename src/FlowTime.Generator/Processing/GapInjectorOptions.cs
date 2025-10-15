namespace FlowTime.Generator.Processing;

/// <summary>
/// Options controlling how the gap injector handles NaN and sparse data.
/// </summary>
public sealed record GapInjectorOptions(bool FillNaNWithZero = false)
{
    public static GapInjectorOptions Default { get; } = new();
}
