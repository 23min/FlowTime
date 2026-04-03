using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Tests for CycleTimeComputer.CheckStationarity — validates whether
/// arrival rates are stable enough for Little's Law to be meaningful.
/// </summary>
public class StationarityTests
{
    private const double DefaultTolerance = 0.25;

    // ── Stationary cases (should NOT flag) ──

    [Fact]
    public void Stationary_ConstantRate_ReturnsFalse()
    {
        double[] arrivals = [10, 10, 10, 10, 10, 10];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void Stationary_SmallVariation_ReturnsFalse()
    {
        // 10% variation — well within 25% tolerance
        double[] arrivals = [10, 11, 10, 9, 10, 11];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void Stationary_ExactlyAtTolerance_ReturnsFalse()
    {
        // First half avg = 9, second half avg = 12
        // divergence = |9-12| / 12 = 3/12 = 0.25 exactly
        // At boundary, should NOT flag (> not >=)
        double[] arrivals = [9, 9, 12, 12];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    // ── Non-stationary cases (should flag) ──

    [Fact]
    public void NonStationary_RampUp_ReturnsTrue()
    {
        // First half avg = 5, second half avg = 20 → divergence = 300%
        double[] arrivals = [4, 6, 18, 22];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void NonStationary_DrainDown_ReturnsTrue()
    {
        // First half avg = 20, second half avg = 5 → divergence = 75%
        double[] arrivals = [18, 22, 4, 6];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void NonStationary_JustOverTolerance_ReturnsTrue()
    {
        // First half avg = 10, second half avg = 13.4
        // divergence = |10-13.4| / 13.4 = 3.4/13.4 ≈ 25.4% > 25%
        double[] arrivals = [10, 10, 13.4, 13.4];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    // ── Edge cases ──

    [Fact]
    public void EdgeCase_SingleBin_ReturnsFalse()
    {
        // Can't split a single bin — not enough data to assess
        double[] arrivals = [10];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_TwoBins_ComparesEach()
    {
        // First half = [10], second half = [20] → divergence = 100%
        double[] arrivals = [10, 20];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_TwoBins_Similar_ReturnsFalse()
    {
        double[] arrivals = [10, 11];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_EmptyArray_ReturnsFalse()
    {
        double[] arrivals = [];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_AllZeros_ReturnsFalse()
    {
        // Both halves average to 0 — no divergence possible
        double[] arrivals = [0, 0, 0, 0];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_FirstHalfZero_SecondHalfNonZero_ReturnsTrue()
    {
        // From zero to activity — clearly non-stationary
        double[] arrivals = [0, 0, 10, 10];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    [Fact]
    public void EdgeCase_OddBinCount_SplitsCorrectly()
    {
        // 5 bins: first half = [10, 10] (avg 10), second half = [10, 10, 10] (avg 10)
        // Floor(5/2) = 2 for first half, remaining 3 for second half
        double[] arrivals = [10, 10, 10, 10, 10];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, DefaultTolerance));
    }

    // ── Custom tolerance ──

    [Fact]
    public void CustomTolerance_TighterThreshold_FlagsSmallChanges()
    {
        // First half avg = 10, second half avg = 11.5 → divergence = 15%
        // With 10% tolerance, this should flag
        double[] arrivals = [10, 10, 11.5, 11.5];
        Assert.True(CycleTimeComputer.CheckNonStationary(arrivals, tolerance: 0.10));
    }

    [Fact]
    public void CustomTolerance_LooserThreshold_IgnoresLargeChanges()
    {
        // First half avg = 10, second half avg = 14 → divergence = 40%
        // With 50% tolerance, this should NOT flag
        double[] arrivals = [10, 10, 14, 14];
        Assert.False(CycleTimeComputer.CheckNonStationary(arrivals, tolerance: 0.50));
    }
}
