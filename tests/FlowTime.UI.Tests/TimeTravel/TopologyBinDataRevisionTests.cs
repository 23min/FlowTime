using FlowTime.UI.Pages.TimeTravel;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyBinDataRevisionTests
{
    [Fact]
    public void BinDataRefresh_IgnoresStaleResults()
    {
        var topology = new Topology();

        topology.TestSetBinDataRevision(1);
        Assert.True(topology.TestApplyBinDataResult(1, 10));
        Assert.Equal(10, topology.TestGetSelectedBin());

        topology.TestSetBinDataRevision(2);
        Assert.False(topology.TestApplyBinDataResult(1, 5));
        Assert.Equal(10, topology.TestGetSelectedBin());
    }
}
