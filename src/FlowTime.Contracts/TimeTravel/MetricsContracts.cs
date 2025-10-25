using System;
using System.Collections.Generic;

namespace FlowTime.Contracts.TimeTravel;

public sealed class MetricsResponse
{
    public required MetricsWindow Window { get; init; }
    public required MetricsGrid Grid { get; init; }
    public IReadOnlyList<ServiceMetrics> Services { get; init; } = Array.Empty<ServiceMetrics>();
}

public sealed class MetricsWindow
{
    public DateTimeOffset? Start { get; init; }
    public string? Timezone { get; init; }
}

public sealed class MetricsGrid
{
    public int BinMinutes { get; init; }
    public int Bins { get; init; }
}

public sealed class ServiceMetrics
{
    public string Id { get; init; } = string.Empty;
    public double SlaPct { get; init; }
    public int BinsMet { get; init; }
    public int BinsTotal { get; init; }
    public IReadOnlyList<double> Mini { get; init; } = Array.Empty<double>();
}
