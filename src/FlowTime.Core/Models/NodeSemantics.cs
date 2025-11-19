using System.Collections.Generic;

using System.Collections.Generic;

namespace FlowTime.Core.Models;

public sealed record NodeSemantics
{
    public required string Arrivals { get; init; }
    public required string Served { get; init; }
    public required string Errors { get; init; }
    public string? Attempts { get; init; }
    public string? Failures { get; init; }
    public string? ExhaustedFailures { get; init; }
    public string? RetryEcho { get; init; }
    public string? RetryBudgetRemaining { get; init; }
    public IReadOnlyList<double>? RetryKernel { get; init; }
    public string? ExternalDemand { get; init; }
    public string? QueueDepth { get; init; }
    public string? Capacity { get; init; }
    public string? ProcessingTimeMsSum { get; init; }
    public string? ServedCount { get; init; }
    public double? SlaMinutes { get; init; }
    public double? MaxAttempts { get; init; }
    public string? BackoffStrategy { get; init; }
    public string? ExhaustedPolicy { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public IReadOnlyDictionary<string, string>? Aliases { get; init; }
}
