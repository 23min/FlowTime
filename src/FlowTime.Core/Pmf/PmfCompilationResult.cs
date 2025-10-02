namespace FlowTime.Core;

/// <summary>
/// Result of PMF compilation.
/// </summary>
public class PmfCompilationResult
{
    /// <summary>
    /// Whether the compilation succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The compiled PMF object (null if failed).
    /// </summary>
    public Pmf.Pmf? CompiledPmf { get; set; }

    /// <summary>
    /// Error message if compilation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warning messages generated during compilation (e.g., renormalization).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static PmfCompilationResult Success(Pmf.Pmf pmf, List<string>? warnings = null)
    {
        return new PmfCompilationResult
        {
            IsSuccess = true,
            CompiledPmf = pmf,
            Warnings = warnings ?? new List<string>()
        };
    }

    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static PmfCompilationResult Failure(string errorMessage)
    {
        return new PmfCompilationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
