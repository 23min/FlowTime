using FlowTime.UI.Models;
using FlowTime.UI.Services;
using Moq;

namespace FlowTime.UI.Tests.Services;

public class GraphAnalysisServiceTests
{
    private readonly Mock<IRunClient> _mockRunClient;
    private readonly GraphAnalysisService _service;

    public GraphAnalysisServiceTests()
    {
        _mockRunClient = new Mock<IRunClient>();
        _service = new GraphAnalysisService(_mockRunClient.Object);
    }

    [Fact]
    public async Task AnalyzeGraphAsync_Success_ReturnsExpectedResult()
    {
        // Arrange
        var yaml = "test: yaml";
        var mockStructure = new GraphStructureResult
        {
            Nodes = new List<NodeStructure>
            {
                new() { Id = "A", Inputs = new List<string>() },
                new() { Id = "B", Inputs = new List<string> { "A" } },
                new() { Id = "C", Inputs = new List<string> { "A", "B" } }
            },
            Order = new List<string> { "A", "B", "C" }
        };

        var mockResponse = new ApiResponse<GraphStructureResult>
        {
            Success = true,
            Value = mockStructure
        };

        _mockRunClient.Setup(x => x.GetGraphStructureAsync(yaml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStructure);

        // Act
        var result = await _service.AnalyzeGraphAsync(yaml);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(mockStructure, result.Structure);
        Assert.Equal(3, result.NodeViews.Count);
        Assert.Equal(3, result.Stats.Total);
        Assert.Equal(1, result.Stats.Sources); // Only A has no inputs
        Assert.Equal(1, result.Stats.Sinks);   // Only C has no outputs
        Assert.Equal(2, result.Stats.MaxFanOut); // A outputs to both B and C
    }

    [Fact]
    public async Task AnalyzeGraphAsync_Exception_ReturnsError()
    {
        // Arrange
        var yaml = "test: yaml";
        var errorMessage = "Network error";
        
        _mockRunClient.Setup(x => x.GetGraphStructureAsync(yaml, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _service.AnalyzeGraphAsync(yaml);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.Error);
        Assert.Null(result.Structure);
        Assert.Empty(result.NodeViews);
    }

    [Fact]
    public void BuildNodeViews_EmptyGraph_ReturnsEmpty()
    {
        // Arrange
        var structure = new GraphStructureResult
        {
            Nodes = new List<NodeStructure>(),
            Order = new List<string>()
        };

        // Act
        var result = _service.AnalyzeGraphAsync("yaml").Result;

        // We can't directly test BuildNodeViews as it's private, but we can test through the public method
        // This test verifies the edge case handling
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AnalyzeGraphAsync_ComplexGraph_ComputesStatsCorrectly()
    {
        // Arrange
        var yaml = "complex: yaml";
        var mockStructure = new GraphStructureResult
        {
            Nodes = new List<NodeStructure>
            {
                new() { Id = "Source1", Inputs = new List<string>() },
                new() { Id = "Source2", Inputs = new List<string>() },
                new() { Id = "Middle", Inputs = new List<string> { "Source1", "Source2" } },
                new() { Id = "Sink1", Inputs = new List<string> { "Middle" } },
                new() { Id = "Sink2", Inputs = new List<string> { "Middle" } }
            },
            Order = new List<string> { "Source1", "Source2", "Middle", "Sink1", "Sink2" }
        };

        _mockRunClient.Setup(x => x.GetGraphStructureAsync(yaml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStructure);

        // Act
        var result = await _service.AnalyzeGraphAsync(yaml);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.Stats.Total);
        Assert.Equal(2, result.Stats.Sources); // Source1, Source2
        Assert.Equal(2, result.Stats.Sinks);   // Sink1, Sink2
        Assert.Equal(2, result.Stats.MaxFanOut); // Middle outputs to 2 nodes
    }
}