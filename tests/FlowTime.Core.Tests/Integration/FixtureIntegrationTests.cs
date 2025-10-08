using FlowTime.Core.Fixtures;
using FlowTime.Core.Models;
using FlowTime.Core.Validation;
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

    [Fact]
    public void FixtureSemanticLoader_LoadsOrderServiceAndValidatesInitialCondition()
    {
        var metadata = FixtureModelLoader.LoadMetadata("order-system");
        var loader = new FixtureSemanticLoader(metadata, "order-system");

        var nodeData = loader.LoadNode("OrderService");
        Assert.Equal(metadata.Window.Bins, nodeData.Arrivals.Length);

        var validator = new InitialConditionValidator();
        validator.Validate(nodeData, new InitialCondition { QueueDepth = 0 });
    }
}
