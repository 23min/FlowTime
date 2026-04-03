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

## Immediate

#### E-10 — Engine Correctness & Analytical Primitives

- **Folder:** `work/epics/E-10-engine-correctness-and-analytics/`
- **Status:** Phases 0-2 complete, `p3a` and `p3a1` wrapped, remaining Phase 3 gated on E-16
- **Goal:** Fix P0 correctness bugs, harden engineering quality, align documentation with code, and build the analytical primitives layer (bottleneck ID, cycle time, WIP limits, variability, constraint enforcement, starvation detection) that downstream epics depend on.
- **Phases:** 0 (bugs) → 1+2 (engineering + docs, parallel) → 3 (analytical primitives)
- **Key dependency:** Phase 3 unlocks near-term epics. See `ROADMAP.md` dependency graph.
- **Reference:** `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (historical rationale)

#### E-16 — Formula-First Core Purification

- **Folder:** `work/epics/E-16-formula-first-core-purification/`
- **Status:** Approved — immediate architecture gate after wrapped `m-ec-p3a1`
- **Goal:** Move semantic truth and analytical identity fully into the compiled Core model so the engine remains a deterministic formula evaluator and API/UI layers consume facts rather than reconstructing meaning from strings.
- **Sequencing:** Runs immediately after `m-ec-p3a1` and before further E-10 Phase 3 expansion (`p3b`, `p3c`, `p3d`).
- **Key milestones:** compiled semantic references, class truth boundary, runtime analytical descriptor, Core analytical evaluation, warning facts/primitive cleanup, analytical contract + consumer purification
- **Migration:** Forward-only. Runs, fixtures, and approved goldens are regenerated rather than kept compatible.
- **Reference:** `docs/architecture/formula-first-engine-refactor-plan.md`

#### dag-map Library Evaluation (Spike)

- **Reference:** `docs/architecture/dag-map-evaluation.md`
- **Status:** Not started — runs in parallel with Phase 1+2
- **Goal:** Evaluate and extend the dag-map metro-map layout library for FlowTime topology rendering. ~2-3 days. Determines viability of SVG-based topology and informs Visualizations and UI Layout Motors epics.

#### E-11 — Svelte UI (Frontend Rewrite)

- **Folder:** `work/epics/E-11-svelte-ui/`
- **Status:** M1-M4 complete, M6 in progress
- **Goal:** Replace Blazor WebAssembly UI with SvelteKit + shadcn-svelte for demo-quality visuals. Independent of engine work.
- **dag-map:** M3 (topology rendering), M4 (heatmap mode) delivered. M5 (inspector) will need dag-map edge coloring and click events.

## Near-Term Epics

These depend on the analytical primitives from Phase 3 (except Telemetry Ingestion which is independent). dag-map enhancements are scoped within consuming milestones, not as a separate epic.

#### E-12 — Dependency Constraints & Shared Resources

- **Folder:** `work/epics/E-12-dependency-constraints/`
- **Goal:** Model downstream dependencies as resource constraints with visible bottlenecks and coupling.
- **Status:** M-10.01 and M-10.02 complete (Option A + B foundations). M-10.03 (MCP enforcement) deferred until runtime constraint enforcement (Phase 3.5) is in place. See `work/gaps.md`.
- **Depends on:** Phase 3.5 (ConstraintAllocator wired into evaluation pipeline)

#### E-13 — Path Analysis & Subgraph Queries

- **Folder:** `work/epics/E-13-path-analysis/`
- **Goal:** Path-level queries and derived metrics (dominant routes, bottleneck attribution, path pain) for UI and MCP.
- **Depends on:** Phase 3.1 (bottleneck ID), Phase 3.2 (cycle time decomposition)
- **dag-map:** Will need path highlighting, edge width by flow volume, non-path dimming
- **Related:** `work/gaps.md` (Path Analysis section)

#### E-14 — Visualizations / Chart Gallery (absorbed into UI Analytical Views)

- **Folder:** `work/epics/E-14-visualizations/`
- **Status:** Absorbed. The chart gallery concept is replaced by purpose-built analytical views in the UI Analytical Views epic. Role-focused chart bundles may still exist as presets within that epic.
- **See:** `work/epics/ui-analytical-views/spec.md`

#### E-15 — Telemetry Ingestion, Topology Inference & Canonical Bundles

- **Folder:** `work/epics/E-15-telemetry-ingestion/`
- **Goal:** Build the pipeline from real-world data (event logs, traces, sensor feeds) to FlowTime topology + Gold-format series. Includes Gold Builder, Graph Builder (topology inference with confidence scoring), and bundle assembly.
- **Depends on:** Stable bundle schemas (already in place). Independent of Phase 3 for basic ingestion; Phase 3 makes ingested data interesting.
- **Validation datasets identified:** BPI Challenge 2012 (process mining), Road Traffic Fines, PeMS + OSM (road traffic), MTA + GTFS (transit). See `docs/architecture/dataset-fitness-and-ingestion-research.md`.
- **Note:** Should preserve variability (Cv) if Phase 3.4 ships, so ingestion format should be designed with this in mind.

## UI Paradigm Epics (draft — unnumbered until sequenced)

These epics implement the UI paradigm shift described in
`docs/architecture/ui-paradigm.md`. Blazor UI enters maintenance mode;
Svelte UI becomes the platform for these new interaction models.

#### UI Workbench & Topology Refinement

- **Folder:** `work/epics/ui-workbench/`
- **Goal:** Strip the topology DAG to structure + one color dimension. Build a workbench panel for pinning nodes/edges and inspecting metrics side-by-side.
- **Depends on:** E-11 M3-M4 (topology + timeline). Supersedes E-11 M5 (Inspector).
- **Rendering:** SVG first; canvas only if measured performance problems.

#### UI Analytical Views

- **Folder:** `work/epics/ui-analytical-views/`
- **Goal:** Purpose-built views alongside topology: heatmap (nodes x bins grid), decomposition (cycle time breakdown + Kingman), comparison (two runs side-by-side), flow balance (conservation checks).
- **Depends on:** UI Workbench epic (view switcher), E-10 Phase 3 (analytical primitives).
- **Absorbs:** E-14 (Visualizations). Role-focused chart bundles become presets within views.

#### UI Question-Driven Interface

- **Folder:** `work/epics/ui-question-driven/`
- **Goal:** Structured query panel where users ask analytical questions ("Where is the bottleneck?", "Why is cycle time high?") and get computed, provenanced answers. Foundation for future DSL and LLM integration.
- **Depends on:** E-10 Phase 3 (analytical primitives), UI Workbench epic, UI Analytical Views epic.

## Mid-Term / Aspirational Epics (unnumbered until sequenced)

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

## Epic Numbering Convention

- Epics are numbered sequentially: E-10, E-11, E-12, ...
- Completed epics before E-10 are unnumbered (legacy)
- Epic folders: `work/epics/E-{NN}-<slug>/`
- Mid-term/aspirational epics get numbered when their sequence is confirmed
- dag-map library enhancements are scoped within consuming epic milestones, not a separate epic

## Keeping in Sync

- `ROADMAP.md` is the high-level plan. This file is the detailed epic index.
- When adding or reordering epics, update both documents.
- Completed epics move to `work/epics/completed/`.
