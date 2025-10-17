using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

public class SimResultDataSchemaTests
{
    [Fact]
    public void Constructor_ShouldAcceptNewSchema()
    {
        // Arrange & Act
        var result = new SimResultData(
            bins: 8,
            binSize: 1,
            binUnit: "hours",
            order: new[] { "demand", "served" },
            series: new Dictionary<string, double[]>
            {
                ["demand"] = new[] { 10.0, 15.0, 20.0, 25.0, 18.0, 12.0, 8.0, 6.0 },
                ["served"] = new[] { 9.0, 14.0, 19.0, 24.0, 17.0, 11.0, 7.0, 5.0 }
            }
        );

        // Assert
        Assert.Equal(8, result.Bins);
        Assert.Equal(1, result.BinSize);
        Assert.Equal("hours", result.BinUnit);
        Assert.Equal(new[] { "demand", "served" }, result.Order);
        Assert.Equal(2, result.Series.Count);
    }

    [Theory]
    [InlineData(60, "minutes", 60)]
    [InlineData(1, "hours", 60)]
    [InlineData(2, "hours", 120)]
    [InlineData(1, "days", 1440)]
    [InlineData(1, "weeks", 10080)]
    public void BinMinutes_ShouldConvertFromBinSize(int binSize, string binUnit, int expectedMinutes)
    {
        // Arrange
        var result = new SimResultData(
            bins: 8,
            binSize: binSize,
            binUnit: binUnit,
            order: new[] { "test" },
            series: new Dictionary<string, double[]> { ["test"] = new[] { 1.0 } }
        );

        // Act & Assert
        Assert.Equal(expectedMinutes, result.BinMinutes);
    }

    [Fact]
    public void BinMinutes_ShouldBeCaseInsensitive()
    {
        // Arrange
        var result = new SimResultData(
            bins: 8,
            binSize: 2,
            binUnit: "HOURS",
            order: new[] { "test" },
            series: new Dictionary<string, double[]> { ["test"] = new[] { 1.0 } }
        );

        // Act & Assert
        Assert.Equal(120, result.BinMinutes); // 2 hours = 120 minutes
    }

    [Fact]
    public void BinMinutes_ShouldThrowOnInvalidUnit()
    {
        // Arrange
        var result = new SimResultData(
            bins: 8,
            binSize: 60,
            binUnit: "seconds",
            order: new[] { "test" },
            series: new Dictionary<string, double[]> { ["test"] = new[] { 1.0 } }
        );

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => result.BinMinutes);
        Assert.Contains("Invalid binUnit", ex.Message);
        Assert.Contains("seconds", ex.Message);
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        var result = new SimResultData(
            bins: 8,
            binSize: 60,
            binUnit: "minutes",
            order: new[] { "demand" },
            series: new Dictionary<string, double[]> { ["demand"] = new[] { 10.0 } }
        );

        // Assert - Check that properties are read-only (getter-only)
        Assert.Equal(8, result.Bins);
        Assert.Equal(60, result.BinSize);
        Assert.Equal("minutes", result.BinUnit);
        Assert.Single(result.Order);
        Assert.Single(result.Series);
    }

    [Fact]
    public void Constructor_ShouldPreserveAllSemanticInformation()
    {
        // Arrange
        var order = new[] { "series1", "series2", "series3" };
        var series = new Dictionary<string, double[]>
        {
            ["series1"] = new[] { 1.0, 2.0, 3.0 },
            ["series2"] = new[] { 4.0, 5.0, 6.0 },
            ["series3"] = new[] { 7.0, 8.0, 9.0 }
        };

        // Act
        var result = new SimResultData(
            bins: 3,
            binSize: 2,
            binUnit: "hours",
            order: order,
            series: series
        );

        // Assert
        Assert.Equal(3, result.Bins);
        Assert.Equal(2, result.BinSize);
        Assert.Equal("hours", result.BinUnit);
        Assert.Equal(120, result.BinMinutes); // Computed: 2 hours = 120 minutes
        Assert.Same(order, result.Order);
        Assert.Same(series, result.Series);
    }
}
