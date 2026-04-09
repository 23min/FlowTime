namespace FlowTime.Core;

/// <summary>
/// Kingman's formula (G/G/1 approximation) for predicted queue waiting time.
///
///   E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]
///
/// Where:
///   ρ  = utilization (served / capacity), must be in (0, 1)
///   Ca = coefficient of variation of the arrivals process
///   Cs = coefficient of variation of the service process
///   E[S] = mean service time
///
/// This is a diagnostic, not a prediction — it assumes steady-state M/G/1
/// queueing. Real systems are transient. The value is useful as a reference
/// point: "queueing theory predicts X ms wait; actual is Y ms."
/// </summary>
public static class KingmanApproximation
{
    /// <summary>
    /// Compute Kingman's approximation for predicted queue waiting time.
    /// Returns null when any input is invalid or when ρ ≥ 1.0 (formula diverges).
    /// </summary>
    /// <param name="utilization">ρ = served / capacity. Must be in (0, 1).</param>
    /// <param name="cvArrivals">Ca = coefficient of variation of arrivals.</param>
    /// <param name="cvService">Cs = coefficient of variation of service.</param>
    /// <param name="meanServiceTimeMs">E[S] = mean service time in milliseconds.</param>
    /// <returns>Predicted waiting time in milliseconds, or null if inputs are invalid.</returns>
    public static double? Compute(double utilization, double cvArrivals, double cvService, double meanServiceTimeMs)
    {
        // Guard: utilization must be in (0, 1) — at or above 1.0 the queue grows unbounded
        if (utilization <= 0.0 || utilization >= 1.0 || !double.IsFinite(utilization))
            return null;

        // Guard: Cv values must be non-negative and finite
        if (cvArrivals < 0.0 || !double.IsFinite(cvArrivals))
            return null;
        if (cvService < 0.0 || !double.IsFinite(cvService))
            return null;

        // Guard: service time must be positive and finite
        if (meanServiceTimeMs <= 0.0 || !double.IsFinite(meanServiceTimeMs))
            return null;

        var rhoFactor = utilization / (1.0 - utilization);
        var variabilityFactor = (cvArrivals * cvArrivals + cvService * cvService) / 2.0;
        var result = rhoFactor * variabilityFactor * meanServiceTimeMs;

        return double.IsFinite(result) ? result : null;
    }
}
