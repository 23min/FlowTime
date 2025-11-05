# TT‑M‑03.27 — Queues First‑Class (Backlog + Latency; No Retries)

Status: Completed  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Make queues first‑class citizens in the Time‑Travel system: model them explicitly in topology, compute/display backlog (queue depth) and queue latency (Little’s Law) without introducing retries or service time yet. This milestone spans API + UI and delivers immediate, intuitive visuals for queues while preserving the current “no‑oldest_age” constraint.

## Goals

- Introduce explicit queue nodes (kind=queue) in example templates and topology, with arrivals/served/queue semantics and explicit SHIFT initial.
- Ensure API responses include queue depth and queue latency per bin (derive latency iff served > 0; otherwise null).
- Render queue nodes on the canvas and inspector as first‑class elements:
  - Canvas: queue node as a rectangular glyph; toggle to show a scalar badge for the current bin’s queue depth.
  - Inspector: stacked charts for Queue depth, Latency, Arrivals, Served, each with horizon overview (consistent with service nodes).
- Clarify the “Queue” focus metric semantics. Queue basis remains valid and becomes the default basis for queue nodes (opt‑in via UI as needed).

## Scope

In Scope
- Template updates (warehouse 1d/5m) to insert a real queue node between upstream and downstream services; no new template file needed.
- API: compute `latencyMinutes` from `queue` and `served` and include it in `/state_window` for queue nodes; null when not computable.
- UI:
  - Canvas glyph for queue nodes (rectangle), with a feature toggle to display a scalar badge for queue depth at bin(t).
  - Inspector charts for queue nodes (Queue depth, Latency, Arrivals, Served) with horizon under each chart.
  - Make “Queue” focus metric available and sensible when queue nodes are present.

Out of Scope
- Retries, backoff, dependency attempts/success/failure metrics.
- Service time S (requires processing_time_sum + served_count, or in_service_count).
- Oldest age telemetry/visuals.

## Requirements

### Functional
- TTF1 — Topology + Templates
  - Example template (warehouse 1d/5m) contains a queue node `DistributorQueue` with:
    - arrivals: `queue_inflow`
    - served: `queue_outflow`
    - queue: `queue_depth`
  - `queue_depth := MAX(0, SHIFT(queue_depth, 1) + queue_inflow − queue_outflow)` with `initial: 0`.
  - Edges rerouted: `Warehouse:out → DistributorQueue:in → Distributor:in`.
- TTF2 — API contracts
  - `/v1/runs/{id}/state_window` includes `latencyMinutes` for queue nodes: `(queue/served) * binMinutes` when `served > 0`, else null.
  - No changes to request shapes; latency is derived server‑side if absent in the stored series.
- TTF3 — UI rendering
  - Canvas renders queue nodes as rectangles, visually distinct from service circles.
  - Feature toggle to show a scalar queue depth badge at the current bin(t) on the queue node.
  - Inspector renders stacked sparklines for Queue depth, Latency, Arrivals, Served; each includes horizon context.
  - “Queue” focus metric remains available; for queue nodes it becomes the sensible default basis (no change required for services).

### Non‑Functional
- NFR1 — Performance: queue inspector redraw ≤ 8 ms on reference graph (20 nodes) while scrubbing.
- NFR2 — Accessibility: inspector charts and queue badges have aria labels; keyboard focus order preserved; color palette passes contrast.

## Deliverables

1) Template updates (warehouse 1d/5m): queue node and series as above.  
2) API latency computation for queue nodes; `/state_window` returns `latencyMinutes`.  
3) UI canvas glyph for queue nodes + toggle for scalar queue depth.  
4) Inspector queue stack + horizons; “Queue” focus metric guidance in UI copy.  
5) Docs: example YAML, telemetry contract snippet, updated roadmap “Deferred” entries (plus architecture note linked above).  
   - Performance run log (central): `docs/performance/perf-log.md`.
6) Tests: unit + golden updates.  
7) Architecture note capturing SHIFT-based queue depth handling: `docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md`.

## Acceptance Criteria

- AC1: Example run loads; Topology shows a queue node rectangle between Warehouse and Distributor.
- AC2: Inspector for the queue node displays Queue depth, Latency, Arrivals, Served with horizons; queue latency matches Little’s Law within rounding.
- AC3: Canvas toggle shows a scalar queue depth badge for the current bin on queue nodes.
- AC4: “Queue” focus metric is meaningful and selectable; heat/color reacts to queue depth as expected.
- AC5: `/state_window` payload for queue nodes includes `latencyMinutes` (null when `served == 0`).
- AC6: UI + API tests green; performance and a11y checks pass.

