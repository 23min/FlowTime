# FlowTime Engine Charter (v2.0)

**Date:** 2026-01-24  
**Audience:** Engine, API, UI, and integration engineers

---

## Purpose

FlowTime Engine is the **deterministic execution and semantics layer** for FlowTime. It evaluates models on a canonical time grid, produces run artifacts, and exposes stable `/state`, `/state_window`, and `/graph` contracts that downstream UIs and agents can trust.

The engine is not a UI, not a template editor, and not a telemetry warehouse. It is the **source of truth** for FlowTime’s time‑binned semantics.

---

## Scope (What the engine does)

### Deterministic evaluation
- Compile models to a strict DAG and evaluate in topological order.
- Preserve provenance and derived‑metric semantics (origin + aggregation).

### Canonical run artifacts
- Emit `run.json`, `manifest.json`, `series/index.json`, and per‑series CSVs.
- Enforce consistency checks and emit warnings as first‑class artifacts.

### Stable state APIs
- `/state` for single‑bin snapshots.
- `/state_window` for windowed series and edge metrics.
- `/graph` for compiled topology and node/edge semantics.

### Core node/edge semantics
- Node kinds: service, serviceWithBuffer, router, sink, const, expr, dependency.
- Edge time bins: throughput, effort, terminal edges with conservation checks.
- Retry series (attempts/failures/retry echo), plus edge‑level retry metrics.
- Dependency constraints:
  - **Option A**: dependency nodes in the topology (flow nodes).
  - **Option B**: constraint registry attached to services (resource constraints).

---

## Non‑Goals (What the engine does not do)

- **No template authoring** (owned by FlowTime.Sim).
- **No UI‑level interpretation** or client‑side derivations.
- **No streaming runtime** or live ingestion loop (telemetry ingestion is a separate epic).
- **No ad‑hoc schema changes** without versioned contracts.

---

## Architecture Principles

1. **Deterministic by default**  
   Same model + seed = same artifacts.

2. **Semantics are server‑side**  
   UI and agents consume semantics; they do not invent them.

3. **Derived metrics are explicit**  
   Every derived series is labeled with provenance (origin + aggregation).

4. **Make wrongness visible**  
   Invariants and diagnostics are persisted and surfaced.

---

## Interfaces

### Inputs
- Model artifacts from FlowTime.Sim or MCP modeling.
- Optional telemetry bundles (ingestion epic in progress).

### Outputs
- Canonical run artifacts (series + manifests).
- Stable API contracts for `/state` and `/state_window`.

---

## Current State (Jan 2026)

Shipped and stable:
- Compile‑to‑DAG evaluator (evaluation integrity).
- Edge time bins + conservation warnings.
- Class‑aware series and UI filters.
- Dependency constraints (Option A and Option B) — foundations laid, enforcement pending.
- Engine semantics layer and MCP‑facing contracts.

---

## Near‑Term Engine Work

- Telemetry ingestion services (canonical bundle loader).
- Overlay/what‑if execution as deterministic variants.
- Path analysis and subgraph queries (server‑side).

---

## References

- `docs/reference/engine-capabilities.md` — Shipped capabilities.
- `work/epics/epic-roadmap.md` — Epic ordering and status.
- `work/epics/completed/engine-semantics-layer/spec.md` — Semantics layer goal.
- `work/epics/completed/edge-time-bin/spec.md` — Edge time bin contract.
- `work/epics/E-12-dependency-constraints/spec.md` — Dependency options.
