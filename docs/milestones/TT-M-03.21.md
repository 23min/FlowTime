# TT‑M‑03.21 — Graph + Metrics endpoints (UI Phase 2 enablement)

**Status:** Completed (2025-10-24)  
**Reach:** FlowTime API v1 — time-travel helpers for UI Phase 2  
**Branch:** feature/time-travel-ui-m3

Purpose
- Provide small, focused backend support that unblocks UI M3 Phase 2 with minimal friction: a graph endpoint that derives topology from canonical artifacts, and a minimal metrics endpoint (or artifact) used by the SLA dashboard.

Scope
- Add `GET /v1/runs/{runId}/graph` that reads the run’s `model/model.yaml` and returns a concise JSON graph (nodes, edges, semantics, optional ui hints).
- Add `GET /v1/runs/{runId}/metrics` (minimal) that computes SLA aggregates and a compact mini‑bar sparkline over a requested bin slice using existing state/series data. As a fallback, support writing `metrics.json` during run creation.

Why now
- UI M3 Phase 2 depends on a stable way to fetch graph topology and SLA tiles without reparsing YAML or reproducing backend logic in the browser. Keeping logic server‑side reduces coupling and accelerates delivery.

Deliverables
- API: `GET /v1/runs/{runId}/graph`
  - Response (shape):
    - `nodes[]`: `{ id, kind, semantics: { arrivals, served, errors, queue, capacity }, ui?: { x, y } }`
    - `edges[]`: `{ id, from, to, weight }`
  - Derives from `model/model.yaml` written by orchestration.
- API: `GET /v1/runs/{runId}/metrics?startBin={s}&endBin={e}`
  - Response (shape):
    - `window`: `{ start, timezone }`
    - `grid`: `{ binMinutes, bins }`
    - `flows[]` (or `services[]`): `{ id, slaPct, binsMet, binsTotal, mini: number[] }`
  - SLA definition: percentage of bins where service meets threshold (served/arrivals ≥ target or latency ≤ SLA), using semantics when present; keep threshold rules simple and documented.
- Docs: contracts in `docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md` and UI mapping notes in `docs/architecture/time-travel/ui-m3-roadmap.md`.
- Tests:
  - Unit: graph extraction from model; metrics aggregator correctness.
  - Integration: both endpoints against canonical runs (microservices, network‑reliability); error cases (run missing, invalid ranges).

Acceptance Criteria
- `GET /v1/runs/{runId}/graph` returns 200 with nodes/edges/semantics parsed from `model.yaml`; 404 for missing run.
- `GET /v1/runs/{runId}/metrics` returns 200 with `flows[].slaPct`, `binsMet`, `binsTotal`, `mini[]` over requested range; 400 for invalid or inverted ranges.
- UI consumers can call both endpoints without parsing YAML or recomputing SLA locally.

Design Notes
- Graph endpoint may reuse existing parsing utilities already used by `/graph` but wired to read from stored models.
- Metrics endpoint may implement a minimal evaluator:
  - Window: map `startBin..endBin` to timestamps using existing grid metadata.
  - Per node: if semantics include `arrivals/served`, compute a simple SLA% (e.g., `served/arrivals ≥ 0.95`) and build a normalized mini‑bar from served/arrivals ratios (length 12 or 24 by bucket).
  - If semantics include `queue` and `slaMin`, compute latency and compare to SLA.
  - Return the higher‑level flow/service view; start simple and document thresholds.

Files to Modify
- `src/FlowTime.API/Program.cs` — map new routes.
- `src/FlowTime.API/Services/GraphService.cs` (NEW) — derive graph from `model.yaml`.
- `src/FlowTime.API/Services/MetricsService.cs` (NEW) — compute aggregates from series or `state_window`.
- `docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md` — add response shapes.
- `docs/architecture/time-travel/ui-m3-roadmap.md` — reference endpoints and confirm Phase 2 wiring.

## Implementation Summary

- Added dedicated `GraphService` that reads `model/model.yaml`, maps semantics (including `queueDepth ⇒ queue`) and returns the concise node/edge contract consumed by the UI.
- Added `MetricsService` that reuses `StateQueryService` windows, applies the documented SLA rule (`served / arrivals ≥ 0.95`), and emits normalized mini-bar arrays.
- Registered both services/endpoints in `Program.cs` (`GET /v1/runs/{runId}/graph`, `GET /v1/runs/{runId}/metrics`) with consistent 4xx handling for missing runs and malformed queries.
- Persisted `metrics.json` alongside each run (same shape as the metrics endpoint) so file-based adapters remain in sync without recomputing SLA aggregates.
- Added a resolver that evaluates the stored model when semantics reference other node ids, enabling `/metrics` (and the emitted `metrics.json`) for simulation-only runs that do not expose `file:` URIs.
- Extended API integration coverage with new tests for graph happy-path/404 and metrics happy-path/query validation.
- Updated architecture docs and the Phase 2 roadmap to reference the new contracts, and added `.http` samples for manual verification.

## Test & Validation Notes

- `dotnet test FlowTime.sln --nologo --verbosity:minimal` (local) — full suite passes after standard warm-up; new integration tests (including golden snapshots) run in ~9s.
- Golden snapshots (`tests/FlowTime.Api.Tests/Golden/*.json`) now lock the graph and metrics contracts to catch accidental schema drift.
- `metrics.json` artifacts mirror the endpoint payload and are written during run creation for offline consumers.

## Follow-ups

- Monitor `metrics.json` size/shape as Phase 2 adds more services; consider lightweight downsampling if responses grow beyond UI needs.

Risks & Mitigations
- Ambiguous SLA rules → document simple defaults; keep overridable later.
- Large ranges → cap `endBin-startBin` to a reasonable size or downsample for `mini`.
- Schema drift → add golden tests to lock endpoint shapes.

Out of Scope
- Advanced overlays, forecasts, or flow grouping across multiple runs.
- Advanced overlays, forecasts, or flow grouping across multiple runs.

Timeline
- Duration: 2–3 days of focused backend work with tests and docs.
