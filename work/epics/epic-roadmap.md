# Architecture Epic Roadmap

This document provides an overview of architecture-level **epics** and how they relate to the broader roadmap (`ROADMAP.md`). Each epic lives in its own folder under `work/epics/` and is implemented via milestones under `work/milestones/`.

## How to Read This Document

- Each section below corresponds to an epic folder under `work/epics/` (active) or `work/epics/completed/` (done).
- Epics are ordered by priority: immediate work first, then near-term, then aspirational.
- For each epic, we list the folder path, goal, key milestones, and current status.

This document should remain in sync with `ROADMAP.md` (which gives the higher-level, product-facing view).

## Completed Epics

9 epics delivered. Full specs and supporting docs in `work/epics/completed/`.

| Epic | Folder | Key Milestones |
|------|--------|---------------|
| Time Travel V1 | `work/epics/completed/time-travel/` | TT-M-03.17 through TT-M-03.32.1 |
| UI Performance | `work/epics/completed/ui-perf/` | FT-M-05.06, FT-M-05.07 |
| Classes & Routing | `work/epics/completed/classes/` | CL-M-04.01 through CL-M-04.04 |
| Service With Buffer | `work/epics/completed/service-with-buffer/` | SB-M-05.01 through SB-M-05.04 |
| Evaluation Integrity | `work/epics/completed/evaluation-integrity/` | M-06.01, M-06.02 |
| Edge Time Bins | `work/epics/completed/edge-time-bin/` | M-07.01 through M-07.06 |
| MCP Modeling & Analysis | `work/epics/completed/ai/` | M-08.01 through M-08.05 |
| Engine Semantics Layer | `work/epics/completed/engine-semantics-layer/` | M-09.01 |
| Package Updates (.NET 9) | `work/epics/completed/update-packages/` | M-11.01, M-11.02 |

## Immediate — Engine Correctness & Analytical Primitives

#### Engine Correctness & Analytical Primitives

- **Folder:** `work/epics/engine-correctness-and-analytics/`
- **Status:** Not started — highest priority
- **Goal:** Fix P0 correctness bugs, harden engineering quality, align documentation with code, and build the analytical primitives layer (bottleneck ID, cycle time, WIP limits, variability, constraint enforcement, starvation detection) that downstream epics depend on.
- **Phases:** 0 (bugs) → 1+2 (engineering + docs, parallel) → 3 (analytical primitives)
- **Key dependency:** Phase 3 unlocks near-term epics. See `ROADMAP.md` dependency graph.
- **Reference:** `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (historical rationale)

#### dag-map Library Evaluation (Spike)

- **Reference:** `docs/architecture/dag-map-evaluation.md`
- **Status:** Not started — runs in parallel with Phase 1+2
- **Goal:** Evaluate and extend the dag-map metro-map layout library for FlowTime topology rendering. ~2-3 days. Determines viability of SVG-based topology and informs Visualizations and UI Layout Motors epics.

## Near-Term Epics

These depend on the analytical primitives from Phase 3 (except Telemetry Ingestion which is independent).

#### Dependency Constraints & Shared Resources

- **Folder:** `work/epics/dependency-constraints/`
- **Goal:** Model downstream dependencies as resource constraints with visible bottlenecks and coupling.
- **Status:** M-10.01 and M-10.02 complete (Option A + B foundations). M-10.03 (MCP enforcement) deferred until runtime constraint enforcement (Phase 3.5) is in place. See `work/gaps.md`.
- **Depends on:** Phase 3.5 (ConstraintAllocator wired into evaluation pipeline)

#### Path Analysis & Subgraph Queries

- **Folder:** `work/epics/path-analysis/`
- **Goal:** Path-level queries and derived metrics (dominant routes, bottleneck attribution, path pain) for UI and MCP.
- **Depends on:** Phase 3.1 (bottleneck ID), Phase 3.2 (cycle time decomposition)
- **Related:** `work/gaps.md` (Path Analysis section)

#### Telemetry Ingestion & Canonical Bundles

- **Folder:** `work/epics/telemetry-ingestion/`
- **Goal:** Transform raw telemetry into canonical FlowTime bundles with validated manifests.
- **Depends on:** Stable bundle schemas (already in place). Independent of Phase 3.
- **Note:** Should preserve variability (Cv) if Phase 3.4 ships, so ingestion format should be designed with this in mind.

#### Visualizations / Chart Gallery

- **Folder:** `work/epics/visualizations/`
- **Goal:** Dedicated UI space for role-focused charts (exec, SRE, support) using FlowTime-derived metrics.
- **Depends on:** Phase 3.1, 3.2, 3.3 (without analytical primitives, charts can only show throughput/queue — same as topology view). dag-map spike informs rendering approach.

## Mid-Term / Aspirational Epics

#### Scenario Overlays & What-If Runs

- **Folder:** `work/epics/overlays/`
- **Goal:** Derived overlay runs for capacity, parallelism, arrivals, WIP limit, and variability experiments with deterministic provenance.
- **Depends on:** Phase 3.3 (WIP limits), Phase 3.4 (variability) — these are force multipliers.

#### Flow-Aware Anomaly & Pathology Detection

- **Folder:** `work/epics/anomaly-detection/`
- **Goal:** Detect incidents and recurring flow pathologies (retry storms, slow drains, stuck queues) using the time-binned DAG model.
- **Depends on:** Phase 3.1 (bottleneck ID), Phase 3.6 (starvation/blocking detection) as building blocks.

#### Telemetry Loop & Parity

- **Folder:** `work/epics/telemetry-loop-parity/`
- **Goal:** Ensure synthetic runs and telemetry replays match within defined tolerances.
- **Depends on:** Telemetry Ingestion epic.

#### UI Layout Motors (Pluggable Layout Engines)

- **Folder:** `work/epics/ui-layout/`
- **Goal:** Decouple topology layout from rendering behind a stable `LayoutInput -> LayoutResult` contract.
- **Depends on:** dag-map spike results.

#### Ptolemy-Inspired Semantics & Directors

- **Folder:** `work/epics/ptolemy/`
- **Goal:** Conceptual guardrails for engine evolution — explicit time/coordination semantics, typed ports, determinacy contracts.

#### Streaming & Subsystems

- **Folders:** `work/epics/streaming/`, `work/epics/subsystems/`
- **Goal:** Explore how FlowTime's DAG semantics extend into streaming and modular subsystems.
- **Notes:** Long-term exploratory. Requires stable engine semantics and node types.

## Uncategorized Epics

These exist as folders but aren't currently on the active roadmap:

- `work/epics/ui/` — UI architecture notes (not a formal epic)
- `work/epics/sim-engine-boundary/` — SIM/Engine boundary purification (status unclear)

## Keeping in Sync

- `ROADMAP.md` is the high-level plan. This file is the detailed epic index.
- When adding or reordering epics, update both documents.
- Completed epics move to `work/epics/completed/`.
