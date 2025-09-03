namespace FlowTime.Adapters.Synthetic;

/// <summary>
/// Series index from series/index.json
/// </summary>
public sealed class SeriesIndex
{
    public required int SchemaVersion { get; init; }
    public required TimeGrid Grid { get; init; }
    public required SeriesMetadata[] Series { get; init; }
}

/// <summary>
/// Individual series metadata from series/index.json
/// </summary>
public sealed class SeriesMetadata
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Path { get; init; }
    public required string Unit { get; init; }
    public required string ComponentId { get; init; }
    public required string Class { get; init; }
    public required int Points { get; init; }
    public required string Hash { get; init; }
}
