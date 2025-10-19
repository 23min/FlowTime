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
/// Deterministic manifest from manifest.json (contains hashes, RNG seed, integrity data)
/// </summary>
public sealed class DeterministicManifest
{
    public required int SchemaVersion { get; init; }
    public required string ScenarioHash { get; init; }
    public required RngInfo Rng { get; init; }
    public required Dictionary<string, string> SeriesHashes { get; init; }
    public required int EventCount { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public string? ModelHash { get; init; }
    public ManifestProvenance? Provenance { get; init; }
}

public sealed class ManifestProvenance
{
    public bool? HasProvenance { get; init; }
    public string? ModelId { get; init; }
    public string? TemplateId { get; init; }
    public string? Source { get; init; }
}

/// <summary>
/// RNG information from manifest.json
/// </summary>
public sealed class RngInfo
{
    public required string Kind { get; init; }
    public required int Seed { get; init; }
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
public readonly record struct TimeGrid(int Bins, int BinSize, string BinUnit, string Timezone, string Align)
{
    /// <summary>
    /// Computed bin duration in minutes (for backward compatibility)
    /// </summary>
    public int BinMinutes => BinUnit.ToLowerInvariant() switch
    {
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        _ => throw new ArgumentException($"Unknown time unit: {BinUnit}")
    };
}
