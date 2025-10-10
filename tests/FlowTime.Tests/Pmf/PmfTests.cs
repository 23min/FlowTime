using FlowTime.Core.Pmf;
using CorePmf = FlowTime.Core.Pmf;

namespace FlowTime.Tests.Pmf;

public class PmfTests
{
    [Fact]
    public void Constructor_ValidProbabilities_CreatesNormalizedPmf()
    {
        // Arrange
        var distribution = new Dictionary<double, double>
        {
            { 10, 0.2 },
            { 20, 0.3 },
            { 30, 0.5 }
        };

        // Act
        var pmf = new CorePmf.Pmf(distribution);

        // Assert
        Assert.Equal(distribution, pmf.Distribution);
        Assert.Equal(23.0, pmf.ExpectedValue); // 10*0.2 + 20*0.3 + 30*0.5 = 23
    }

    [Fact]
    public void Constructor_UnnormalizedProbabilities_AutomaticallyNormalizes()
    {
        // Arrange
        var distribution = new Dictionary<double, double>
        {
            { 1, 2 }, { 2, 4 }, { 3, 6 } // Sum = 12
        };

        // Act
        var pmf = new CorePmf.Pmf(distribution);

        // Assert
        var expectedDistribution = new Dictionary<double, double>
        {
            { 1, 1.0/6 }, { 2, 1.0/3 }, { 3, 0.5 }
        };
        Assert.Equal(expectedDistribution, pmf.Distribution);
        Assert.Equal(2.333333333333333, pmf.ExpectedValue, precision: 10); // 1*(1/6) + 2*(1/3) + 3*(1/2) = 14/6
    }

    [Fact]
    public void Constructor_EmptyDistribution_ThrowsArgumentException()
    {
        // Arrange
        var distribution = new Dictionary<double, double>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CorePmf.Pmf(distribution));
        Assert.Contains("at least one", ex.Message);
    }

    [Fact]
    public void Constructor_NegativeProbabilities_ThrowsArgumentException()
    {
        // Arrange
        var distribution = new Dictionary<double, double>
        {
            { 1, 0.5 }, { 2, -0.3 } // Negative probability
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CorePmf.Pmf(distribution));
        Assert.Contains("non-negative", ex.Message);
    }

    [Fact]
    public void Constructor_ZeroProbabilitySum_ThrowsArgumentException()
    {
        // Arrange
        var distribution = new Dictionary<double, double>
        {
            { 1, 0.0 }, { 2, 0.0 } // Sum = 0
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CorePmf.Pmf(distribution));
        Assert.Contains("sum to zero", ex.Message);
    }

    [Fact]
    public void ExpectedValue_SingleValue_ReturnsValueRegardlessOfProbability()
    {
        // Arrange
        var distribution = new Dictionary<double, double> { { 42, 1.0 } };

        // Act
        var pmf = new CorePmf.Pmf(distribution);

        // Assert
        Assert.Equal(42.0, pmf.ExpectedValue);
    }

    [Fact]
    public void ExpectedValue_ComplexDistribution_CalculatesCorrectly()
    {
        // Arrange: Distribution with realistic values
        var distribution = new Dictionary<double, double>
        {
            { 0, 0.1 }, { 10, 0.4 }, { 50, 0.3 }, { 100, 0.15 }, { 200, 0.05 }
        };

        // Act
        var pmf = new CorePmf.Pmf(distribution);

        // Assert
        var expected = 0*0.1 + 10*0.4 + 50*0.3 + 100*0.15 + 200*0.05; // = 44
        Assert.Equal(expected, pmf.ExpectedValue);
    }

    [Fact]
    public void Equals_SameDistribution_ReturnsTrue()
    {
        // Arrange
        var distribution = new Dictionary<double, double>
        {
            { 1, 0.3 }, { 2, 0.4 }, { 3, 0.3 }
        };
        var pmf1 = new CorePmf.Pmf(distribution);
        var pmf2 = new CorePmf.Pmf(distribution);

        // Act & Assert
        Assert.True(pmf1.Equals(pmf2));
        Assert.True(pmf2.Equals(pmf1));
        Assert.Equal(pmf1.GetHashCode(), pmf2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var pmf1 = new CorePmf.Pmf(new Dictionary<double, double> { { 1, 0.5 }, { 2, 0.5 } });
        var pmf2 = new CorePmf.Pmf(new Dictionary<double, double> { { 1, 0.5 }, { 3, 0.5 } });

        // Act & Assert
        Assert.False(pmf1.Equals(pmf2));
    }

    [Fact]
    public void Equals_DifferentProbabilities_ReturnsFalse()
    {
        // Arrange
        var pmf1 = new CorePmf.Pmf(new Dictionary<double, double> { { 1, 0.3 }, { 2, 0.7 } });
        var pmf2 = new CorePmf.Pmf(new Dictionary<double, double> { { 1, 0.4 }, { 2, 0.6 } });

        // Act & Assert
        Assert.False(pmf1.Equals(pmf2));
    }

    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        // Arrange
        var pmf = new CorePmf.Pmf(new Dictionary<double, double> { { 10, 0.3 }, { 20, 0.7 } });

        // Act
        var result = pmf.ToString();

        // Assert
        Assert.Contains("Pmf", result);
        Assert.Contains("10", result);
        Assert.Contains("20", result);
        Assert.Contains("0.3", result);
        Assert.Contains("0.7", result);
    }
}
