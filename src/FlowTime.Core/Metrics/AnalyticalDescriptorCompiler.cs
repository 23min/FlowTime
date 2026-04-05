using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public static class AnalyticalDescriptorCompiler
{
    public static AnalyticalDescriptor Build(
        string nodeId,
        string? kind,
        NodeSemantics semantics,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions)
    {
        var normalizedKind = NormalizeKind(kind);
        var serviceWithBufferSourceId = ResolveServiceWithBufferSourceNodeId(nodeId, normalizedKind, semantics.QueueDepthRef, nodeDefinitions);
        var identity = ResolveIdentity(normalizedKind, serviceWithBufferSourceId);

        return new AnalyticalDescriptor
        {
            Identity = identity,
            Category = ResolveCategory(identity),
            HasQueueSemantics = identity is AnalyticalIdentity.Queue or AnalyticalIdentity.Dlq or AnalyticalIdentity.ServiceWithBuffer,
            HasServiceSemantics = identity is AnalyticalIdentity.Service or AnalyticalIdentity.ServiceWithBuffer,
            HasCycleTimeDecomposition = identity == AnalyticalIdentity.ServiceWithBuffer,
            StationarityWarningApplicable = identity is AnalyticalIdentity.Queue or AnalyticalIdentity.Dlq or AnalyticalIdentity.ServiceWithBuffer,
            QueueSourceNodeId = ResolveQueueSourceNodeId(nodeId, normalizedKind, identity, semantics.QueueDepthRef, nodeDefinitions, serviceWithBufferSourceId),
            QueueOrigin = ResolveQueueOrigin(identity, semantics.QueueDepthRef, nodeDefinitions),
            Parallelism = semantics.ParallelismRef
        };
    }

    public static AnalyticalDescriptor BuildForKind(string? kind)
    {
        var normalizedKind = NormalizeKind(kind);
        var identity = ResolveIdentity(normalizedKind, serviceWithBufferSourceId: null);

        return new AnalyticalDescriptor
        {
            Identity = identity,
            Category = ResolveCategory(identity),
            HasQueueSemantics = identity is AnalyticalIdentity.Queue or AnalyticalIdentity.Dlq or AnalyticalIdentity.ServiceWithBuffer,
            HasServiceSemantics = identity is AnalyticalIdentity.Service or AnalyticalIdentity.ServiceWithBuffer,
            HasCycleTimeDecomposition = identity == AnalyticalIdentity.ServiceWithBuffer,
            StationarityWarningApplicable = identity is AnalyticalIdentity.Queue or AnalyticalIdentity.Dlq or AnalyticalIdentity.ServiceWithBuffer,
            QueueSourceNodeId = identity == AnalyticalIdentity.ServiceWithBuffer ? "self" : null,
            QueueOrigin = AnalyticalQueueOrigin.None
        };
    }

    private static AnalyticalIdentity ResolveIdentity(string normalizedKind, string? serviceWithBufferSourceId)
    {
        if (!string.IsNullOrWhiteSpace(serviceWithBufferSourceId))
        {
            return AnalyticalIdentity.ServiceWithBuffer;
        }

        return normalizedKind switch
        {
            "service" => AnalyticalIdentity.Service,
            "servicewithbuffer" => AnalyticalIdentity.ServiceWithBuffer,
            "queue" => AnalyticalIdentity.Queue,
            "dlq" => AnalyticalIdentity.Dlq,
            "router" => AnalyticalIdentity.Router,
            "external" => AnalyticalIdentity.External,
            "sink" => AnalyticalIdentity.Sink,
            "dependency" => AnalyticalIdentity.Dependency,
            "expr" or "expression" => AnalyticalIdentity.Expression,
            "const" or "constant" or "pmf" => AnalyticalIdentity.Constant,
            _ => AnalyticalIdentity.Unknown
        };
    }

    private static AnalyticalNodeCategory ResolveCategory(AnalyticalIdentity identity)
    {
        return identity switch
        {
            AnalyticalIdentity.Expression => AnalyticalNodeCategory.Expression,
            AnalyticalIdentity.Constant => AnalyticalNodeCategory.Constant,
            AnalyticalIdentity.Queue => AnalyticalNodeCategory.Queue,
            AnalyticalIdentity.Dlq => AnalyticalNodeCategory.Dlq,
            AnalyticalIdentity.Router => AnalyticalNodeCategory.Router,
            AnalyticalIdentity.Service or
            AnalyticalIdentity.ServiceWithBuffer or
            AnalyticalIdentity.External or
            AnalyticalIdentity.Sink or
            AnalyticalIdentity.Dependency => AnalyticalNodeCategory.Service,
            _ => AnalyticalNodeCategory.Unknown
        };
    }

    private static string? ResolveServiceWithBufferSourceNodeId(
        string nodeId,
        string normalizedKind,
        CompiledSeriesReference? queueDepthRef,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions)
    {
        if (normalizedKind == "servicewithbuffer")
        {
            return nodeId;
        }

        var candidateId = queueDepthRef?.ProducerIdCandidate;
        if (string.IsNullOrWhiteSpace(candidateId) ||
            string.Equals(candidateId, "self", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return nodeDefinitions.TryGetValue(candidateId, out var definition) &&
            string.Equals(definition.Kind, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase)
            ? definition.Id
            : null;
    }

    private static string? ResolveQueueSourceNodeId(
        string nodeId,
        string normalizedKind,
        AnalyticalIdentity identity,
        CompiledSeriesReference? queueDepthRef,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions,
        string? serviceWithBufferSourceId)
    {
        if (identity == AnalyticalIdentity.ServiceWithBuffer)
        {
            return serviceWithBufferSourceId ?? nodeId;
        }

        if (queueDepthRef is null)
        {
            return null;
        }

        if (queueDepthRef.Kind == CompiledSeriesReferenceKind.Node &&
            !string.IsNullOrWhiteSpace(queueDepthRef.NodeId) &&
            !string.Equals(queueDepthRef.NodeId, "self", StringComparison.OrdinalIgnoreCase))
        {
            return queueDepthRef.NodeId;
        }

        var candidateId = queueDepthRef.ProducerIdCandidate;
        if (!string.IsNullOrWhiteSpace(candidateId) && nodeDefinitions.ContainsKey(candidateId))
        {
            return candidateId;
        }

        return normalizedKind is "queue" or "dlq" ? nodeId : null;
    }

    private static AnalyticalQueueOrigin ResolveQueueOrigin(
        AnalyticalIdentity identity,
        CompiledSeriesReference? queueDepthRef,
        IReadOnlyDictionary<string, NodeDefinition> nodeDefinitions)
    {
        if (!IsQueueCapable(identity) || queueDepthRef is null)
        {
            return AnalyticalQueueOrigin.None;
        }

        if (queueDepthRef.Kind == CompiledSeriesReferenceKind.File)
        {
            return AnalyticalQueueOrigin.Explicit;
        }

        var seriesId = queueDepthRef.ProducerIdCandidate;
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            return AnalyticalQueueOrigin.None;
        }

        if (string.Equals(seriesId, "self", StringComparison.OrdinalIgnoreCase))
        {
            return AnalyticalQueueOrigin.Derived;
        }

        if (nodeDefinitions.TryGetValue(seriesId, out var definition))
        {
            if (definition.Metadata is not null &&
                definition.Metadata.TryGetValue("series.origin", out var origin) &&
                string.Equals(origin, "derived", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyticalQueueOrigin.Derived;
            }

            return AnalyticalQueueOrigin.Explicit;
        }

        return AnalyticalQueueOrigin.Derived;
    }

    private static bool IsQueueCapable(AnalyticalIdentity identity) =>
        identity is AnalyticalIdentity.Queue or AnalyticalIdentity.Dlq or AnalyticalIdentity.ServiceWithBuffer;

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "service";
        }

        return kind.Trim().ToLowerInvariant();
    }
}