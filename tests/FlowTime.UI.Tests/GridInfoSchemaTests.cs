using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

/// <summary>
/// Tests for GridInfo deserialization with new schema (binSize/binUnit).
/// Part of UI-M2.9: Schema Migration for UI - Task 1.2
/// </summary>
public class GridInfoSchemaTests
{
    [Fact]
    public void GridInfo_Deserializes_NewSchema()
    {
        // Arrange: JSON with NEW schema (binSize/binUnit) from Engine API
        var json = @"{
            ""bins"": 8,
            ""binSize"": 1,
            ""binUnit"": ""hours""
        }";

        // Act: Deserialize
        var grid = JsonSerializer.Deserialize<GridInfo>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert: New schema fields parsed correctly
        Assert.NotNull(grid);
        Assert.Equal(8, grid.Bins);
        Assert.Equal(1, grid.BinSize);
        Assert.Equal("hours", grid.BinUnit);
    }

    [Theory]
    [InlineData("minutes", 5, 5)]      // 5 minutes = 5 minutes
    [InlineData("hours", 1, 60)]       // 1 hour = 60 minutes
    [InlineData("days", 1, 1440)]      // 1 day = 1440 minutes
    [InlineData("weeks", 2, 20160)]    // 2 weeks = 20160 minutes
    [InlineData("HOURS", 3, 180)]      // Case-insensitive: 3 hours = 180 minutes
    public void GridInfo_BinMinutes_ComputesCorrectly_ForAllValidUnits(string unit, int size, int expectedMinutes)
    {
        // Arrange: Create GridInfo with new schema
        var grid = new GridInfo(
            Bins: 24,
            BinSize: size,
            BinUnit: unit
        );

        // Act: Access computed BinMinutes property
        var actualMinutes = grid.BinMinutes;

        // Assert: Computed value matches expected conversion
        Assert.Equal(expectedMinutes, actualMinutes);
    }

    [Fact]
    public void GridInfo_BinMinutes_ThrowsOnInvalidUnit()
    {
        // Arrange: Invalid unit (Engine should have rejected this)
        var grid = new GridInfo(
            Bins: 24,
            BinSize: 10,
            BinUnit: "parsecs"  // Invalid - not in {minutes, hours, days, weeks}
        );

        // Act & Assert: Should throw ArgumentException for invalid units
        // This indicates a bug in Engine if it reaches UI
        var ex = Assert.Throws<ArgumentException>(() => grid.BinMinutes);
        Assert.Contains("Invalid binUnit", ex.Message);
        Assert.Contains("parsecs", ex.Message);
    }

    [Fact]
    public void GridInfo_Serializes_WithoutBinMinutes()
    {
        // Arrange: GridInfo with new schema
        var grid = new GridInfo(
            Bins: 24,
            BinSize: 1,
            BinUnit: "hours"
        );

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(grid, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });

        // Assert: binMinutes should NOT appear in JSON (JsonIgnore attribute)
        Assert.DoesNotContain("BinMinutes", json);
        Assert.DoesNotContain("binMinutes", json, StringComparison.OrdinalIgnoreCase);
        
        // Assert: New schema fields ARE present
        Assert.Contains("\"bins\":", json.ToLower());
        Assert.Contains("\"binsize\":", json.ToLower());
        Assert.Contains("\"binunit\":", json.ToLower());
    }

    [Fact]
    public void GridInfo_IsRecord_SupportsEqualityComparison()
    {
        // Arrange: Two GridInfo instances with same values
        var grid1 = new GridInfo(Bins: 24, BinSize: 1, BinUnit: "hours");
        var grid2 = new GridInfo(Bins: 24, BinSize: 1, BinUnit: "hours");
        var grid3 = new GridInfo(Bins: 24, BinSize: 2, BinUnit: "hours");

        // Assert: Records with same values are equal
        Assert.Equal(grid1, grid2);
        Assert.NotEqual(grid1, grid3);
    }

    [Fact]
    public void GridInfo_DeserializesFromEngineResponse_RealWorldScenario()
    {
        // Arrange: Realistic Engine /v1/run response snippet
        var engineResponse = @"{
            ""grid"": {
                ""bins"": 288,
                ""binSize"": 5,
                ""binUnit"": ""minutes""
            },
            ""order"": [""demand"", ""served""],
            ""series"": {},
            ""runId"": ""run_20241006T120000Z_abc123""
        }";

        // Act: Deserialize RunResponse
        var response = JsonSerializer.Deserialize<RunResponse>(engineResponse, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert: Grid parsed correctly from Engine
        Assert.NotNull(response);
        Assert.NotNull(response.Grid);
        Assert.Equal(288, response.Grid.Bins);
        Assert.Equal(5, response.Grid.BinSize);
        Assert.Equal("minutes", response.Grid.BinUnit);
        Assert.Equal(5, response.Grid.BinMinutes);  // Computed correctly
    }
}
