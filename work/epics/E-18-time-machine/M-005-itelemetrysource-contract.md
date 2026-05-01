---
id: M-005
title: ITelemetrySource Contract
status: done
parent: E-18
---

## Goal

Define `ITelemetrySource` as the formal input contract for the Time Machine's external data
surface, with two concrete implementations from day one.  Satisfies the deferred portion of
the spec's m-E18-01b scope (the tiered-validation half shipped as M-003; this delivers
the source-contract half).

## Scope

**`ITelemetrySource` interface** — in `src/FlowTime.TimeMachine/Telemetry/`:
- `ITelemetrySource` — single method: `Task<TelemetryData> ReadAsync(CancellationToken)`
- `TelemetryData` — typed payload: grid, series dictionary, optional provenance metadata

**`CanonicalBundleSource : ITelemetrySource`** — reads the canonical bundle format
(`manifest.json` + CSV series files) written by the existing `TelemetryBundleBuilder` in
`FlowTime.Core`.  Concrete class (not behind a second interface).

**`FileCsvSource : ITelemetrySource`** — reads `file:`-referenced CSV inputs, extracting
the existing file-read logic already in `FlowTime.Core` into a named, injectable
implementation.

**In scope:**
- `src/FlowTime.TimeMachine/Telemetry/ITelemetrySource.cs`
- `src/FlowTime.TimeMachine/Telemetry/TelemetryData.cs`
- `src/FlowTime.TimeMachine/Telemetry/CanonicalBundleSource.cs`
- `src/FlowTime.TimeMachine/Telemetry/FileCsvSource.cs`
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Telemetry/`

**Out of scope:**
- `ITelemetrySink` — explicitly deferred per D-033
- Real-world format adapters (Prometheus, OTEL, BPI) — m-E18 telemetry adapters milestone
- Time Machine `Evaluate` / `Reevaluate` consuming the source — separate milestone
- HTTP endpoint changes

## Contract

### `ITelemetrySource`

```csharp
namespace FlowTime.TimeMachine.Telemetry;

/// <summary>
/// Input contract for external data fed into the Time Machine.
/// Each implementation snapshots data from its source at ReadAsync time,
/// returning a deterministic TelemetryData payload the Time Machine can consume.
/// </summary>
public interface ITelemetrySource
{
    Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default);
}
```

### `TelemetryData`

```csharp
public sealed class TelemetryData
{
    /// <summary>Grid definition (bins, binSize, binUnit).</summary>
    public required GridDefinition Grid { get; init; }

    /// <summary>Node-id → double[] series values (one per bin).</summary>
    public required IReadOnlyDictionary<string, double[]> Series { get; init; }

    /// <summary>Optional provenance: source path, captured-at timestamp, content hash.</summary>
    public TelemetryProvenance? Provenance { get; init; }
}

public sealed class TelemetryProvenance
{
    public string? SourcePath { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public string? ContentHash { get; init; }
}
```

### `CanonicalBundleSource`

Reads a canonical bundle directory (containing `manifest.json` and `series/*.csv`).

```csharp
public sealed class CanonicalBundleSource : ITelemetrySource
{
    public CanonicalBundleSource(string bundleDirectory) { ... }
    public Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default) { ... }
}
```

### `FileCsvSource`

Reads a single CSV file as a named series.

```csharp
public sealed class FileCsvSource : ITelemetrySource
{
    /// <param name="filePath">Path to the CSV file.</param>
    /// <param name="seriesId">Node ID to assign the series to.</param>
    /// <param name="grid">Grid definition to validate series length against.</param>
    public FileCsvSource(string filePath, string seriesId, GridDefinition grid) { ... }
    public Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default) { ... }
}
```

## Acceptance Criteria

- [x] `ITelemetrySource` interface exists in `FlowTime.TimeMachine.Telemetry`
- [x] `TelemetryData` carries Grid + Series + optional Provenance
- [x] `CanonicalBundleSource.ReadAsync` reads a bundle directory and returns correct series values
- [x] `FileCsvSource.ReadAsync` reads a single CSV and returns the series under the specified ID
- [x] Both implementations compile and have passing unit tests (23 tests across 2 suites)
- [x] `ITelemetrySink` is **not** introduced (explicitly documented as deferred)
- [x] `rg "FlowTime\.Generator" src/ tests/` still zero (no regressions)
- [x] `dotnet test FlowTime.sln` all green (72 TimeMachine tests, 0 failures)
