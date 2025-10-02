using FlowTime.Core;
using FlowTime.Core.Pmf;

namespace FlowTime.Tests.Pmf;

public class PmfNodeTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesNode()
    {
        // Arrange
        var nodeId = new NodeId("test");
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 0.5 }, { 20, 0.5 } });

        // Act
        var node = new PmfNode(nodeId, pmf);

        // Assert
        Assert.Equal(nodeId, node.Id);
        Assert.Equal(pmf, node.Pmf);
    }

    [Fact]
    public void Constructor_NullPmf_ThrowsArgumentNullException()
    {
        // Arrange
        var nodeId = new NodeId("test");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PmfNode(nodeId, null!));
    }

    [Fact]
    public void Inputs_Always_ReturnsEmptyCollection()
    {
        // Arrange
        var node = new PmfNode(new NodeId("test"), 
            new Core.Pmf.Pmf(new Dictionary<double, double> { { 1, 1.0 } }));

        // Act
        var inputs = node.Inputs.ToList();

        // Assert
        Assert.Empty(inputs);
    }

    [Fact]
    public void Evaluate_CreatesConstantSeriesWithExpectedValue()
    {
        // Arrange
        var nodeId = new NodeId("demand");
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 0.2 }, { 20, 0.3 }, { 30, 0.5 } });
        var node = new PmfNode(nodeId, pmf);
        var grid = new TimeGrid(4, 60, TimeUnit.Minutes); // 4 bins, 60 minutes each
        
        // Act
        var result = node.Evaluate(grid, _ => throw new NotSupportedException("PMF nodes should not request inputs"));

        // Assert
        Assert.Equal(4, result.Length);
        var expectedValue = pmf.ExpectedValue; // 10*0.2 + 20*0.3 + 30*0.5 = 23
        for (int i = 0; i < result.Length; i++)
        {
            Assert.Equal(expectedValue, result[i]);
        }
    }

    [Fact]
    public void Evaluate_DifferentGridSizes_ProducesCorrectSizedSeries()
    {
        // Arrange
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 42, 1.0 } });
        var node = new PmfNode(new NodeId("test"), pmf);

        // Act & Assert for various grid sizes
        foreach (var bins in new[] { 1, 3, 10, 24 })
        {
            var grid = new TimeGrid(bins, 60);
            var result = node.Evaluate(grid, _ => throw new NotSupportedException());
            
            Assert.Equal(bins, result.Length);
            for (int i = 0; i < result.Length; i++)
            {
                Assert.Equal(42.0, result[i]);
            }
        }
    }

    [Fact]
    public void Evaluate_ComplexPmf_UsesCorrectExpectedValue()
    {
        // Arrange: Realistic demand distribution
        var distribution = new Dictionary<double, double>
        {
            { 0, 0.05 }, { 50, 0.25 }, { 100, 0.4 }, { 150, 0.25 }, { 200, 0.05 }
        };
        var pmf = new Core.Pmf.Pmf(distribution);
        var node = new PmfNode(new NodeId("demand"), pmf);
        var grid = new TimeGrid(3, 60, TimeUnit.Minutes);

        // Act
        var result = node.Evaluate(grid, _ => throw new NotSupportedException());

        // Assert
        var expectedValue = 0*0.05 + 50*0.25 + 100*0.4 + 150*0.25 + 200*0.05; // = 100
        Assert.Equal(3, result.Length);
        for (int i = 0; i < result.Length; i++)
        {
            Assert.Equal(expectedValue, result[i]);
        }
    }

    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        // Arrange
        var nodeId = new NodeId("test_node");
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 1.0 } });
        var node = new PmfNode(nodeId, pmf);

        // Act
        var result = node.ToString();

        // Assert
        Assert.Contains("PmfNode", result);
        Assert.Contains("test_node", result);
        Assert.Contains("Pmf", result);
    }

    [Fact]
    public void Equals_SameNodeIdAndPmf_ReturnsTrue()
    {
        // Arrange
        var nodeId = new NodeId("test");
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 0.5 }, { 20, 0.5 } });
        var node1 = new PmfNode(nodeId, pmf);
        var node2 = new PmfNode(nodeId, pmf);

        // Act & Assert
        Assert.True(node1.Equals(node2));
        Assert.Equal(node1.GetHashCode(), node2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentNodeId_ReturnsFalse()
    {
        // Arrange
        var pmf = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 1.0 } });
        var node1 = new PmfNode(new NodeId("node1"), pmf);
        var node2 = new PmfNode(new NodeId("node2"), pmf);

        // Act & Assert
        Assert.False(node1.Equals(node2));
    }

    [Fact]
    public void Equals_DifferentPmf_ReturnsFalse()
    {
        // Arrange
        var nodeId = new NodeId("test");
        var pmf1 = new Core.Pmf.Pmf(new Dictionary<double, double> { { 10, 1.0 } });
        var pmf2 = new Core.Pmf.Pmf(new Dictionary<double, double> { { 20, 1.0 } });
        var node1 = new PmfNode(nodeId, pmf1);
        var node2 = new PmfNode(nodeId, pmf2);

        // Act & Assert
        Assert.False(node1.Equals(node2));
    }
}
