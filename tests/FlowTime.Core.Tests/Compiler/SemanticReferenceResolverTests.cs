using FlowTime.Core.Compiler;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Compiler;

public sealed class SemanticReferenceResolverTests
{
    [Fact]
    public void ParseSeriesReference_RecognizesRepoStandardPatterns()
    {
        var file = SemanticReferenceResolver.ParseSeriesReference("file:telemetry/arrivals.csv");
        var series = SemanticReferenceResolver.ParseSeriesReference("series:Orders@priority");
        var self = SemanticReferenceResolver.ParseSeriesReference(" self ");

        Assert.Equal(CompiledSeriesReferenceKind.File, file.Kind);
        Assert.Equal("file:telemetry/arrivals.csv", file.RawText);
        Assert.Equal("telemetry/arrivals.csv", file.FilePath);

        Assert.Equal(CompiledSeriesReferenceKind.Node, series.Kind);
        Assert.Equal("Orders", series.NodeId);
        Assert.Equal("priority", series.ClassId);

        Assert.Equal(CompiledSeriesReferenceKind.Self, self.Kind);
        Assert.Equal("self", self.CanonicalText);
    }

    [Fact]
    public void ParseParallelismReference_RecognizesConstantAndSeriesForms()
    {
        var constant = SemanticReferenceResolver.ParseParallelismReference("2.5");
        var numeric = SemanticReferenceResolver.ParseParallelismReference(4d);
        var series = SemanticReferenceResolver.ParseParallelismReference("series:Workers");

        Assert.NotNull(constant);
        Assert.Equal(2.5d, constant!.Constant);
        Assert.Null(constant.Series);

        Assert.NotNull(numeric);
        Assert.Equal(4d, numeric!.Constant);
        Assert.Null(numeric.Series);

        Assert.NotNull(series);
        Assert.Null(series!.Constant);
        Assert.NotNull(series.Series);
        Assert.Equal(CompiledSeriesReferenceKind.Node, series.Series!.Kind);
        Assert.Equal("Workers", series.Series.NodeId);
    }

    [Fact]
    public void ParseMetadata_PopulatesCompiledReferences_OnRuntimeSemantics()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Service",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "file:arrivals.csv",
                            Served = "series:served@priority",
                            QueueDepth = "self",
                            Parallelism = "2"
                        }
                    }
                }
            }
        };

        var metadata = ModelParser.ParseMetadata(model);
        var semantics = metadata.Topology!.Nodes.Single().Semantics;

        Assert.NotNull(semantics.ArrivalsRef);
        Assert.Equal(CompiledSeriesReferenceKind.File, semantics.ArrivalsRef!.Kind);
        Assert.NotNull(semantics.ServedRef);
        Assert.Equal(CompiledSeriesReferenceKind.Node, semantics.ServedRef!.Kind);
        Assert.NotNull(semantics.QueueDepthRef);
        Assert.Equal(CompiledSeriesReferenceKind.Self, semantics.QueueDepthRef!.Kind);
        Assert.NotNull(semantics.ParallelismRef);
        Assert.Equal(2d, semantics.ParallelismRef!.Constant);
    }
}