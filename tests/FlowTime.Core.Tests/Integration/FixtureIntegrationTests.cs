using FlowTime.Core.Fixtures;
using Xunit;

namespace FlowTime.Core.Tests.Integration;

public class FixtureIntegrationTests
{
    [Theory]
    [InlineData("order-system", 288)]
    [InlineData("microservices", 144)]
    [InlineData("http-service", 96)]
    public void FixtureRunBuilder_GeneratesNodeData(string fixtureName, int expectedBins)
    {
        var run = FixtureRunBuilder.Build(fixtureName);

        Assert.Equal(expectedBins, run.Metadata.Window.Bins);
        Assert.NotNull(run.Metadata.Topology);
        Assert.Equal(run.Metadata.Topology!.Nodes.Count, run.Nodes.Count);

        foreach (var node in run.Nodes.Values)
        {
            Assert.Equal(expectedBins, node.Arrivals.Length);
            Assert.Equal(expectedBins, node.Served.Length);
            Assert.Equal(expectedBins, node.Errors.Length);
        }
    }
}
