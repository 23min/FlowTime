using System.Text.Json;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

/// <summary>
/// Tests for SimGridInfo deserialization with new schema (binSize/binUnit).
/// Part of UI-M2.9: Schema Migration for UI
/// </summary>
public class SimGridInfoSchemaTests
{
    [Fact]
    public void SimGridInfo_Deserializes_NewSchema()
    {
        // Arrange: JSON with NEW schema (binSize/binUnit)
        var json = @"{
            ""bins"": 288,
            ""binSize"": 5,
            ""binUnit"": ""minutes"",
            ""timezone"": ""UTC"",
            ""align"": ""left""
        }";

        // Act: Deserialize
        var grid = JsonSerializer.Deserialize<SimGridInfo>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert: New schema fields parsed correctly
        Assert.NotNull(grid);
        Assert.Equal(288, grid.Bins);
        Assert.Equal(5, grid.BinSize);
        Assert.Equal("minutes", grid.BinUnit);
        Assert.Equal("UTC", grid.Timezone);
        Assert.Equal("left", grid.Align);
    }

    [Theory]
    [InlineData("minutes", 5, 5)]      // 5 minutes = 5 minutes
    [InlineData("hours", 1, 60)]       // 1 hour = 60 minutes
    [InlineData("days", 1, 1440)]      // 1 day = 1440 minutes
    [InlineData("weeks", 1, 10080)]    // 1 week = 10080 minutes
    [InlineData("HOURS", 2, 120)]      // Case-insensitive: 2 hours = 120 minutes
    public void SimGridInfo_BinMinutes_ComputesCorrectly_ForAllValidUnits(string unit, int size, int expectedMinutes)
    {
        // Arrange: Create SimGridInfo with specified binSize and binUnit
        var grid = new SimGridInfo
        {
            Bins = 10,
            BinSize = size,
            BinUnit = unit
        };

        // Act: Access computed BinMinutes property
        var actualMinutes = grid.BinMinutes;

        // Assert: Computed value matches expected conversion
        Assert.Equal(expectedMinutes, actualMinutes);
    }

    [Fact]
    public void SimGridInfo_BinMinutes_ThrowsOnInvalidUnit()
    {
        // Arrange: Invalid unit (Engine should have rejected this)
        var grid = new SimGridInfo
        {
            Bins = 10,
            BinSize = 42,
            BinUnit = "fortnights"  // Invalid - not in {minutes, hours, days, weeks}
        };

        // Act & Assert: Should throw ArgumentException for invalid units
        // This indicates a bug in Engine if it reaches UI
        var ex = Assert.Throws<ArgumentException>(() => grid.BinMinutes);
        Assert.Contains("Invalid binUnit", ex.Message);
        Assert.Contains("fortnights", ex.Message);
    }

    [Fact]
    public void SimGridInfo_Serializes_WithoutBinMinutes()
    {
        // Arrange: SimGridInfo with new schema
        var grid = new SimGridInfo
        {
            Bins = 24,
            BinSize = 1,
            BinUnit = "hours",
            Timezone = "UTC",
            Align = "left"
        };

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(grid, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });

        // Assert: binMinutes should NOT appear in JSON (JsonIgnore attribute)
        Assert.DoesNotContain("BinMinutes", json);
        
        // Assert: New schema fields ARE present (PascalCase by default)
        Assert.Contains("\"BinSize\":1", json);
        Assert.Contains("\"BinUnit\":\"hours\"", json);
        Assert.Contains("\"Bins\":24", json);
    }
}
