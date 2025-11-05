# TT‑M‑03.28 — Retries First‑Class (Attempts/Failures, Effort vs Throughput Edges, Temporal Echoes)

Status: Planned  
Owners: Platform (API/Sim) + UI  
References: docs/architecture/retry-modeling.md, docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md, docs/performance/perf-log.md

## Overview

Introduce retries as first‑class concepts: explicitly model attempts/successes/failures, differentiate throughput vs effort edges, and support retry‑induced temporal echoes via causal operators (e.g., CONV/SHIFT/DELAY) while preserving forward‑only evaluation and bounded history.

## Goals

- Add attempts/failures (and optional retryEcho) series and edge semantics to the model, API, and UI.
- Keep evaluation causal and performant (bounded history, precompute where pragmatic).
- Provide clear visuals (chips, edges, inspector stacks) distinct from throughput.

## Scope

In Scope
- Template example with attempts/served/failures + retry echo kernel; edge typing (throughput/effort).
- API graph/state_window additions for attempts/failures/retryEcho (additive only).
- UI edge styles for effort vs throughput; chips and inspector stacks for retry series.
- Tests (unit/golden/UI) and docs; perf log entry after full run.

Out of Scope (deferred)
- Advanced effort heatmap overlays; service‑time S metrics; oldest‑age telemetry.

## Requirements

### Functional
- Throughput edges: downstream arrivals = upstream served (optional lag).
- Effort edges: dependency load = upstream attempts × multiplier (optional lag).
- Retry echo: `retryEcho = CONV(failures, kernel)` (causal kernel; bounded length).

### Non‑Functional
- Bounded history and kernel length; perf budgets enforced.
- Additive API changes; guard missing telemetry with structured warnings.

## Built‑In Mitigations (Must‑Do)

- Kernel governance: validate kernel length and sum; warn/clamp when out of policy (recommend Σ≤1.0; length cap configurable).
- Precompute retryEcho for simulation at artifact time (like SHIFT queue depth) to avoid runtime cost; telemetry remains authoritative.
- Conservation checks: helper to assert `attempts = successes + failures`; sanity check temporal mass for kernel on fixtures.
- Causal enforcement: only past references allowed; keep DAG forward‑only, precompute where necessary to avoid cycles.
- Null‑guarding: consistent behaviour for missing series; emit `telemetry_sources_unresolved` warnings.
- Schema/additive contracts: no breaking changes; document fields and defaults.
- UI toggles + A11y: feature toggles for retry chips; distinct edge styles; aria labels; contrast checks.
- Rounding/precision: standardize decimal precision in builders and goldens to reduce flakiness.

## Deliverables

1) Template(s) with retries and edge typing (throughput/effort) and a deterministic kernel.  
2) API: `/graph` edge types + `/state_window` attempts/failures/retryEcho series (additive).  
3) UI: edge styles; chips (Attempts/Failures/Retry); inspector stacks + horizons.  
4) Tests: unit + golden updates for API; UI tests for inspector/edges.  
5) Docs: milestone + tracking; telemetry contract snippet; perf log update.  

## Acceptance Criteria

- AC1: Example run loads; `/graph` includes effort and throughput edges; `/state_window` includes attempts/failures/(retryEcho).  
- AC2: Effort edges render distinctly from throughput; chips for Attempts/Failures/Retry visible (toggled).  
- AC3: Inspector stack for retry‑enabled nodes includes Attempts/Failures/RetryEcho/Served with horizons.  
- AC4: Kernel governance warnings fire on out‑of‑policy kernels; bounded history enforced.  
- AC5: Tests (API/UI/golden) pass; perf budget respected; no regressions.

## Implementation Plan (Sessions)

Session 1 — Templates/Precompute
- Add retries to a focused example; implement artifact‑time precompute of retryEcho.

Session 2 — API Contracts
- Extend `/graph` and `/state_window` additively; add unit/golden tests.

Session 3 — UI Rendering
- Add edge styles and chips; inspector stacks + horizons; UI tests.

Session 4 — Docs/Perf
- Update docs/contract; add perf log entry after full run.

## Telemetry Contract (Draft)

Per retry‑enabled node per bin:
- attempts, served (=successes), failures; optional retryEcho.
- Edge metadata: throughput vs effort (multiplier, lag optional).

## Risks & Mitigations

See “Built‑In Mitigations” above and docs/architecture/retry-modeling.md (causal operators, bounded history, conservation).

