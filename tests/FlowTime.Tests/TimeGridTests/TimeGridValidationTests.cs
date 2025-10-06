using FlowTime.Core;

namespace FlowTime.Tests.TimeGridTests;

/// <summary>
/// Tests for TimeGrid validation with binSize + binUnit format (target schema)
/// Status: GREEN - Tests pass
/// </summary>
public class TimeGridValidationTests
{
    [Fact]
    public void TimeGrid_WithBinSizeAndBinUnit_CreatesCorrectly()
    {
        // Arrange
        int bins = 100;
        int binSize = 5;
        var binUnit = Core.TimeUnit.Hours;
        
        // Act
        var grid = new Core.TimeGrid(bins, binSize, binUnit);
        
        // Assert
        Assert.Equal(bins, grid.Bins);
        Assert.Equal(binSize, grid.BinSize);
        Assert.Equal(binUnit, grid.BinUnit);
        Assert.Equal(300, grid.BinMinutes); // 5 hours = 300 minutes
    }
    
    [Fact]
    public void TimeGrid_InvalidBinSize_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            new Core.TimeGrid(100, 0, Core.TimeUnit.Hours));
        
        Assert.Contains("binSize", ex.Message);
    }
    
    [Fact]
    public void TimeGrid_InvalidBins_ThrowsArgumentException()
    {
        // Act & Assert: bins must be >= 1 (zero and negative are invalid)
        var ex1 = Assert.Throws<ArgumentException>(() => 
            new Core.TimeGrid(0, 5, Core.TimeUnit.Hours));
        Assert.Contains("bins", ex1.Message);
        
        var ex2 = Assert.Throws<ArgumentException>(() => 
            new Core.TimeGrid(-1, 5, Core.TimeUnit.Hours));
        Assert.Contains("bins", ex2.Message);
    }
    
    [Fact]
    public void TimeGrid_BinSizeTooLarge_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            new Core.TimeGrid(100, 1001, Core.TimeUnit.Hours));
        
        Assert.Contains("binSize", ex.Message);
        Assert.Contains("1000", ex.Message);
    }
    
    [Fact]
    public void TimeGrid_BinsTooLarge_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            new Core.TimeGrid(10001, 5, Core.TimeUnit.Hours));
        
        Assert.Contains("bins", ex.Message);
        Assert.Contains("10000", ex.Message);
    }
    
    [Theory]
    [InlineData(24, 1, 1440)]   // 24 bins × 1 hour = 24 hours = 1440 minutes
    [InlineData(7, 1, 420)]     // 7 bins × 1 hour = 7 hours = 420 minutes
    [InlineData(52, 1, 3120)]   // 52 bins × 1 hour = 52 hours = 3120 minutes
    [InlineData(100, 5, 30000)] // 100 bins × 5 hours = 500 hours = 30000 minutes
    public void TimeGrid_TotalDuration_CalculatedCorrectly_Hours(
        int bins, int binSize, int expectedMinutes)
    {
        // Note: Can't use enum in InlineData, so separate test per unit
        var grid = new Core.TimeGrid(bins, binSize, Core.TimeUnit.Hours);
        Assert.Equal(expectedMinutes, grid.TotalMinutes);
    }
    
    [Fact]
    public void TimeGrid_TotalDuration_Days()
    {
        var grid = new Core.TimeGrid(7, 1, Core.TimeUnit.Days);
        Assert.Equal(10080, grid.TotalMinutes); // 7 days = 10080 minutes
    }
    
    [Fact]
    public void TimeGrid_TotalDuration_Weeks()
    {
        var grid = new Core.TimeGrid(52, 1, Core.TimeUnit.Weeks);
        Assert.Equal(524160, grid.TotalMinutes); // 52 weeks
    }
    
    [Fact]
    public void TimeGrid_TotalDuration_Minutes()
    {
        var grid = new Core.TimeGrid(100, 5, Core.TimeUnit.Minutes);
        Assert.Equal(500, grid.TotalMinutes); // 100 × 5 minutes
    }
}
