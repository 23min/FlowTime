namespace FlowTime.Core.Tests.Pmf;

/// <summary>
/// Tests for AC-3 of m-ec-p3c: Kingman's approximation for predicted queue waiting time.
/// E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]
/// </summary>
public sealed class KingmanApproximationTests
{
    /// <summary>
    /// Known inputs: ρ=0.8, Ca=1.0, Cs=0.5, E[S]=10ms.
    /// E[Wq] = (0.8/0.2) × ((1.0 + 0.25)/2) × 10 = 4 × 0.625 × 10 = 25.0 ms
    /// </summary>
    [Fact]
    public void Kingman_KnownInputs_CorrectPrediction()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.8,
            cvArrivals: 1.0,
            cvService: 0.5,
            meanServiceTimeMs: 10.0);

        Assert.NotNull(result);
        Assert.Equal(25.0, result!.Value, precision: 6);
    }

    /// <summary>
    /// Deterministic arrivals and service (Cv=0 for both): predicted wait = 0.
    /// E[Wq] = (ρ/(1-ρ)) × ((0 + 0)/2) × E[S] = 0
    /// </summary>
    [Fact]
    public void Kingman_DeterministicBoth_ZeroWait()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.8,
            cvArrivals: 0.0,
            cvService: 0.0,
            meanServiceTimeMs: 10.0);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value, precision: 10);
    }

    /// <summary>
    /// M/M/1 case: exponential arrivals and service (Ca=Cs=1).
    /// E[Wq] = (ρ/(1-ρ)) × ((1+1)/2) × E[S] = (ρ/(1-ρ)) × E[S]
    /// At ρ=0.5, E[S]=20: E[Wq] = (0.5/0.5) × 20 = 20.0 ms
    /// </summary>
    [Fact]
    public void Kingman_MM1Case_MatchesExact()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.5,
            cvArrivals: 1.0,
            cvService: 1.0,
            meanServiceTimeMs: 20.0);

        Assert.NotNull(result);
        Assert.Equal(20.0, result!.Value, precision: 6);
    }

    /// <summary>
    /// ρ ≥ 1.0 → null (queue grows unbounded, formula diverges).
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Kingman_UtilizationAtOrAboveOne_ReturnsNull(double rho)
    {
        var result = KingmanApproximation.Compute(
            utilization: rho,
            cvArrivals: 1.0,
            cvService: 1.0,
            meanServiceTimeMs: 10.0);

        Assert.Null(result);
    }

    /// <summary>
    /// ρ ≤ 0 → null (no load, formula not meaningful).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    public void Kingman_ZeroOrNegativeUtilization_ReturnsNull(double rho)
    {
        var result = KingmanApproximation.Compute(
            utilization: rho,
            cvArrivals: 1.0,
            cvService: 1.0,
            meanServiceTimeMs: 10.0);

        Assert.Null(result);
    }

    /// <summary>
    /// NaN or negative Cv → null (invalid input).
    /// </summary>
    [Fact]
    public void Kingman_NegativeCv_ReturnsNull()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.5,
            cvArrivals: -1.0,
            cvService: 1.0,
            meanServiceTimeMs: 10.0);

        Assert.Null(result);
    }

    /// <summary>
    /// Zero service time → null (no meaningful wait prediction).
    /// </summary>
    [Fact]
    public void Kingman_ZeroServiceTime_ReturnsNull()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.5,
            cvArrivals: 1.0,
            cvService: 1.0,
            meanServiceTimeMs: 0.0);

        Assert.Null(result);
    }

    /// <summary>
    /// High variability arrivals (Ca=2.0) with steady service (Cs=0.2)
    /// at moderate utilization. Shows bursty arrivals dominate wait time.
    /// ρ=0.7, E[S]=15ms
    /// E[Wq] = (0.7/0.3) × ((4.0 + 0.04)/2) × 15 = 2.333 × 2.02 × 15 ≈ 70.7 ms
    /// </summary>
    [Fact]
    public void Kingman_HighVariabilityArrivals_LargerWait()
    {
        var result = KingmanApproximation.Compute(
            utilization: 0.7,
            cvArrivals: 2.0,
            cvService: 0.2,
            meanServiceTimeMs: 15.0);

        Assert.NotNull(result);
        var expected = (0.7 / 0.3) * ((4.0 + 0.04) / 2.0) * 15.0;
        Assert.Equal(expected, result!.Value, precision: 6);
    }
}
