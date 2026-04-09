namespace FlowTime.Core.Tests.Pmf;

/// <summary>
/// Tests for AC-1 and AC-2 of m-ec-p3c Variability.
/// Verifies Cv computation from PMF distributions and observed series.
/// </summary>
public sealed class CvComputationTests
{
    // -----------------------------------------------------------------------
    // AC-1a: Cv from PMF distribution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Deterministic PMF (single value with probability 1) → Cv = 0.
    /// </summary>
    [Fact]
    public void Pmf_Deterministic_CvIsZero()
    {
        var pmf = new Core.Pmf.Pmf(
            new[] { 5.0 },
            new[] { 1.0 });

        Assert.Equal(5.0, pmf.ExpectedValue, precision: 10);
        Assert.Equal(0.0, pmf.Variance, precision: 10);
        Assert.Equal(0.0, pmf.CoefficientOfVariation, precision: 10);
    }

    /// <summary>
    /// Symmetric two-value PMF: {(0, 0.5), (10, 0.5)}.
    /// μ = 5.0, σ² = 0.5*(0-5)² + 0.5*(10-5)² = 25, σ = 5, Cv = 1.0.
    /// </summary>
    [Fact]
    public void Pmf_SymmetricTwoValue_CvIsOne()
    {
        var pmf = new Core.Pmf.Pmf(
            new[] { 0.0, 10.0 },
            new[] { 0.5, 0.5 });

        Assert.Equal(5.0, pmf.ExpectedValue, precision: 10);
        Assert.Equal(25.0, pmf.Variance, precision: 10);
        Assert.Equal(5.0, pmf.StandardDeviation, precision: 10);
        Assert.Equal(1.0, pmf.CoefficientOfVariation, precision: 10);
    }

    /// <summary>
    /// Known PMF: {(1, 0.2), (2, 0.3), (3, 0.3), (4, 0.2)}.
    /// μ = 0.2*1 + 0.3*2 + 0.3*3 + 0.2*4 = 2.5
    /// σ² = 0.2*(1-2.5)² + 0.3*(2-2.5)² + 0.3*(3-2.5)² + 0.2*(4-2.5)² = 1.05
    /// σ = √1.05 ≈ 1.02470
    /// Cv = σ/μ ≈ 0.40988
    /// </summary>
    [Fact]
    public void Pmf_KnownDistribution_CvMatchesExpected()
    {
        var pmf = new Core.Pmf.Pmf(
            new[] { 1.0, 2.0, 3.0, 4.0 },
            new[] { 0.2, 0.3, 0.3, 0.2 });

        Assert.Equal(2.5, pmf.ExpectedValue, precision: 10);
        Assert.Equal(1.05, pmf.Variance, precision: 10);
        Assert.Equal(Math.Sqrt(1.05), pmf.StandardDeviation, precision: 6);
        Assert.Equal(Math.Sqrt(1.05) / 2.5, pmf.CoefficientOfVariation, precision: 6);
    }

    /// <summary>
    /// PMF with zero expected value → Cv = 0 (no variation around zero).
    /// </summary>
    [Fact]
    public void Pmf_ZeroExpectedValue_CvIsZero()
    {
        var pmf = new Core.Pmf.Pmf(
            new[] { 0.0 },
            new[] { 1.0 });

        Assert.Equal(0.0, pmf.ExpectedValue, precision: 10);
        Assert.Equal(0.0, pmf.CoefficientOfVariation, precision: 10);
    }

    // -----------------------------------------------------------------------
    // AC-1b: Cv from observed series (sample statistics)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constant series → sample Cv = 0.
    /// </summary>
    [Fact]
    public void SampleCv_ConstantSeries_CvIsZero()
    {
        var series = new double[] { 5.0, 5.0, 5.0, 5.0 };
        var cv = CvCalculator.ComputeSampleCv(series);
        Assert.Equal(0.0, cv, precision: 10);
    }

    /// <summary>
    /// Known series with computable sample statistics.
    /// Values: [2, 4, 4, 4, 5, 5, 7, 9]
    /// μ = 5.0, σ² (population) = 4.0, σ = 2.0, Cv = 0.4
    /// </summary>
    [Fact]
    public void SampleCv_KnownSeries_CvMatchesExpected()
    {
        var series = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        var cv = CvCalculator.ComputeSampleCv(series);
        Assert.Equal(0.4, cv, precision: 6);
    }

    /// <summary>
    /// Empty series → Cv = 0 (graceful handling).
    /// </summary>
    [Fact]
    public void SampleCv_EmptySeries_CvIsZero()
    {
        var cv = CvCalculator.ComputeSampleCv(Array.Empty<double>());
        Assert.Equal(0.0, cv, precision: 10);
    }

    /// <summary>
    /// Single-element series → Cv = 0 (no variation with one value).
    /// </summary>
    [Fact]
    public void SampleCv_SingleElement_CvIsZero()
    {
        var cv = CvCalculator.ComputeSampleCv(new[] { 42.0 });
        Assert.Equal(0.0, cv, precision: 10);
    }

    /// <summary>
    /// Series with zero mean → Cv = 0 (no variation around zero).
    /// </summary>
    [Fact]
    public void SampleCv_ZeroMean_CvIsZero()
    {
        var series = new double[] { 0.0, 0.0, 0.0 };
        var cv = CvCalculator.ComputeSampleCv(series);
        Assert.Equal(0.0, cv, precision: 10);
    }
}
