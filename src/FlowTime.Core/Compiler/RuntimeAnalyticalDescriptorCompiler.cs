using FlowTime.Core.Models;

namespace FlowTime.Core.Compiler;

public static class RuntimeAnalyticalDescriptorCompiler
{
    public static RuntimeAnalyticalDescriptor Compile(
        string nodeId,
        string? kind,
        string? nodeRole,
        NodeSemantics semantics,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitionsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(nodeDefinitionsById);

        return CompileCore(nodeId, kind, nodeRole, semantics.QueueDepth, semantics.Parallelism, nodeDefinitionsById);
    }

    public static RuntimeAnalyticalDescriptor Compile(
        string nodeId,
        string? kind,
        string? nodeRole,
        TopologyNodeSemanticsDefinition semantics,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitionsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(nodeDefinitionsById);

        return CompileCore(
            nodeId,
            kind,
            nodeRole,
            SemanticReferenceResolver.ParseOptionalSeriesReference(semantics.QueueDepth),
            semantics.Parallelism,
            nodeDefinitionsById);
    }

    private static RuntimeAnalyticalDescriptor CompileCore(
        string nodeId,
        string? kind,
        string? nodeRole,
        CompiledSeriesReference? queueDepth,
        ParallelismReference? parallelism,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitionsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(nodeDefinitionsById);

        var normalizedKind = NormalizeKind(kind);
        var category = ResolveCategory(normalizedKind, nodeRole);
        var queueSourceNodeId = ResolveQueueSourceNodeId(nodeId, normalizedKind, queueDepth, nodeDefinitionsById);
        var hasQueueSemantics = category is RuntimeAnalyticalNodeCategory.Queue or RuntimeAnalyticalNodeCategory.Dlq
            || !string.IsNullOrWhiteSpace(queueSourceNodeId);
        var hasServiceSemantics = category == RuntimeAnalyticalNodeCategory.Service;
        var identity = ResolveIdentity(normalizedKind, category, hasQueueSemantics, hasServiceSemantics);

        return new RuntimeAnalyticalDescriptor
        {
            Identity = identity,
            Category = category,
            HasQueueSemantics = hasQueueSemantics,
            HasServiceSemantics = hasServiceSemantics,
            HasCycleTimeDecomposition = hasQueueSemantics && hasServiceSemantics,
            StationarityWarningApplicable = hasQueueSemantics,
            QueueSourceNodeId = queueSourceNodeId,
            Parallelism = BuildParallelismDescriptor(nodeId, parallelism)
        };
    }

    private static RuntimeAnalyticalNodeCategory ResolveCategory(string normalizedKind, string? nodeRole)
    {
        if (string.Equals(nodeRole, "sink", StringComparison.OrdinalIgnoreCase) || normalizedKind == "sink")
        {
            return RuntimeAnalyticalNodeCategory.Sink;
        }

        return normalizedKind switch
        {
            "queue" => RuntimeAnalyticalNodeCategory.Queue,
            "dlq" => RuntimeAnalyticalNodeCategory.Dlq,
            "router" => RuntimeAnalyticalNodeCategory.Router,
            "dependency" => RuntimeAnalyticalNodeCategory.Dependency,
            "const" or "constant" or "pmf" => RuntimeAnalyticalNodeCategory.Constant,
            "expr" or "expression" => RuntimeAnalyticalNodeCategory.Expression,
            _ => RuntimeAnalyticalNodeCategory.Service
        };
    }

    private static string? ResolveQueueSourceNodeId(
        string nodeId,
        string normalizedKind,
        CompiledSeriesReference? queueDepth,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitionsById)
    {
        if (normalizedKind is "servicewithbuffer" or "queue" or "dlq")
        {
            return nodeId;
        }

        if (queueDepth is null)
        {
            return null;
        }

        if (queueDepth.Kind == CompiledSeriesReferenceKind.Self)
        {
            return nodeId;
        }

        if (queueDepth.Kind != CompiledSeriesReferenceKind.Node)
        {
            return null;
        }

        var candidateId = queueDepth.ResolveProducerId(nodeId);
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return null;
        }

        return nodeDefinitionsById.TryGetValue(candidateId, out var definition) &&
               string.Equals(definition.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase)
            ? candidateId
            : null;
    }

    private static RuntimeParallelismDescriptor? BuildParallelismDescriptor(string nodeId, ParallelismReference? parallelism)
    {
        if (parallelism is null)
        {
            return null;
        }

        var seriesSourceNodeId = parallelism.SeriesReference?.ResolveProducerId(nodeId);
        return new RuntimeParallelismDescriptor
        {
            Constant = parallelism.Constant,
            SeriesSourceNodeId = string.IsNullOrWhiteSpace(seriesSourceNodeId) ? null : seriesSourceNodeId
        };
    }

    private static RuntimeAnalyticalIdentity ResolveIdentity(
        string normalizedKind,
        RuntimeAnalyticalNodeCategory category,
        bool hasQueueSemantics,
        bool hasServiceSemantics)
    {
        if (category == RuntimeAnalyticalNodeCategory.Service)
        {
            return hasQueueSemantics && hasServiceSemantics
                ? RuntimeAnalyticalIdentity.ServiceWithBuffer
                : RuntimeAnalyticalIdentity.Service;
        }

        return category switch
        {
            RuntimeAnalyticalNodeCategory.Queue => RuntimeAnalyticalIdentity.Queue,
            RuntimeAnalyticalNodeCategory.Dlq => RuntimeAnalyticalIdentity.Dlq,
            RuntimeAnalyticalNodeCategory.Router => RuntimeAnalyticalIdentity.Router,
            RuntimeAnalyticalNodeCategory.Dependency => RuntimeAnalyticalIdentity.Dependency,
            RuntimeAnalyticalNodeCategory.Sink => RuntimeAnalyticalIdentity.Sink,
            RuntimeAnalyticalNodeCategory.Constant => normalizedKind == "pmf"
                ? RuntimeAnalyticalIdentity.Pmf
                : RuntimeAnalyticalIdentity.Constant,
            RuntimeAnalyticalNodeCategory.Expression => RuntimeAnalyticalIdentity.Expression,
            _ => RuntimeAnalyticalIdentity.Service
        };
    }

    private static string NormalizeKind(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? "service"
            : kind.Trim().ToLowerInvariant();
    }
}