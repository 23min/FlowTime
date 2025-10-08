using FlowTime.Core.Fixtures;
using FlowTime.Core.Models;
using Xunit;

namespace FlowTime.Core.Tests.Integration;

public class FixtureIntegrationTests
{
    [Theory]
    [InlineData("order-system", 288)]
    [InlineData("microservices", 144)]
    [InlineData("http-service", 96)]
    public void FixtureMetadata_ParsesWindowAndTopology(string fixtureName, int expectedBins)
    {
        var metadata = FixtureModelLoader.LoadMetadata(fixtureName);

        Assert.Equal(expectedBins, metadata.Window.Bins);
        Assert.NotNull(metadata.Topology);
        Assert.NotEmpty(metadata.Topology!.Nodes);
    }
}
