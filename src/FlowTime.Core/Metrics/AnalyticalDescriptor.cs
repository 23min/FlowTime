using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public enum AnalyticalIdentity
{
    Unknown,
    Service,
    ServiceWithBuffer,
    Queue,
    Dlq,
    Router,
    External,
    Sink,
    Dependency,
    Expression,
    Constant
}

public enum AnalyticalNodeCategory
{
    Unknown,
    Expression,
    Constant,
    Service,
    Queue,
    Dlq,
    Router
}

public enum AnalyticalQueueOrigin
{
    None,
    Explicit,
    Derived
}

public sealed record AnalyticalDescriptor
{
    public static readonly AnalyticalDescriptor None = new()
    {
        Identity = AnalyticalIdentity.Unknown,
        Category = AnalyticalNodeCategory.Unknown,
        QueueOrigin = AnalyticalQueueOrigin.None
    };

    public required AnalyticalIdentity Identity { get; init; }
    public required AnalyticalNodeCategory Category { get; init; }
    public bool HasQueueSemantics { get; init; }
    public bool HasServiceSemantics { get; init; }
    public bool HasCycleTimeDecomposition { get; init; }
    public bool StationarityWarningApplicable { get; init; }
    public string? QueueSourceNodeId { get; init; }
    public AnalyticalQueueOrigin QueueOrigin { get; init; }
    public CompiledParallelismReference? Parallelism { get; init; }
}