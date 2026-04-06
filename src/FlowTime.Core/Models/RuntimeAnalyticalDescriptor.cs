namespace FlowTime.Core.Models;

public enum RuntimeAnalyticalNodeCategory
{
    Service,
    Queue,
    Dlq,
    Router,
    Dependency,
    Sink,
    Constant,
    Expression
}

public enum RuntimeAnalyticalIdentity
{
    Service,
    ServiceWithBuffer,
    Queue,
    Dlq,
    Router,
    Dependency,
    Sink,
    Constant,
    Pmf,
    Expression
}

public sealed record RuntimeParallelismDescriptor
{
    public double? Constant { get; init; }

    public string? SeriesSourceNodeId { get; init; }
}

public sealed record RuntimeAnalyticalDescriptor
{
    public required RuntimeAnalyticalIdentity Identity { get; init; }

    public required RuntimeAnalyticalNodeCategory Category { get; init; }

    public required bool HasQueueSemantics { get; init; }

    public required bool HasServiceSemantics { get; init; }

    public required bool HasCycleTimeDecomposition { get; init; }

    public required bool StationarityWarningApplicable { get; init; }

    public string? QueueSourceNodeId { get; init; }

    public RuntimeParallelismDescriptor? Parallelism { get; init; }
}