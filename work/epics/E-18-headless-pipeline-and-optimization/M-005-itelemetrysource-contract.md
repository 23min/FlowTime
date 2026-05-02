---
id: M-005
title: ITelemetrySource Contract
status: done
parent: E-18
acs:
  - id: AC-1
    title: ITelemetrySource interface exists in FlowTime.TimeMachine.Telemetry
    status: met
  - id: AC-2
    title: TelemetryData carries Grid + Series + optional Provenance
    status: met
  - id: AC-3
    title: CanonicalBundleSource.ReadAsync reads a bundle directory and returns
    status: met
  - id: AC-4
    title: FileCsvSource.ReadAsync reads a single CSV and returns the series
    status: met
  - id: AC-5
    title: Both implementations compile and have passing unit tests (23 tests
    status: met
  - id: AC-6
    title: ITelemetrySink is not introduced (explicitly documented as deferred)
    status: met
  - id: AC-7
    title: rg "FlowTime\.Generator" src/ tests/ still zero (no regressions)
    status: met
  - id: AC-8
    title: dotnet test FlowTime.sln all green (72 TimeMachine tests, 0 failures)
    status: met
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

## Acceptance criteria

### AC-1 — ITelemetrySource interface exists in FlowTime.TimeMachine.Telemetry

`ITelemetrySource` interface exists in `FlowTime.TimeMachine.Telemetry`
### AC-2 — TelemetryData carries Grid + Series + optional Provenance

`TelemetryData` carries Grid + Series + optional Provenance
### AC-3 — CanonicalBundleSource.ReadAsync reads a bundle directory and returns

`CanonicalBundleSource.ReadAsync` reads a bundle directory and returns correct series values
### AC-4 — FileCsvSource.ReadAsync reads a single CSV and returns the series

`FileCsvSource.ReadAsync` reads a single CSV and returns the series under the specified ID
### AC-5 — Both implementations compile and have passing unit tests (23 tests

Both implementations compile and have passing unit tests (23 tests across 2 suites)
### AC-6 — ITelemetrySink is not introduced (explicitly documented as deferred)

`ITelemetrySink` is **not** introduced (explicitly documented as deferred)
### AC-7 — rg "FlowTime\.Generator" src/ tests/ still zero (no regressions)

`rg "FlowTime\.Generator" src/ tests/` still zero (no regressions)
### AC-8 — dotnet test FlowTime.sln all green (72 TimeMachine tests, 0 failures)

`dotnet test FlowTime.sln` all green (72 TimeMachine tests, 0 failures)
