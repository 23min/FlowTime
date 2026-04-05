using System.Collections.Generic;

namespace FlowTime.Core.Models;

public sealed record NodeSemantics
{
    public required string Arrivals { get; init; }
    public CompiledSeriesReference? ArrivalsRef { get; init; }
    public required string Served { get; init; }
    public CompiledSeriesReference? ServedRef { get; init; }
    public string? Errors { get; init; }
    public CompiledSeriesReference? ErrorsRef { get; init; }
    public string? Attempts { get; init; }
    public CompiledSeriesReference? AttemptsRef { get; init; }
    public string? Failures { get; init; }
    public CompiledSeriesReference? FailuresRef { get; init; }
    public string? ExhaustedFailures { get; init; }
    public CompiledSeriesReference? ExhaustedFailuresRef { get; init; }
    public string? RetryEcho { get; init; }
    public CompiledSeriesReference? RetryEchoRef { get; init; }
    public string? RetryBudgetRemaining { get; init; }
    public CompiledSeriesReference? RetryBudgetRemainingRef { get; init; }
    public IReadOnlyList<double>? RetryKernel { get; init; }
    public string? ExternalDemand { get; init; }
    public CompiledSeriesReference? ExternalDemandRef { get; init; }
    public string? QueueDepth { get; init; }
    public CompiledSeriesReference? QueueDepthRef { get; init; }
    public string? Capacity { get; init; }
    public CompiledSeriesReference? CapacityRef { get; init; }
    public string? ParallelismRawText { get; init; }
    public CompiledParallelismReference? ParallelismRef { get; init; }
    public string? ProcessingTimeMsSum { get; init; }
    public CompiledSeriesReference? ProcessingTimeMsSumRef { get; init; }
    public string? ServedCount { get; init; }
    public CompiledSeriesReference? ServedCountRef { get; init; }
    public double? SlaMinutes { get; init; }
    public double? MaxAttempts { get; init; }
    public string? BackoffStrategy { get; init; }
    public string? ExhaustedPolicy { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}
