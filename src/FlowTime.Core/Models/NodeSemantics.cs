using System.Collections.Generic;

namespace FlowTime.Core.Models;

public sealed record NodeSemantics
{
    public required CompiledSeriesReference Arrivals { get; init; }
    public required CompiledSeriesReference Served { get; init; }
    public CompiledSeriesReference? Errors { get; init; }
    public CompiledSeriesReference? Attempts { get; init; }
    public CompiledSeriesReference? Failures { get; init; }
    public CompiledSeriesReference? ExhaustedFailures { get; init; }
    public CompiledSeriesReference? RetryEcho { get; init; }
    public CompiledSeriesReference? RetryBudgetRemaining { get; init; }
    public IReadOnlyList<double>? RetryKernel { get; init; }
    public CompiledSeriesReference? ExternalDemand { get; init; }
    public CompiledSeriesReference? QueueDepth { get; init; }
    public CompiledSeriesReference? Capacity { get; init; }
    public ParallelismReference? Parallelism { get; init; }
    public CompiledSeriesReference? ProcessingTimeMsSum { get; init; }
    public CompiledSeriesReference? ServedCount { get; init; }
    public double? SlaMinutes { get; init; }
    public double? MaxAttempts { get; init; }
    public string? BackoffStrategy { get; init; }
    public string? ExhaustedPolicy { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}
