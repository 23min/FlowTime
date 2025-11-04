# TT‑M‑03.28 — Retries Foundations (Attempts/Success/Failure + Retry Rate)

Status: Planned  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Introduce first‑class retry observability. Extend contracts and examples so dependency attempts, successes, and failures are captured (or derived), and expose a simple retry rate for visualization. This milestone lays API/template groundwork and a minimal UI readout; edge heatmaps and service time land in follow‑ups.

## Goals

- Add dependency attempt/success/failure series to example templates (synthetic).  
- Include retry metrics in API responses (per window) with a minimal, stable shape.  
- UI reads and displays retry rate in inspector (per node and dependency) without advanced overlays yet.  

## Scope

In Scope
- Templates: add per‑dependency `dep_attempts`, `dep_success`, `dep_failure` series for at least one example system (microservices).  
- API: extend `/v1/runs/{id}/state_window` with an `edges` section that carries retry series (attempts, success, failure) and a derived `retryRate` (`(attempts - success)/attempts` with guards).  
- UI: Inspector shows a compact Retry chart for the focused node’s outgoing dependencies (stacked mini-bars or list), and a simple numeric retry rate.

Out of Scope
- Edge coloring/heat overlays on topology (TT‑M‑03.30).  
- Service time and processing time (TT‑M‑03.29).  
- Backoff visualization.

## Requirements

### Functional
- RF1 — Template series
  - For a chosen example (e.g., `microservices`), add per‑edge series:  
    - `dep_{A}_to_{B}_attempts`, `dep_{A}_to_{B}_success`, `dep_{A}_to_{B}_failure` (bins length).  
  - Optional parameters to vary retry probability.
- RF2 — API window payload
  - `/v1/runs/{id}/state_window` adds an `edges` object:  
    - `edges["A→B"]: { attempts: number[], success: number[], failure: number[], retryRate: number[] }`  
  - Guard division by zero: `retryRate[i] = null` if `attempts[i] == 0`.
- RF3 — UI inspector
  - For the focused node, list outgoing dependencies with a small bar for `retryRate` and a numeric %; clicking an item highlights the edge.

### Non‑Functional
- Stable contract: shape of `edges` must remain consistent (golden tests).  
- Performance: window payload growth bounded (cap to visible range; no global 288‑bin dump unless requested).  
- Accessibility: inspector labels readable; color independence (text also conveys rate).

## Deliverables
1) Example template update (microservices) with dependency retry series.  
2) API: `edges` section on `/state_window` with retry metrics + guards.  
3) UI: inspector retry list for focused node; navigation highlights selected edge.  
4) Docs: contract snippet and example YAML.  
5) Tests: golden API payload and UI snapshot/unit for retry list.

## Acceptance Criteria
- AC1: `/state_window` includes `edges` with attempts/success/failure and `retryRate`.  
- AC2: Inspector renders retry rates for the focused node’s outgoing dependencies.  
- AC3: Division‑by‑zero handled (nulls for retryRate where attempts == 0).  
- AC4: Golden tests pin `edges` response shape.

## Implementation Plan (Sessions)

Session 1 — Template + Fixtures  
- Add retry series to microservices example; document naming conventions.  
- Verify bins length and conservation (attempts = success + failure + canceled?).

Session 2 — API Window Contract  
- Extend builder to collect dependency series; build `edges` map; derive `retryRate`.  
- Add unit + golden tests.

Session 3 — UI Inspector Retry View  
- Add a small panel in node inspector to list outgoing deps with retry % and tiny bars.  
- Edge highlight on click; no canvas overlays yet.

Session 4 — Docs + Validation  
- Update milestone and contracts; add `.http` samples; confirm null handling.

## Testing Strategy
- API unit: retryRate guards, shape correctness, partial data tolerances.  
- API golden: fixed run → known edges payload.  
- UI: render list, accessibility labels, edge highlight event.  
- Perf: slice same as visible window.

## Telemetry Contract Addendum (Window)
```json
{
  "edges": {
    "ServiceA→ServiceB": {
      "attempts": [10, 9, ...],
      "success": [9, 9, ...],
      "failure": [1, 0, ...],
      "retryRate": [0.1, 0.0, ...]
    }
  }
}
```

## Risks & Mitigations
- Payload size: limit to requested slice; lazy‑load if many edges.  
- Ambiguous semantics: document attempts vs success/failure mapping; examples.

## Open Questions
- Should edges carry latency in this milestone or in TT‑M‑03.30 with overlays? (Default: TT‑M‑03.30.)

## References
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md  
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/development/milestone-documentation-guide.md

