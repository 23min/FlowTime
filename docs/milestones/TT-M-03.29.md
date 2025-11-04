# TT‑M‑03.29 — Service Time (S) Derivation (Processing Time Sum)

Status: Planned  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Derive average service time S for service nodes from aggregated processing time and served counts, and expose it in `/state` and `/state_window`. Where raw processing time sums are unavailable, keep S null; do not infer from queue latency. Provide clear visuals and tests.

## Goals

- Define and adopt a minimal contract for service processing time per bin:  
  - `processingTimeMsSum` and `servedCount` (integers)  
  - Derived `serviceTimeMs = processingTimeMsSum / max(1, servedCount)` with guards  
- Update at least one template/fixture to include these series.  
- UI: show Service Time line in inspector; enable coloring basis selection to “ServiceTime”.

## Scope

In Scope
- Templates/fixtures: add `processingTimeMsSum` and `servedCount` for service nodes in an example system.  
- API: add `serviceTimeMs` to `/state` and `/state_window` node payloads for `kind=service`.  
- UI: inspector renders S; feature bar exposes “Service Time” color basis.

Out of Scope
- In‑service concurrency or queue wait time decomposition.  
- Edge overlays (handled in TT‑M‑03.30).

## Requirements

### Functional
- RS1 — Template series  
  - For selected services, emit per‑bin `processingTimeMsSum` and `servedCount`.  
  - Validate units (ms) and totals length == bins.
- RS2 — API derivation  
  - `/state_window.nodes[serviceId].series.serviceTimeMs` present when inputs available; null otherwise.  
  - `/state.nodes[serviceId].serviceTimeMs` present for single bin.
- RS3 — UI inspector  
  - Add a “Service Time” chart with horizon; include aria label.  
  - Feature bar adds “Color basis: Service Time” option; coloring thresholds documented.

### Non‑Functional
- Guards: avoid division by zero; nulls are explicit and not coerced to 0.  
- Performance: no recomputation on client; API derives once per response.  
- Accessibility: labels and units visible; numeric formatting stable.

## Deliverables
1) Example fixture with processing time and served count.  
2) API derivation and payload extensions for S.  
3) UI inspector S chart + color basis option.  
4) Docs: contract examples, threshold defaults.  
5) Tests: unit/golden + UI rendering tests.

## Acceptance Criteria
- AC1: `/state_window` includes `serviceTimeMs` for services with inputs; null when missing.  
- AC2: Inspector shows correct S line; horizon reflects window highlight.  
- AC3: Color basis “Service Time” active; thresholds apply.  
- AC4: Tests updated and green (excluding known unrelated perf skips).

## Implementation Plan (Sessions)

Session 1 — Fixture & Template  
- Add `processingTimeMsSum` + `servedCount`; document generation logic.  
- Validate bins alignment.

Session 2 — API Derivation  
- Add S computation to node series builder for `kind=service`; extend `/state`.  
- Unit + golden tests.

Session 3 — UI Integration  
- Inspector chart and color basis toggle; thresholds/palette wired to ColorScale.  
- Basic accessibility checks.

Session 4 — Docs + Stabilization  
- Contract snippets; `.http` samples; docs updated.  
- Review thresholds; finalize tests.

## Testing Strategy
- API unit: S derivation with guards; null propagation.  
- API golden: fixed run → consistent S series.  
- UI unit: chart renders and color basis switches.  
- Integration: inspect several bins; numeric checks with tolerances.

## Thresholds (Initial)
- Service Time basis (ms): green ≤ P50, yellow ≤ P90, red > P90 of baseline distribution (configurable in UI; default static values OK initially).

## Risks & Mitigations
- Missing series in real telemetry → keep S null; document requirements and fallbacks.  
- Threshold selection → start with static defaults; move to quantile‑based later.

## References
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md  
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/development/milestone-documentation-guide.md

