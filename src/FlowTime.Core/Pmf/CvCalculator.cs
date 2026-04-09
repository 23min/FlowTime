namespace FlowTime.Core;

/// <summary>
/// Computes the coefficient of variation (Cv = σ/μ) from observed series values.
/// Uses population statistics (not sample — consistent with the PMF Cv definition).
/// </summary>
public static class CvCalculator
{
    /// <summary>
    /// Compute the population coefficient of variation from an observed series.
    /// Returns 0 when the series is empty, has a single element, or has zero mean.
    /// </summary>
    public static double ComputeSampleCv(double[] values)
    {
        if (values is null || values.Length <= 1)
            return 0.0;

        var n = values.Length;
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += values[i];
        }

        var mean = sum / n;
        if (mean == 0.0)
            return 0.0;

        double varianceSum = 0;
        for (int i = 0; i < n; i++)
        {
            var diff = values[i] - mean;
            varianceSum += diff * diff;
        }

        var variance = varianceSum / n; // population variance
        var stdDev = Math.Sqrt(variance);

        return stdDev / Math.Abs(mean);
    }
}
