---
id: M-057
title: Phase 3a.1 — Analytical Projection Hardening
status: done
parent: E-10
acs:
  - id: AC-1
    title: 'AC-1: Current analytical capabilities resolved in Core'
    status: met
  - id: AC-2
    title: AC-2
    status: met
  - id: AC-3
    title: AC-3
    status: met
  - id: AC-4
    title: 'AC-4: Stationarity warning policy is explicit in the runtime path'
    status: met
  - id: AC-5
    title: 'AC-5: Current DTO parity is complete for analytical class fields'
    status: met
  - id: AC-6
    title: 'AC-6: Remaining purification work is explicitly handed off to E-16'
    status: met
  - id: AC-7
    title: 'AC-7: Tests and gate'
    status: met
---

## Goal

Establish the first correct boundary move for analytical state metrics: Core owns the current analytical capability resolution and derived metric computation used by `/state` and `/state_window`, while the remaining formula-first purification work is explicitly handed to E-16.

## Context

Phase 3a introduced the right analytical primitives: `CycleTimeComputer` computes `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, and `flowEfficiency` as pure math in Core. But the **capability decision** — "does this node have queue semantics? service semantics? both?" — still lived in `StateQueryService`, duplicated across snapshot building, window building, metadata emission, stationarity warnings, per-class conversion, and flow-latency composition.

This milestone fixed that first-order boundary problem by moving the current analytical capability/computation surface into Core and wiring state projection to consume it. During the follow-on architecture pressure test, we decided not to keep expanding p3a1 into the full cleanup epic. Full purification now belongs to E-16.

That means p3a1 is the bridge milestone, not the final deletion milestone. It delivers the current analytical computation move, finite-value safety, logicalType parity for the current state surfaces, and DTO parity. Compiled semantic references, runtime analytical descriptors, class-truth separation, and client heuristic deletion are owned by E-16.

## Acceptance criteria

### AC-1 — AC-1: Current analytical capabilities resolved in Core

**AC-1: Current analytical capabilities resolved in Core.** Core provides an `AnalyticalCapabilities` concept (record, type, or equivalent) that captures what the current state endpoints can compute analytically: queue semantics (Little's Law queue time, latency), service semantics (service time), cycle-time decomposition (queue + service → cycle time, flow efficiency), and stationarity-warning eligibility.
### AC-2 — AC-2

**AC-2: Core computes the current analytical derived metrics for state projection.** Core provides a computation surface that takes capabilities and raw data (for a single bin or a windowed range) and produces the analytical values used by `/state` and `/state_window`: `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, `flowEfficiency`, and `latencyMinutes`. This covers both node-level and current per-class breakdowns.
### AC-3 — AC-3

**AC-3: Finite-value safety and metadata honesty for the current analytical payload.** The Core computation guarantees that NaN or Infinity inputs or intermediate results never produce non-null NaN/Infinity outputs. Analytical derived values follow the Phase 1 Tier 2 null policy, and `seriesMetadata` only advertises analytical keys that are actually emitted in the payload.
### AC-4 — AC-4: Stationarity warning policy is explicit in the runtime path

**AC-4: Stationarity warning policy is explicit in the runtime path.** `littles-law-non-stationary` uses one named runtime configuration source for tolerance, and it is emitted only when `queueTimeMs` is actually present for the window being projected.
### AC-5 — AC-5: Current DTO parity is complete for analytical class fields

**AC-5: Current DTO parity is complete for analytical class fields.** The current state API and Blazor time-travel DTO surfaces expose analytical by-class fields end-to-end so consumers can ingest the projected values without local reconstruction.
### AC-6 — AC-6: Remaining purification work is explicitly handed off to E-16

**AC-6: Remaining purification work is explicitly handed off to E-16.** Typed semantic references, runtime analytical descriptors, class-truth separation, public analytical contract redesign, client heuristic deletion, and final semantic-parser removal are out of scope for `m-ec-p3a1` and owned by E-16.
### AC-7 — AC-7: Tests and gate

**AC-7: Tests and gate.** `dotnet build` and `dotnet test --nologo` are green for the wrapped milestone state.
## Technical Notes

- `AnalyticalCapabilities` remains the bridge abstraction for the current state surfaces. E-16 will replace string-derived inputs with compiled runtime facts.
- `flowLatencyMs` graph propagation stays in the adapter for this milestone. p3a1 only moves the per-node analytical base values it consumes into Core.
- Existing non-analytical derived metrics (`utilization`, `throughputRatio`, `retryTax`, `color`) remain in the adapter.
- Semantic string parsing and logicalType reconstruction that still exist for runtime behavior are tolerated only as bridge behavior. E-16 owns deleting them.
- This milestone is allowed to update approved snapshots and fixtures to reflect corrected analytical outputs.

## Out of Scope

- Typed semantic references in the compiled/runtime model
- Runtime analytical descriptors on compiled nodes
- Class-truth boundary cleanup / wildcard fallback separation
- Public analytical contract redesign and client heuristic deletion
- Final semantic-parser deletion from API and UI layers
- New analytical primitives (WIP limits, variability, constraint enforcement, bottleneck ranking)

## Dependencies

- Phase 1 complete ✅
- Phase 3a implementation exists and is the input to this hardening pass
- E-16 follows immediately after wrap and before Phase 3 continues with p3b/p3c/p3d

## References

- `work/epics/E-10-engine-correctness-and-analytics/m-ec-p3a-review.md`
- `work/epics/E-10-engine-correctness-and-analytics/m-ec-p3a-review-codex.md`
- `work/epics/E-10-engine-correctness-and-analytics/m-ec-p3a-cycle-time.md`
- `docs/architecture/nan-policy.md`
- `work/epics/E-16-formula-first-core-purification/spec.md`
