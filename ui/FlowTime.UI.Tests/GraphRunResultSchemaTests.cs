using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

/// <summary>
/// Tests for GraphRunResult schema migration (binSize/binUnit).
/// Part of UI-M2.9: Schema Migration for UI - Task 1.3
/// </summary>
public class GraphRunResultSchemaTests
{
    [Fact]
    public void GraphRunResult_StoresSemanticGridInformation()
    {
        // Arrange: Create GraphRunResult with new schema
        var result = new GraphRunResult(
            Bins: 24,
            BinSize: 1,
            BinUnit: "hours",
            Order: new[] { "demand", "served" },
            Series: new Dictionary<string, double[]>
            {
                ["demand"] = new[] { 10.0, 20.0, 30.0 },
                ["served"] = new[] { 8.0, 18.0, 28.0 }
            },
            RunId: "run_20241006T120000Z_abc123"
        );

        // Assert: All semantic information preserved
        Assert.Equal(24, result.Bins);
        Assert.Equal(1, result.BinSize);
        Assert.Equal("hours", result.BinUnit);
        Assert.Equal(new[] { "demand", "served" }, result.Order);
        Assert.Equal(2, result.Series.Count);
        Assert.Equal("run_20241006T120000Z_abc123", result.RunId);
    }

    [Theory]
    [InlineData("minutes", 5, 5)]      // 5 minutes = 5 minutes
    [InlineData("hours", 1, 60)]       // 1 hour = 60 minutes
    [InlineData("days", 1, 1440)]      // 1 day = 1440 minutes
    [InlineData("weeks", 1, 10080)]    // 1 week = 10080 minutes
    public void GraphRunResult_BinMinutes_ComputesCorrectly(string unit, int size, int expectedMinutes)
    {
        // Arrange: Create GraphRunResult with specified binSize and binUnit
        var result = new GraphRunResult(
            Bins: 24,
            BinSize: size,
            BinUnit: unit,
            Order: Array.Empty<string>(),
            Series: new Dictionary<string, double[]>()
        );

        // Act: Access computed BinMinutes property
        var actualMinutes = result.BinMinutes;

        // Assert: Computed value matches expected conversion
        Assert.Equal(expectedMinutes, actualMinutes);
    }

    [Fact]
    public void GraphRunResult_BinMinutes_ThrowsOnInvalidUnit()
    {
        // Arrange: Invalid unit (Engine should have rejected this)
        var result = new GraphRunResult(
            Bins: 24,
            BinSize: 10,
            BinUnit: "parsecs",  // Invalid
            Order: Array.Empty<string>(),
            Series: new Dictionary<string, double[]>()
        );

        // Act & Assert: Should throw ArgumentException
        var ex = Assert.Throws<ArgumentException>(() => result.BinMinutes);
        Assert.Contains("Invalid binUnit", ex.Message);
        Assert.Contains("parsecs", ex.Message);
    }

    [Fact]
    public void GraphRunResult_IsRecord_SupportsValueEquality()
    {
        // Arrange: Two GraphRunResult instances with same values
        var order = new[] { "demand", "served" };
        var series = new Dictionary<string, double[]>
        {
            ["demand"] = new[] { 10.0, 20.0 }
        };

        var result1 = new GraphRunResult(24, 1, "hours", order, series, "run_123");
        var result2 = new GraphRunResult(24, 1, "hours", order, series, "run_123");
        var result3 = new GraphRunResult(24, 2, "hours", order, series, "run_123");

        // Assert: Records with same values are equal
        Assert.Equal(result1, result2);
        Assert.NotEqual(result1, result3);
    }

    [Fact]
    public void GraphRunResult_CanBeCreated_WithoutRunId()
    {
        // Arrange & Act: Create without RunId (optional parameter)
        var result = new GraphRunResult(
            Bins: 24,
            BinSize: 1,
            BinUnit: "hours",
            Order: Array.Empty<string>(),
            Series: new Dictionary<string, double[]>()
        );

        // Assert: RunId is null
        Assert.Null(result.RunId);
        Assert.Equal(24, result.Bins);
        Assert.Equal(1, result.BinSize);
        Assert.Equal("hours", result.BinUnit);
    }

    [Fact]
    public void GraphRunResult_PreservesSemanticInfo_FromEngineResponse()
    {
        // Arrange: Simulate what ApiRunClient receives from Engine
        var engineResponse = new RunResponse(
            Grid: new GridInfo(Bins: 288, BinSize: 5, BinUnit: "minutes"),
            Order: new[] { "demand", "served" },
            Series: new Dictionary<string, double[]>
            {
                ["demand"] = new[] { 100.0, 110.0, 120.0 },
                ["served"] = new[] { 95.0, 105.0, 115.0 }
            },
            RunId: "run_20241006T150000Z_xyz789",
            ArtifactsPath: "/data/runs/run_20241006T150000Z_xyz789"
        );

        // Act: Convert to GraphRunResult (what ApiRunClient.RunAsync does)
        var result = new GraphRunResult(
            Bins: engineResponse.Grid.Bins,
            BinSize: engineResponse.Grid.BinSize,
            BinUnit: engineResponse.Grid.BinUnit,
            Order: engineResponse.Order,
            Series: engineResponse.Series,
            RunId: engineResponse.RunId
        );

        // Assert: Semantic information preserved (not converted to binMinutes prematurely)
        Assert.Equal(288, result.Bins);
        Assert.Equal(5, result.BinSize);
        Assert.Equal("minutes", result.BinUnit);
        Assert.Equal(5, result.BinMinutes);  // Computed on demand
        Assert.Equal("run_20241006T150000Z_xyz789", result.RunId);
        Assert.Equal(2, result.Series.Count);
    }

    [Fact]
    public void GraphRunResult_ImmutableCollections_PreventModification()
    {
        // Arrange: Create with collections
        var order = new[] { "demand", "served" };
        var series = new Dictionary<string, double[]>
        {
            ["demand"] = new[] { 10.0 }
        };

        var result = new GraphRunResult(24, 1, "hours", order, series);

        // Assert: Collections are readonly interfaces
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Order);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, double[]>>(result.Series);
    }
}
