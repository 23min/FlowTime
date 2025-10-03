using FlowTime.Core;

namespace FlowTime.Tests.TimeGridTests;

/// <summary>
/// Tests for TimeUnit enum and conversion logic
/// Status: GREEN - Tests pass
/// </summary>
public class TimeUnitConversionTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    public void ToMinutes_Minutes_ConvertsCorrectly(int binSize, int expectedMinutes)
    {
        // Act
        var minutes = Core.TimeUnit.Minutes.ToMinutes(binSize);
        
        // Assert
        Assert.Equal(expectedMinutes, minutes);
    }
    
    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(24, 1440)]
    public void ToMinutes_Hours_ConvertsCorrectly(int binSize, int expectedMinutes)
    {
        // Act
        var minutes = Core.TimeUnit.Hours.ToMinutes(binSize);
        
        // Assert
        Assert.Equal(expectedMinutes, minutes);
    }
    
    [Theory]
    [InlineData(1, 1440)]
    [InlineData(7, 10080)]
    public void ToMinutes_Days_ConvertsCorrectly(int binSize, int expectedMinutes)
    {
        // Act
        var minutes = Core.TimeUnit.Days.ToMinutes(binSize);
        
        // Assert
        Assert.Equal(expectedMinutes, minutes);
    }
    
    [Theory]
    [InlineData(1, 10080)]
    [InlineData(4, 40320)]
    public void ToMinutes_Weeks_ConvertsCorrectly(int binSize, int expectedMinutes)
    {
        // Act
        var minutes = Core.TimeUnit.Weeks.ToMinutes(binSize);
        
        // Assert
        Assert.Equal(expectedMinutes, minutes);
    }
    
    [Fact]
    public void ToMinutes_ZeroBinSize_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            Core.TimeUnit.Hours.ToMinutes(0));
        
        Assert.Contains("binSize", ex.Message);
    }
    
    [Fact]
    public void ToMinutes_NegativeBinSize_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            Core.TimeUnit.Days.ToMinutes(-5));
        
        Assert.Contains("binSize", ex.Message);
    }
    
    [Theory]
    [InlineData("minutes")]
    [InlineData("Minutes")]
    [InlineData("MINUTES")]
    public void Parse_Minutes_CaseInsensitive(string input)
    {
        var result = Core.TimeUnitExtensions.Parse(input);
        Assert.Equal(Core.TimeUnit.Minutes, result);
    }
    
    [Theory]
    [InlineData("hours")]
    [InlineData("Hours")]
    [InlineData("HOURS")]
    public void Parse_Hours_CaseInsensitive(string input)
    {
        var result = Core.TimeUnitExtensions.Parse(input);
        Assert.Equal(Core.TimeUnit.Hours, result);
    }
    
    [Theory]
    [InlineData("days")]
    [InlineData("Days")]
    [InlineData("DAYS")]
    public void Parse_Days_CaseInsensitive(string input)
    {
        var result = Core.TimeUnitExtensions.Parse(input);
        Assert.Equal(Core.TimeUnit.Days, result);
    }
    
    [Theory]
    [InlineData("weeks")]
    [InlineData("Weeks")]
    [InlineData("WEEKS")]
    public void Parse_Weeks_CaseInsensitive(string input)
    {
        var result = Core.TimeUnitExtensions.Parse(input);
        Assert.Equal(Core.TimeUnit.Weeks, result);
    }
    
    [Fact]
    public void Parse_InvalidString_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            Core.TimeUnitExtensions.Parse("invalid"));
        
        Assert.Contains("invalid", ex.Message);
        Assert.Contains("TimeUnit", ex.Message);
    }
    
    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            Core.TimeUnitExtensions.Parse(""));
    }
    
    [Fact]
    public void Parse_NullString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            Core.TimeUnitExtensions.Parse(null!));
    }
    
    [Fact]
    public void ToString_ReturnsLowercaseNames()
    {
        Assert.Equal("minutes", Core.TimeUnit.Minutes.ToString().ToLowerInvariant());
        Assert.Equal("hours", Core.TimeUnit.Hours.ToString().ToLowerInvariant());
        Assert.Equal("days", Core.TimeUnit.Days.ToString().ToLowerInvariant());
        Assert.Equal("weeks", Core.TimeUnit.Weeks.ToString().ToLowerInvariant());
    }
    
    [Fact]
    public void AllTimeUnits_ToMinutes_Positive()
    {
        // Arrange
        var allUnits = new[] 
        { 
            Core.TimeUnit.Minutes, 
            Core.TimeUnit.Hours, 
            Core.TimeUnit.Days, 
            Core.TimeUnit.Weeks 
        };
        
        // Act & Assert
        foreach (var unit in allUnits)
        {
            var minutes = unit.ToMinutes(1);
            Assert.True(minutes > 0, $"{unit} should convert to positive minutes");
        }
    }
    
    [Fact]
    public void TimeUnits_Ordering_IsCorrect()
    {
        // Verify that conversion maintains ordering
        var minute = Core.TimeUnit.Minutes.ToMinutes(1);
        var hour = Core.TimeUnit.Hours.ToMinutes(1);
        var day = Core.TimeUnit.Days.ToMinutes(1);
        var week = Core.TimeUnit.Weeks.ToMinutes(1);
        
        Assert.True(minute < hour);
        Assert.True(hour < day);
        Assert.True(day < week);
    }
}
