using FlowTime.Core.Compiler;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;

namespace FlowTime.Core.Tests.Metrics;

internal static class AnalyticalDescriptorTestFactory
{
    public static AnalyticalDescriptor ForKind(string? kind) =>
        AnalyticalDescriptorCompiler.BuildForKind(kind);

    public static AnalyticalDescriptor ResolvedServiceWithBuffer(string? kind = "service")
    {
        const string queueDepth = "file:BufferNode.csv";

        var semantics = new NodeSemantics
        {
            Arrivals = "file:QueueReader_arrivals.csv",
            ArrivalsRef = SemanticReferenceResolver.ParseSeriesReference("file:QueueReader_arrivals.csv"),
            Served = "file:QueueReader_served.csv",
            ServedRef = SemanticReferenceResolver.ParseSeriesReference("file:QueueReader_served.csv"),
            QueueDepth = queueDepth,
            QueueDepthRef = SemanticReferenceResolver.ParseOptionalSeriesReference(queueDepth)
        };

        var nodeDefinitions = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["BufferNode"] = new()
            {
                Id = "BufferNode",
                Kind = "serviceWithBuffer"
            }
        };

        return AnalyticalDescriptorCompiler.Build("QueueReader", kind, semantics, nodeDefinitions);
    }
}