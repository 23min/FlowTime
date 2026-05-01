---
id: M-063
title: Dependency Constraints Foundations
status: done
parent: E-12
acs:
  - id: AC-1
    title: Dependency nodes appear in /state and /state_window with correct
    status: met
  - id: AC-2
    title: Edge semantics for dependency load are explicit and validated.
    status: met
  - id: AC-3
    title: Missing dependency signals emit warnings and are visible to consumers.
    status: met
  - id: AC-4
    title: Tests pass; no UI changes required.
    status: met
---

## Goal

Introduce a first, minimal dependency-constraint model that allows FlowTime to represent downstream/shared resources **as explicit DAG nodes** without breaking the arrivals/served/queue basis. The engine must expose dependency load and coupling in API outputs, with explicit provenance and warnings for inferred signals.

## Problem Statement

FlowTime currently models node throughput and queue depth but cannot explain bottlenecks caused by shared downstream dependencies (DBs, caches, APIs). This milestone establishes a baseline representation so dependencies can be modeled as constraints, and consumers can observe coupling and saturation without client-side derivation.

## Scope

### In Scope
- **Option A only:** Add a dependency node kind and connect it explicitly in the DAG.
- Preserve the arrivals/served basis for dependency nodes.
- Expose dependency load in `/state` and `/state_window` with explicit provenance metadata.
- Surface warnings for inferred or missing dependency signals.
- Provide a reference template and a golden test fixture that exercises dependency constraints.

### Out of Scope
- **Option B (constraint attached to service nodes)** — deferred to M-064.
- Full shared-capacity allocation algorithms.
- Latency-based inference of hidden backlog (Level 3 per epic doc).
- UI overlays beyond existing topology panels.
- Telemetry ingestion changes (handled by the Telemetry Ingestion epic).

## Proposed Model (MVP)

Support **Option A (Dependency as Node)** with explicit edges:

- A dependency is a node type (`dependency`) with arrivals and served series.
- Upstream services connect to the dependency using **effort edges** (attempt load).
- Dependency node emits served to the upstream service or downstream hop via **throughput edges**.

This keeps edge semantics explicit and leverages existing edge metrics.

### Retry / Backpressure Guidance (Non‑Looping)

To keep the graph acyclic while still modeling dependency failures:

- **Retry load** should be modeled as a derived attempts series (e.g., `CONV(dependency_errors, kernel)`).
- **Backpressure** should be represented by inflating service time or reducing effective capacity on the caller:
  - Example: `processingTimeMsSum = base_time_ms * (served + retry_attempts)`.
- **Explicit feedback loops are out of scope** for this milestone (DAG constraint).

### Future Compatibility

- Option B (dependency as constraint) is defined in M-064 and builds on the same series/edge semantics.
- Derived/inferred signals must be labeled `origin: derived` and surfaced with warnings.

## Deliverables

### Engine / Contracts
- Add a `dependency` node kind (or equivalent logical type) with expected series: `arrivals`, `served`, optional `errors`.
- Ensure edge series metadata (attemptsVolume, failuresVolume, retryRate) applies cleanly to dependency edges.
- Extend `seriesMetadata` to include dependency-specific series with `origin` and `aggregation`.

### API / Schema
- `/state` and `/state_window` include dependency node series and metadata.
- Warnings for missing dependency series are emitted consistently (e.g., `missing_dependency_arrivals`).

### Tests
- Golden fixtures for dependency nodes in `/state` and `/state_window`.
- Contract tests asserting provenance (`origin`) and aggregation for dependency series.
- Warning tests covering missing dependency inputs.

### Documentation
- Update `work/epics/dependency-constraints/spec.md` with MVP scope and constraints.
- Add a short usage note in `docs/reference/engine-capabilities.md`.

## Decisions Needed

1. **Node representation:** confirm that the MVP should implement Option A (dependency as node) only.
2. **Naming:** choose the node kind string (`dependency` vs `resource`).
3. **Series semantics:** confirm which series are mandatory vs optional (arrivals/served/errors).

## Risks

- Overloading edge semantics for dependency load could blur effort vs throughput if not clearly labeled.
- Tests and documentation must clearly communicate inferred vs explicit signals.

## Test Plan (Must Follow TDD)

1. Add failing golden test for dependency node in `/state_window` (with metadata).
2. Add failing golden test for dependency node in `/state` (single bin).
3. Add failing warning test for missing dependency arrivals/served.
4. Implement engine + schema updates.
5. Update goldens and documentation.

## Acceptance criteria

### AC-1 — Dependency nodes appear in /state and /state_window with correct

Dependency nodes appear in `/state` and `/state_window` with correct series and metadata.
### AC-2 — Edge semantics for dependency load are explicit and validated.

### AC-3 — Missing dependency signals emit warnings and are visible to consumers.

### AC-4 — Tests pass; no UI changes required.
