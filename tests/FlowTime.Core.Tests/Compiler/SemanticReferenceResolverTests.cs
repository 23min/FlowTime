using FlowTime.Core.Compiler;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Compiler;

public sealed class SemanticReferenceResolverTests
{
    [Fact]
    public void ParseSeriesReference_BareNode_ReturnsNodeReference()
    {
        var reference = SemanticReferenceResolver.ParseSeriesReference("orders_served");

        Assert.Equal(CompiledSeriesReferenceKind.Node, reference.Kind);
        Assert.Equal("orders_served", reference.RawText);
        Assert.Equal("orders_served", reference.NodeId);
        Assert.Null(reference.ClassId);
        Assert.Equal("orders_served", reference.LookupKey);
    }

    [Fact]
    public void ParseSeriesReference_SeriesPrefixWithClass_ReturnsNodeReference()
    {
        var reference = SemanticReferenceResolver.ParseSeriesReference("series:orders_served@premium");

        Assert.Equal(CompiledSeriesReferenceKind.Node, reference.Kind);
        Assert.Equal("series:orders_served@premium", reference.RawText);
        Assert.Equal("orders_served", reference.NodeId);
        Assert.Equal("premium", reference.ClassId);
        Assert.Equal("orders_served", reference.LookupKey);
        Assert.Equal("orders_served", reference.ResolveProducerId("OrderService"));
    }

    [Fact]
    public void ParseSeriesReference_File_ReturnsFileReference()
    {
        var reference = SemanticReferenceResolver.ParseSeriesReference("file:telemetry/orders_served.csv");

        Assert.Equal(CompiledSeriesReferenceKind.File, reference.Kind);
        Assert.Equal("file:telemetry/orders_served.csv", reference.RawText);
        Assert.Equal("telemetry/orders_served.csv", reference.FilePath);
        Assert.Equal("orders_served", reference.LookupKey);
        Assert.Null(reference.ResolveProducerId("OrderService"));
    }

    [Fact]
    public void ParseSeriesReference_Self_ReturnsSelfReference()
    {
        var reference = SemanticReferenceResolver.ParseSeriesReference(" self ");

        Assert.Equal(CompiledSeriesReferenceKind.Self, reference.Kind);
        Assert.Equal("self", reference.RawText);
        Assert.Equal("self", reference.LookupKey);
        Assert.Equal("OrderQueue", reference.ResolveProducerId("OrderQueue"));
    }

    [Fact]
    public void ParseOptionalSeriesReference_Blank_ReturnsNull()
    {
        var reference = SemanticReferenceResolver.ParseOptionalSeriesReference("  ");

        Assert.Null(reference);
    }
}