namespace FlowTime.Core;

/// <summary>
/// Grid alignment policy for PMF compilation.
/// Determines how to handle cases where PMF length doesn't match grid.bins.
/// </summary>
public enum RepeatPolicy
{
    /// <summary>
    /// Throw an error if PMF length doesn't match grid.bins.
    /// </summary>
    Error,

    /// <summary>
    /// Repeat (tile) the PMF pattern to match grid.bins.
    /// PMF length must divide evenly into grid.bins.
    /// Example: [A, B, C] with 6 bins → [A, B, C, A, B, C]
    /// </summary>
    Repeat
}
