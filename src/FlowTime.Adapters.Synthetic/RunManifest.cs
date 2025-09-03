namespace FlowTime.Adapters.Synthetic;

/// <summary>
/// High-level run summary from run.json
/// </summary>
public sealed class RunManifest
{
    public required int SchemaVersion { get; init; }
    public required string RunId { get; init; }
    public required string EngineVersion { get; init; }
    public required string Source { get; init; }
    public required TimeGrid Grid { get; init; }
    public string? ModelHash { get; init; }
    public required string ScenarioHash { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required string[] Warnings { get; init; }
    public required SeriesReference[] Series { get; init; }
}

/// <summary>
/// Series reference from run.json
/// </summary>
public sealed class SeriesReference
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Unit { get; init; }
}

/// <summary>
/// Time grid specification
/// </summary>
public readonly record struct TimeGrid(int Bins, int BinMinutes, string Timezone, string Align);
