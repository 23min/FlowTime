# TT‑M‑03.31 — End‑to‑End Fixtures, Goldens, and Documentation (Retries + Service Time)

Status: Completed  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Close out the “Retry + Service Time” epic with production‑quality fixtures, golden tests for API contracts, UI tests, and documentation. Ensure examples clearly show how retries impact dependencies and how service time affects node coloring.

## Summary (May 2025)

- Added a deterministic replay fixture under `fixtures/time-travel/retry-service-time/` (CSV series, model, README). Harnessed in API tests/UI tests so anyone can recreate the run locally (`FLOWTIME_DATA_DIR=/path/to/fixture dotnet test`).
- `/v1/runs/{runId}/state_window` now always emits server-computed retry edge series (attempts/failures/retryRate with multiplier/lag). The UI consumes those slices directly; client-side derivation has been fully removed.
- Golden snapshots cover the edge slice (`state-window-edges-approved.json`) and schema tests assert both shapes. UI tests verify retry overlays consume server edges.
- Docs updated (this milestone, architecture retry section, roadmap) with contract tables, fixture instructions, and demo checklist.

## Validation

1. Generated the retry fixture (`cp -r fixtures/time-travel/retry-service-time data/runs/run_state_fixture/model`, run metadata via test harness) and inspected via UI/topology page with overlays enabled (retry legend shows server metrics at bin 1).
2. `dotnet test FlowTime.sln` — PASS (perf baseline skips still present). API `StateEndpointTests.GetStateWindow_IncludesRetryEdgesWhenRequested` validates golden; schema tests validate edges slice. UI tests confirm `CanvasRenderRequest.EdgeSeries`.
3. Demo checklist (operator view):
   - Load topology page with run `run_state_edges`.
   - Toggle Retry overlay; verify legends/labels match golden values (`attemptsLoad[1]=20`, `retryRate[1]=0.1`).
   - Switch node color basis to Service Time and inspect TTL in inspector.

## What’s Next

- Expand fixtures with captured provenance + CLI instructions if additional replay scenarios are needed.

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

Context: TT‑M‑03.30 implements edge overlays via client‑side derivation (Option A). TT‑M‑03.31 adds a server-provided `edges` slice to `/state_window` so clients (UI/CLI/others) can consume consistent edge metrics without duplicating logic.

### Contract Changes (Additive)
- Endpoint: `GET /v1/runs/{runId}/state_window?startBin&endBin[&mode=operational|full]` always returns an `edges` slice (empty array when no retry dependencies exist).
- DTO: extend `StateWindowResponse` with optional `Edges` collection.
  - File: `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`
  - Add new type `EdgeSeries` and property `IReadOnlyList<EdgeSeries> Edges { get; init; }`
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
   - In `StateQueryService.GetStateWindowAsync(...)`, always build `EdgeSeries` for retry‑relevant edges using node `NodeData` already loaded.
   - Respect existing `maxWindowBins` and validation. Guard divide‑by‑zero and NaN/Infinity.
4) Schema + Examples
   - Update `docs/schemas/time-travel-state.schema.json` with `edges` definition.
   - Add `.http` example under `src/FlowTime.API/FlowTime.API.state.http` highlighting the retry edge payload (no extra query parameters required).

### Performance & Controls
- Edge series ship by default; keep existing `maxWindowBins` (500) enforcement so edge computation stays bounded at O(E × bins).
- Return only dependency edges with fields in `{ attempts, failures }`.

### Tests (API)
- Snapshot/schema tests for both shapes (with and without `edges`).
  - Update: `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs`
- Update/add: `tests/FlowTime.Api.Tests/StateEndpointTests.cs` (goldens verifying default `edges` payload)
- Unit tests for lag/multiplier application and retryRate guards.

### UI Integration
- UI renders retry overlays solely from server‑computed edge series (no additional query flags required).
- No breaking changes in UI contracts; legend/thresholds unchanged.

### Acceptance Criteria (Edges Slice)
- AC‑Edges‑1: `/state_window` always includes an `edges` collection (empty array when no retry dependencies exist).
- AC‑Edges‑2: For retry‑enabled fixtures, `edges[].series.retryRate` matches `failures/attempts` (with guards), and `attemptsLoad` reflects multiplier/lag.
- AC‑Edges‑3: Schema validation passes for both response shapes.
- AC‑Edges‑4: UI overlays consume server `edges` and client-side retry derivation is removed after parity validation.

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
- Consume server `edges` slice for retries (remove client derivation after parity).

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
