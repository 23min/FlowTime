using System;
using FlowTime.Core.DataSources;
using Xunit;

namespace FlowTime.Core.Tests.DataSources;

public class UriResolverTests
{
    [Fact]
    public void ResolveFilePath_WithRelativeUri_ReturnsCombinedPath()
    {
        var resolved = UriResolver.ResolveFilePath("file:telemetry/OrderService.csv", "/model");
        Assert.Equal("/model/telemetry/OrderService.csv", resolved);
    }

    [Fact]
    public void ResolveFilePath_WithAbsoluteUri_ReturnsPath()
    {
        var resolved = UriResolver.ResolveFilePath("file:/tmp/data.csv", null);
        Assert.Equal("/tmp/data.csv", resolved);
    }

    [Fact]
    public void ResolveFilePath_RelativeWithoutModelDirectory_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => UriResolver.ResolveFilePath("file:data.csv", null));
    }

    [Fact]
    public void ResolveFilePath_UnsupportedScheme_Throws()
    {
        Assert.Throws<NotSupportedException>(() => UriResolver.ResolveFilePath("http://example.com/data.csv", "/model"));
    }
}
