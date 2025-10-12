using System;

namespace FlowTime.Contracts;

public sealed class StateResponse
{
    public string RunId { get; init; } = string.Empty;
    public string? Mode { get; init; }
    public StateWindowInfo Window { get; init; } = new();
    public StateGridInfo Grid { get; init; } = new();
    public StateBinInfo Bin { get; init; } = new();
    public Dictionary<string, NodeState> Nodes { get; init; } = new();
}

public sealed class StateWindowResponse
{
    public string RunId { get; init; } = string.Empty;
    public string? Mode { get; init; }
    public StateWindowInfo Window { get; init; } = new();
    public StateGridInfo Grid { get; init; } = new();
    public StateSliceInfo Slice { get; init; } = new();
    public IReadOnlyList<DateTime?> Timestamps { get; init; } = Array.Empty<DateTime?>();
    public Dictionary<string, NodeWindowSeries> Nodes { get; init; } = new();
}

public sealed class StateWindowInfo
{
    public DateTime? Start { get; init; }
    public string Timezone { get; init; } = "UTC";
}

public sealed class StateGridInfo
{
    public int Bins { get; init; }
    public int BinSize { get; init; }
    public string BinUnit { get; init; } = "minutes";
    public double BinMinutes { get; init; }
}

public sealed class StateBinInfo
{
    public int Index { get; init; }
    public DateTime? StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
}

public sealed class StateSliceInfo
{
    public int StartBin { get; init; }
    public int EndBin { get; init; }
    public int Bins { get; init; }
}

public sealed class NodeState
{
    public string Kind { get; init; } = "service";
    public double? Arrivals { get; init; }
    public double? Served { get; init; }
    public double? Errors { get; init; }
    public double? Queue { get; init; }
    public double? ExternalDemand { get; init; }
    public double? Capacity { get; init; }
    public double? Utilization { get; init; }
    public double? LatencyMinutes { get; init; }
    public double? SlaMinutes { get; init; }
    public string Color { get; init; } = "gray";
}

public sealed class NodeWindowSeries
{
    public string Kind { get; init; } = "service";
    public Dictionary<string, double?[]> Series { get; init; } = new();
}