## Completion Summary

- Queue node is precomputed during artifact generation, binding `semantics.queue` to the emitted `queue_depth` CSV; architecture note published at `docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md`.
- API `/state_window` response validated with new golden (`state-window-queue-null-approved.json`) covering the served=0 latency case, alongside unit tests for queue latency derivation.
- UI canvas/inspector updates include badge toggle persistence, queue-chip layout, and inspector coverage tests; accessibility/performance targets observed.
- Docs, roadmap deferrals, and centralized performance log (`docs/performance/perf-log.md`) updated; full solution test suite executed (`dotnet test tests/FlowTime.Tests -c Release --no-build`).

## Implementation Plan (Sessions)

Session 1 — Templates + Topology
- Add `queue_inflow`, `queue_outflow`, `queue_depth` (with `initial`) and `DistributorQueue` node to `templates/supply-chain-multi-tier-warehouse-1d5m.yaml`.
- Reroute edges Warehouse→DistributorQueue→Distributor.
- Provide sample run artifacts; validate conservation (served ≤ arrivals) still holds.
- Precompute true queue depth at artifact generation so `semantics.queue` references the emitted CSV (see architecture note).

Session 2 — API Latency Derivation
- In `/state_window` builder, when a node.kind == `queue`, derive `latencyMinutes = (queue/served) * binMinutes` with guards; include in response. (Implemented; additional golden captured for zero-served bins.)
- Update goldens where applicable (state-window + queue latency null case).

Session 3 — UI Canvas Glyph + Toggle
- Render queue nodes as rectangles (existing canvas layer); add feature toggle to show scalar queue depth badge at bin(t).
- Ensure tooltip flip and hitboxes respect the new glyph shape.

Session 4 — Inspector Charts + Horizons
- Ensure queue nodes populate inspector stacks for `queue`, `latencyMinutes`, `arrivals`, `served` (Topology.razor already supports queue stacks; verify binding).
- Horizons render under each chart (already implemented); ensure aria labels/labels mention “Queue/Latency/Arrivals/Served”.

Session 5 — Docs + Tests + Roadmap
- Update docs with template snippet and telemetry contract for queue nodes.  
- Add UI unit tests for queue stacks; API tests for latency inclusion.  
- Update `docs/architecture/time-travel/time-travel-planning-roadmap.md` with a “Deferred” section (retries, service time S, oldest_age, edge overlays, queue-depth fallback retriever) and note TT‑M‑03.27 delivery.

## Testing Strategy

- API tests: latencyMinutes inclusion/guards; queue node payload completeness.
- UI tests: inspector shows 4 queues series; color basis `Queue` affects stroke/fill; scalar badge toggle on canvas.
- Integration: load example run; scrub across bins; verify latency drops to null when served==0.
- Golden updates: orchestration/`state_window` goldens updated for queue latency.

## Telemetry Contract (per bin)

Required (queues):
- `queue` (queue_depth)  
- `arrivals` (arrivals_to_queue)  
- `served` (arrivals_to_service)  
- `binMinutes` (from grid)  

Derived (API):
- `latencyMinutes = (queue/served) * binMinutes` when `served > 0`, else null

Not included (deferred):
- Retries/backoff, processing_time_sum/served_count (service time S), oldest_age

## Risks & Mitigations

- Missing queue series in templates → Provide example template updates; graceful placeholders in UI.  
- Latency spikes when served≈0 → Guard and null out latency; explain in inspector label.  
- Visual clutter with scalar badge → Feature toggle (off by default for large graphs).

## Open Questions

- Should `Queue` focus metric auto‑select when a queue node is focused? (Default = leave as user choice; consider auto‑hint.)
- Should we compute a smoothed latency for visualization? (Out of scope; consider post‑TT‑M‑03.27.)

## References

- docs/development/milestone-documentation-guide.md  
- docs/development/TEMPLATE-tracking.md  
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/architecture/time-travel/time-travel-planning-roadmap.md  
- docs/architecture/time-travel/time-travel-architecture-ch1-overview.md  
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md  
- docs/architecture/time-travel/time-travel-architecture-ch3-components.md
