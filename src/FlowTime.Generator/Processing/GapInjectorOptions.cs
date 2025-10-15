namespace FlowTime.Generator.Processing;

/// <summary>
/// Options controlling how the gap injector handles NaN and sparse data.
/// </summary>
public sealed record GapInjectorOptions(
    bool FillNaNWithZero = false,
    GapHandlingMode MissingValueHandling = GapHandlingMode.Ignore)
{
    public static GapInjectorOptions Default { get; } = new();
}

public enum GapHandlingMode
{
    Ignore = 0,
    WarnOnly = 1,
    FillWithZero = 2
}
