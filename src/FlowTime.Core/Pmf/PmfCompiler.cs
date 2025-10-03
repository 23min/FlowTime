namespace FlowTime.Core;

/// <summary>
/// Compiles PMF specifications into validated Pmf objects and optionally samples series.
/// Implements 3-phase compilation pipeline:
/// 1. Validation - Check probabilities, normalization
/// 2. Grid Alignment - Handle repeat/error policies  
/// 3. Compilation - Sample deterministic series from PMF using RNG (if GridBins specified)
/// </summary>
public static class PmfCompiler
{
    private const double DEFAULT_NORMALIZATION_TOLERANCE = 0.001;

    /// <summary>
    /// Compile a PMF from entries.
    /// Phase 1: Validation only (for now).
    /// </summary>
    /// <param name="entries">PMF entries with values and probabilities</param>
    /// <param name="name">Name/ID for this PMF (for error messages)</param>
    /// <param name="options">Compilation options (optional)</param>
    /// <returns>Compilation result with validated Pmf or error</returns>
    public static PmfCompilationResult Compile(
        PmfEntry[] entries,
        string name,
        PmfCompilerOptions? options = null)
    {
        options ??= new PmfCompilerOptions();
        var warnings = new List<string>();

        try
        {
            // ============================================================
            // PHASE 1: VALIDATION
            // ============================================================

            // Validate input
            if (entries == null || entries.Length == 0)
                return PmfCompilationResult.Failure("PMF must have at least one entry");

            // Check for negative probabilities
            foreach (var entry in entries)
            {
                if (entry.Probability < 0)
                {
                    return PmfCompilationResult.Failure(
                        $"PMF '{name}': Probability must be non-negative, got {entry.Probability} for value {entry.Value}");
                }
            }

            // Check probability sum
            var sum = entries.Sum(e => e.Probability);
            
            if (sum <= 0)
            {
                return PmfCompilationResult.Failure(
                    $"PMF '{name}': Probabilities sum to {sum}, must be positive");
            }

            // Normalization check
            var tolerance = options.NormalizationTolerance;
            double[] probabilities;
            
            if (Math.Abs(sum - 1.0) > tolerance)
            {
                // Renormalize with warning
                warnings.Add($"PMF '{name}': Probabilities sum to {sum:F6}, renormalizing to 1.0");
                probabilities = entries.Select(e => e.Probability / sum).ToArray();
            }
            else
            {
                probabilities = entries.Select(e => e.Probability).ToArray();
            }

            // Extract values
            var values = entries.Select(e => e.Value).ToArray();

            // ============================================================
            // PHASE 2: GRID ALIGNMENT VALIDATION
            // ============================================================

            if (options.GridBins.HasValue)
            {
                var gridBins = options.GridBins.Value;
                var pmfLength = values.Length;

                if (pmfLength == gridBins)
                {
                    // Perfect match - no issues
                }
                else if (options.RepeatPolicy == RepeatPolicy.Repeat)
                {
                    // Check if PMF can tile evenly into grid
                    if (gridBins % pmfLength != 0)
                    {
                        return PmfCompilationResult.Failure(
                            $"PMF '{name}': Cannot repeat {pmfLength} values to fit {gridBins} bins (not evenly divisible)");
                    }

                    // Valid for tiling - record in warnings
                    warnings.Add($"PMF '{name}': Will tile {pmfLength} values to {gridBins} bins ({gridBins / pmfLength}x repeat) during sampling");
                }
                else // RepeatPolicy.Error
                {
                    return PmfCompilationResult.Failure(
                        $"PMF '{name}': Length mismatch - PMF has {pmfLength} values but grid has {gridBins} bins (policy: error)");
                }
            }

            // Create validated Pmf (original distribution, not tiled)
            var pmf = new Pmf.Pmf(values, probabilities);

            // ============================================================
            // PHASE 3: COMPILATION (SAMPLING)
            // ============================================================

            double[]? compiledSeries = null;

            if (options.GridBins.HasValue)
            {
                var gridBins = options.GridBins.Value;
                var rng = new Pcg32(options.Seed);

                compiledSeries = new double[gridBins];

                // Sample from PMF distribution
                for (int i = 0; i < gridBins; i++)
                {
                    compiledSeries[i] = SampleFromPmf(values, probabilities, rng);
                }
            }

            return PmfCompilationResult.Success(pmf, compiledSeries, warnings);
        }
        catch (Exception ex)
        {
            return PmfCompilationResult.Failure($"PMF '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Sample a value from the PMF using inverse transform sampling.
    /// </summary>
    private static double SampleFromPmf(double[] values, double[] probabilities, Pcg32 rng)
    {
        var u = rng.NextDouble();
        var cumulativeProb = 0.0;

        for (int i = 0; i < probabilities.Length; i++)
        {
            cumulativeProb += probabilities[i];
            if (u <= cumulativeProb)
            {
                return values[i];
            }
        }

        // Fallback for rounding errors: return last value
        return values[^1];
    }

    /// <summary>
    /// Validate PMF entries without compiling.
    /// Useful for quick validation checks.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) Validate(PmfEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
            return (false, "PMF must have at least one entry");

        foreach (var entry in entries)
        {
            if (entry.Probability < 0)
                return (false, $"Negative probability: {entry.Probability}");
        }

        var sum = entries.Sum(e => e.Probability);
        if (sum <= 0)
            return (false, $"Probabilities sum to {sum}, must be positive");

        return (true, null);
    }
}
