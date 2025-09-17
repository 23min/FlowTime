using FlowTime.UI.Models;
using FlowTime.UI.Services;
using Moq;

namespace FlowTime.UI.Tests.Services;

public class SimulationResultsServiceTests
{
    private readonly Mock<IRunClient> _mockRunClient;
    private readonly SimulationResultsService _service;

    public SimulationResultsServiceTests()
    {
        _mockRunClient = new Mock<IRunClient>();
        _service = new SimulationResultsService(_mockRunClient.Object);
    }

    [Fact]
    public async Task RunSimulationAsync_Success_ReturnsExpectedResult()
    {
        // Arrange
        var yaml = "test: yaml";
        var mockRunResult = new GraphRunResult
        {
            OutputCsv = "Header1,FlowTime,Header3\nItem1,1.5,Value3\nItem2,2.3,Value4\nItem3,0.8,Value5"
        };

        var mockResponse = new ApiResponse<GraphRunResult>
        {
            Success = true,
            Value = mockRunResult
        };

        _mockRunClient.Setup(x => x.RunModelAsync(yaml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockRunResult);

        // Act
        var result = await _service.RunSimulationAsync(yaml);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.CsvLines.Count); // Header + 3 data rows
        Assert.Equal(3, result.Chart.Series.Count); // 3 flow time values
        Assert.Equal(new List<double> { 1.5, 2.3, 0.8 }, result.Chart.Series);
        Assert.Equal(new List<string> { "#1", "#2", "#3" }, result.Chart.Labels);
    }

    [Fact]
    public async Task RunSimulationAsync_Exception_ReturnsError()
    {
        // Arrange
        var yaml = "test: yaml";
        var errorMessage = "Simulation failed";
        
        _mockRunClient.Setup(x => x.RunModelAsync(yaml, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _service.RunSimulationAsync(yaml);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.Error);
        Assert.Empty(result.CsvLines);
        Assert.Empty(result.Chart.Series);
        Assert.Empty(result.Chart.Labels);
    }

    [Fact]
    public void BuildChart_EmptyCsv_ReturnsEmptyChart()
    {
        // Arrange
        var csvLines = new List<string>();

        // Act
        var result = _service.BuildChart(csvLines);

        // Assert
        Assert.Empty(result.Series);
        Assert.Empty(result.Labels);
    }

    [Fact]
    public void BuildChart_HeaderOnly_ReturnsEmptyChart()
    {
        // Arrange
        var csvLines = new List<string> { "Header1,FlowTime,Header3" };

        // Act
        var result = _service.BuildChart(csvLines);

        // Assert
        Assert.Empty(result.Series);
        Assert.Empty(result.Labels);
    }

    [Fact]
    public void BuildChart_ValidData_ParsesCorrectly()
    {
        // Arrange
        var csvLines = new List<string>
        {
            "Header1,FlowTime,Header3",
            "Item1,1.5,Value3",
            "Item2,2.3,Value4",
            "Item3,0.8,Value5"
        };

        // Act
        var result = _service.BuildChart(csvLines);

        // Assert
        Assert.Equal(3, result.Series.Count);
        Assert.Equal(1.5, result.Series[0]);
        Assert.Equal(2.3, result.Series[1]);
        Assert.Equal(0.8, result.Series[2]);
        Assert.Equal(new List<string> { "#1", "#2", "#3" }, result.Labels);
    }

    [Fact]
    public void BuildChart_InvalidFlowTimeData_SkipsInvalidRows()
    {
        // Arrange
        var csvLines = new List<string>
        {
            "Header1,FlowTime,Header3",
            "Item1,1.5,Value3",
            "Item2,invalid,Value4",  // Invalid flow time
            "Item3,2.8,Value5",
            "Item4,,Value6"  // Empty flow time
        };

        // Act
        var result = _service.BuildChart(csvLines);

        // Assert
        Assert.Equal(2, result.Series.Count); // Only valid rows
        Assert.Equal(1.5, result.Series[0]);
        Assert.Equal(2.8, result.Series[1]);
        Assert.Equal(new List<string> { "#1", "#2" }, result.Labels);
    }

    [Fact]
    public void BuildChart_InsufficientColumns_SkipsInvalidRows()
    {
        // Arrange
        var csvLines = new List<string>
        {
            "Header1,FlowTime,Header3",
            "Item1,1.5,Value3",
            "Item2",  // Only one column
            "Item3,2.8,Value5"
        };

        // Act
        var result = _service.BuildChart(csvLines);

        // Assert
        Assert.Equal(2, result.Series.Count); // Only valid rows
        Assert.Equal(1.5, result.Series[0]);
        Assert.Equal(2.8, result.Series[1]);
    }
}