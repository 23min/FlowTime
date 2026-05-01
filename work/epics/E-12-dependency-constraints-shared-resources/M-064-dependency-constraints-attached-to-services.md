---
id: M-064
title: Dependency Constraints (Attached to Services)
status: done
parent: E-12
---

## Goal

Add **Option B** dependency constraints: allow services to declare dependency constraints without inserting explicit dependency nodes in the graph. This keeps the topology simple while preserving coupling and bottleneck visibility in the engine outputs.

## Problem Statement

Option A (explicit dependency nodes) is correct but can clutter large graphs. Most telemetry systems (e.g., Azure App Insights) model dependencies **as relationships on a service** rather than explicit nodes in a DAG. We need a first-class constraint model that preserves the arrivals/served basis and exposes shared bottlenecks without graph inflation.

## Scope

### In Scope
- Allow service nodes (`service`, `serviceWithBuffer`, `router`) to declare **dependency constraints**.
- Represent constraints in `/state` and `/state_window` outputs with explicit metadata and warnings.
- Support **shared dependencies** via a named constraint resource that multiple services can reference.
- Provide a minimal allocation rule (documented and deterministic) for shared constraints.
- Minimal UI exposure so Option B can be validated during this milestone.
- A dedicated Option B template that exercises both full-feature and fallback behavior paths.

### Out of Scope
- UI re-layout or new visual layers (reuse existing inspectors).
- Latency-based backlog inference (beyond explicit signals).
- Telemetry ingestion transformations (handled by Telemetry Ingestion epic).

## Proposed Model (Option B)

### Constraint Declaration
- Services can list dependency constraints, e.g.:
  - `constraints: [db_main, cache_redis]`
- Each constraint has its own arrivals/served signals (from telemetry or derived). Optional signals include `errors` and `latency`.
- Missing constraint signals must emit warnings with explicit provenance.

### Engine Semantics
- Effective service capacity is limited by:
  - service capacity
  - dependency constraint capacity
- Constraints may also **inflate service time** when retry load is modeled (deterministic rule).
- When constraints are shared, capacity is allocated using a deterministic rule (e.g., proportional to demand or observed served).
- Constraints must **not** re‑inject arrivals (no feedback loops in M‑10.02).

### Output Contract
- `/state` and `/state_window` include per-node:
  - `constraints` block with dependency IDs and per-bin constraint metrics (arrivals/served, optional errors/latency).
  - `constraintStatus` indicating if the node was dependency-limited in the bin.
- Series metadata must flag derived vs explicit sources (`origin` + `aggregation`).

### UI Exposure (Permanent)
- Add a Constraints section in the inspector for nodes with attached constraints.
- Show per-constraint arrivals/served/errors (if present) and a constraint-limited indicator.
- No new visual layers; reuse existing inspector styling.

## Deliverables

### Engine / Contracts
- Add constraint metadata to node contract (JSON + C# models).
- Add constraint evaluation logic in the engine for supported node kinds.

### API / Schema
- Extend `time-travel-state` schema with `constraints` on nodes.
- Ensure `seriesMetadata` for constraint series is provided.

### Tests
- Golden fixtures for dependency constraints on service nodes.
- Contract tests for constraint metadata and warnings.
- Deterministic allocation test for shared dependencies.

### Documentation
- Update `work/epics/dependency-constraints/spec.md` with Option B semantics.
- Add notes to `docs/reference/engine-capabilities.md` for constraint-limited nodes.
- Document the Option B template and validation expectations in `templates/README.md`.

## Decisions Needed

1. **Allocation rule:** choose a deterministic default (recommend proportional to demand) so tests are stable.
2. **Constraint schema:** nested block vs referenced external resource.
3. **Supported node kinds:** confirm scope (`service`, `serviceWithBuffer`, `router`).

## Test Plan (Must Follow TDD)

1. Add failing golden test for `/state_window` with dependency constraints attached to a service.
2. Add failing test for shared dependency allocation.
3. Add warning test for missing constraint signals.
4. Implement engine support and update schema/contracts.
5. Update goldens and documentation.
6. Validate Option B in UI using the dedicated template.

## Acceptance Criteria

- Services can declare dependency constraints without explicit dependency nodes.
- `/state` and `/state_window` expose constraint metrics and limitation status.
- Shared dependency allocation is deterministic and documented.
- Warnings surface missing constraint inputs with provenance metadata.
