using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class SmokeTests
{
    [Fact]
    public void VersionInfo_IsStable()
    {
        Assert.Equal("FlowTime-Sim", VersionInfo.Product);
        Assert.True(VersionInfo.Major >= 0);
    }
}
