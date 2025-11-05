namespace FlowTime.Core.Models;

public sealed record NodeData
{
    public required string NodeId { get; init; }
    public required double[] Arrivals { get; init; }
    public required double[] Served { get; init; }
    public required double[] Errors { get; init; }
    public double[]? Attempts { get; init; }
    public double[]? Failures { get; init; }
    public double[]? RetryEcho { get; init; }
    public double[]? RetryKernel { get; init; }
    public double[]? ExternalDemand { get; init; }
    public double[]? QueueDepth { get; init; }
    public double[]? Capacity { get; init; }
    public double[]? Values { get; init; }
}
