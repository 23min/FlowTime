# TT‑M‑03.31 — End‑to‑End Fixtures, Goldens, and Documentation (Retries + Service Time)

Status: Planned  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Close out the “Retry + Service Time” epic with production‑quality fixtures, golden tests for API contracts, UI tests, and documentation. Ensure examples clearly show how retries impact dependencies and how service time affects node coloring.

## Goals

- Provide reproducible fixtures/templates that exercise retries and processing time.  
- Lock API contracts via golden responses.  
- Update operator and architecture docs (contracts, thresholds, examples).  
- Wrap with a validation checklist and a how‑to for demo scenarios.

## Scope

In Scope
- Fixtures: microservices example with tunable retry probability and processing times.  
- API: golden snapshots for `/state` and `/state_window` including `edges` and `serviceTimeMs`.  
- UI: unit/integration tests for overlays, basis switching, and inspector series.

Out of Scope
- Performance benchmarks beyond existing targets (track regressions only).

## Deliverables
1) Example fixture bundle(s) under `fixtures/` with README.  
2) Golden tests for API contracts (state and window).  
3) UI tests for overlays + S basis + inspector retries.  
4) Docs: milestone updates, roadmap references, contract tables, operator how‑to.

## Option B — API Edges Slice in `/state_window` (Spec)

Context: TT‑M‑03.30 implements edge overlays via client‑side derivation (Option A). TT‑M‑03.31 will add an optional `edges` slice to `/state_window` so clients (UI/CLI/others) can consume consistent, server‑computed edge metrics without duplicating logic.

### Contract Changes (Additive)
- Endpoint: `GET /v1/runs/{runId}/state_window?startBin&endBin[&mode=operational|full][&include=edges|all]`
  - New query parameter `include` controls optional payload sections. When `include=edges` or `include=all`, the response includes `edges`.
- DTO: extend `StateWindowResponse` with optional `Edges` collection.
  - File: `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`
  - Add new type `EdgeSeries` and property `IReadOnlyList<EdgeSeries>? Edges { get; init; }`
  - Backward compatible: property omitted when not requested.
- Schema: update `docs/schemas/time-travel-state.schema.json` to allow optional `edges: []` alongside `nodes: []`.

### Payload Shape (EdgeSeries)
Mirror `NodeSeries` for consistency; include minimal metadata required for overlays.

```
edges: [
  {
    id: string,            // e.g., "A->B:attempts" (stable)
    from: string,          // upstream node id
    to: string,            // downstream node id
    edgeType: "dependency",// dependency only (omit topology layout edges)
    field: "attempts"|"failures",
    multiplier: number?,    // from model (default 1)
    lag: number?,           // bins (default 0)
    series: {
      attemptsLoad?: number?[], // upstream attempts shifted by lag and scaled by multiplier
      failuresLoad?: number?[], // upstream failures shifted by lag and scaled by multiplier (or errors)
      retryRate?: number?[]     // failures/attempts at (bin−lag); null on divide‑by‑zero
    }
  }
]
```

Notes
- Only dependency edges for retry‑relevant fields (`attempts`, `failures`) are included to keep payloads lean.
- `retryRate` is derived from upstream raw series (after lag) and is independent of `multiplier` (rate invariant to scaling).
- Window alignment matches `nodes`: arrays have length `Window.BinCount` and respect `startBin`/`endBin`.

### Semantics & Aggregation
- Source series:
  - `attempts`: prefer `data.Attempts`; otherwise derive `served + failures` (with `failures = data.Failures ?? data.Errors`).
  - `failures`: prefer `data.Failures`; otherwise use `data.Errors`.
- Transform per edge:
  - Apply integer lag L: sample source at `index = bin − L` (out‑of‑range ⇒ null).
  - Apply multiplier M: scale `attemptsLoad = attempts × M`, `failuresLoad = failures × M` (M defaults to 1 when omitted).
  - Compute `retryRate = failures / attempts` at the lagged index; when attempts ≤ 0 ⇒ null.

### Implementation Plan (API)
1) Contracts
   - Add `EdgeSeries` DTO (`Id`, `From`, `To`, `EdgeType`, `Field`, `Multiplier`, `Lag`, `Series: IDictionary<string, double?[]>`).
   - Add optional `Edges` to `StateWindowResponse`.
2) Enumeration of dependency edges (share logic)
   - Factor a small internal helper used by both `GraphService` and `StateQueryService` to enumerate dependency edges with `field`, `multiplier`, `lag`.
   - Files: `src/FlowTime.API/Services/GraphService.cs` (edge enumeration), `src/FlowTime.API/Services/StateQueryService.cs` (consumer).
3) Series computation
   - In `StateQueryService.GetStateWindowAsync(...)`, when `include` requests edges, build `EdgeSeries` for retry‑relevant edges using node `NodeData` already loaded.
   - Respect existing `maxWindowBins` and validation. Guard divide‑by‑zero and NaN/Infinity.
4) Schema + Examples
   - Update `docs/schemas/time-travel-state.schema.json` with `edges` definition.
   - Add `.http` example under `src/FlowTime.API/FlowTime.API.state.http` to demonstrate `include=edges`.

### Performance & Controls
- Opt‑in via `include=edges` to avoid payload bloat for consumers that do not need edges.
- Keep existing `maxWindowBins` (500) enforcement; edge computation work is O(E × bins) and bounded by the requested slice.
- Return only dependency edges with fields in `{ attempts, failures }`.

### Tests (API)
- Snapshot/schema tests for both shapes (with and without `edges`).
  - Update: `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs`
  - Update/add: `tests/FlowTime.Api.Tests/StateEndpointTests.cs` (goldens with `include=edges`)
- Unit tests for lag/multiplier application and retryRate guards.

### UI Integration (Optional Pivot)
- UI for 03.30 continues using client derivation (Option A).
- For 03.31, add a feature flag to let the UI consume server‑computed `edges` when present; fallback to client derivation when absent.
- No breaking changes in UI contracts; legend/thresholds unchanged.

### Acceptance Criteria (Edges Slice)
- AC‑Edges‑1: `/state_window` includes `edges` only when `include=edges|all` is provided; default response unchanged.
- AC‑Edges‑2: For retry‑enabled fixtures, `edges[].series.retryRate` matches `failures/attempts` (with guards), and `attemptsLoad` reflects multiplier/lag.
- AC‑Edges‑3: Schema validation passes for both response shapes.

## Acceptance Criteria
- AC1: `dotnet test` passes all new goldens/UI tests (outside known unrelated perf skips).  
- AC2: Example can be generated and viewed in the UI; overlays/basis switch work.  
- AC3: Docs present clear examples and thresholds with a demo script.

## Implementation Plan (Sessions)

Session 1 — Fixtures + README  
- Finalize example series; add README and CLI to generate artifacts.

Session 2 — Golden Tests  
- Add snapshots pinning `/state` and `/state_window` shapes and key values.  
- Include edge retry series and node service time fields.

Session 3 — UI Tests  
- Toggle overlays; verify legend; verify S basis; hover/selection link tests.  
- Basic snapshot of inspector retries.

Session 4 — Docs + Sign‑off  
- Architecture contracts; operator guide; screenshots; finalize milestone status.

## Risks & Mitigations
- Flaky goldens due to RNG → seed deterministically; document seeds.  
- Payload bloat → slice windows; avoid full‑range defaults in tests.

## References
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md  
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/operations/telemetry-capture-guide.md  
- docs/development/milestone-documentation-guide.md
