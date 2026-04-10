# Architecture Epic Roadmap

This document provides an overview of architecture-level **epics** and how they relate to the broader roadmap (`ROADMAP.md`). Each epic lives in its own folder under `work/epics/`. Active milestone specs and logs live beside the owning epic spec in that same folder; `work/milestones/` is a compatibility stub only.

## How to Read This Document

- Each section below corresponds to an epic folder under `work/epics/` (active) or `work/epics/completed/` (done).
- Epics are ordered by priority: immediate work first, then near-term, then aspirational.
- For each epic, we list the folder path, goal, key milestones, and current status.

This document should remain in sync with `ROADMAP.md` (which gives the higher-level, product-facing view).

## Completed Epics

17 epics delivered. Full specs and supporting docs in `work/epics/completed/`.

| Epic | Folder | Key Milestones |
|------|--------|---------------|
| Time Travel V1 | `work/epics/completed/time-travel/` | TT-M-03.17 through TT-M-03.32.1 |
| UI Performance | `work/epics/completed/ui-perf/` | FT-M-05.06, FT-M-05.07 |
| Classes & Routing | `work/epics/completed/classes/` | CL-M-04.01 through CL-M-04.04 |
| Core Foundations | `work/epics/completed/core-foundations/` | M-00.00, M-01.00, M-01.05, M-01.06 |
| PMF Modeling | `work/epics/completed/pmf-modeling/` | M-02.00 |
| Artifacts, Schema & Provenance | `work/epics/completed/artifacts-schema-provenance/` | M-02.06 through M-02.10 |
| Service API Foundation | `work/epics/completed/service-api-foundation/` | SVC-M-00.00, SVC-M-01.00 |
| Synthetic Ingest | `work/epics/completed/synthetic-ingest/` | SYN-M-00.00 |
| UI Foundations & Runner | `work/epics/completed/ui-foundations/` | UI-M-00.00 through UI-M-02.00 |
| UI Charter Workflow | `work/epics/completed/ui-charter-workflow/` | UI-M-02.05 through UI-M-02.08 |
| UI Schema & Contract Migration | `work/epics/completed/ui-schema-migration/` | UI-M-02.09 |
| Service With Buffer | `work/epics/completed/service-with-buffer/` | SB-M-05.01 through SB-M-05.04 |
| Evaluation Integrity | `work/epics/completed/evaluation-integrity/` | M-06.01, M-06.02 |
| Edge Time Bins | `work/epics/completed/edge-time-bin/` | M-07.01 through M-07.06 |
| MCP Modeling & Analysis | `work/epics/completed/ai/` | M-08.01 through M-08.05 |
| Engine Semantics Layer | `work/epics/completed/engine-semantics-layer/` | M-09.01 |
| Package Updates (.NET 9) | `work/epics/completed/update-packages/` | M-11.01, M-11.02 |

## Immediate

#### E-10 — Engine Correctness & Analytical Primitives (completed)

- **Folder:** `work/epics/completed/E-10-engine-correctness-and-analytics/`
- **Status:** Complete — all 8 milestones (p0, p1, p2, p3a, p3a1, p3d, p3c, p3b) delivered
- **Goal:** Fix P0 correctness bugs, harden engineering quality, align documentation with code, and build the analytical primitives layer (bottleneck ID, cycle time, WIP limits, variability, constraint enforcement, starvation detection) that downstream epics depend on.
- **Reference:** `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (historical rationale)

#### E-16 — Formula-First Core Purification

- **Folder:** `work/epics/completed/E-16-formula-first-core-purification/`
- **Status:** Completed (`m-E16-06` completed on `milestone/m-E16-06-analytical-contract-and-consumer-purification`)
- **Goal:** Move semantic truth and analytical identity fully into the compiled Core model so the engine remains a deterministic formula evaluator and API/UI layers consume facts rather than reconstructing meaning from strings.
- **Sequencing:** Runs immediately and before further E-10 Phase 3 expansion, which resumes in the order `p3d` -> `p3c` -> `p3b`.
- **Key milestones:** m-E16-01 compiled semantic references (completed) → m-E16-02 class truth boundary (completed) → m-E16-03 runtime analytical descriptor (completed) → m-E16-04 Core analytical evaluation (completed) → m-E16-05 warning facts/primitive cleanup (completed) → m-E16-06 analytical contract + consumer purification (completed)
- **Key decisions:** D-2026-04-03-005 (flowLatencyMs to Core), D-2026-04-03-006 (descriptor absorbs AnalyticalCapabilities), D-2026-04-03-007 (Parallelism typing)
- **Migration:** Forward-only. Runs, fixtures, and approved goldens are regenerated rather than kept compatible.
- **Reference:** `work/epics/E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md`

#### E-19 — Surface Alignment & Compatibility Cleanup

- **Folder:** `work/epics/completed/E-19-surface-alignment-and-compatibility-cleanup/`
- **Status:** all four milestones (m-E19-01 through m-E19-04) completed; epic→main merge pending; Blazor remains supported in parallel with E-11
- **Goal:** Tighten the remaining non-analytical legacy and compatibility surfaces across first-party UI, Sim, docs, schemas, and examples so current product surfaces stay aligned to one set of current contracts
- **Sequencing:** Runs after E-16, in parallel with resumed E-10 Phase 3 work by default; should not silently replace the `p3d` -> `p3c` -> `p3b` sequence
- **Key milestones:** supported surface inventory (completed) → runtime endpoint/client cleanup (completed) → schema/template/example retirement (completed) → Blazor support alignment (completed)
- **Reference:** `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md`

#### E-20 — Matrix Engine

- **Folder:** `work/epics/E-20-matrix-engine/`
- **Status:** In-progress (m-E20-01–07 complete; m-E20-08 parity harness, m-E20-09 per-class+edges, m-E20-10 artifact sink pending)
- **Goal:** Replace C# object-graph evaluation with Rust column-store + evaluation-plan engine. Standalone CLI binary (`flowtime-engine`). Foundation for E-17/E-18.
- **Depends on:** E-10 (complete), E-16 (complete)
- **Reference:** `docs/research/engine-rewrite-language-and-representation.md`

#### dag-map Library Evaluation (Spike)

- **Reference:** `docs/architecture/dag-map-evaluation.md`
- **Status:** Not started — runs in parallel with Phase 1+2
- **Goal:** Evaluate and extend the dag-map metro-map layout library for FlowTime topology rendering. ~2-3 days. Determines viability of SVG-based topology and informs UI Analytical Views and UI Layout Motors epics.

#### E-11 — Svelte UI (Parallel Frontend Track)

- **Folder:** `work/epics/E-11-svelte-ui/`
- **Status:** paused after M6 (M1-M4 + M6 done, M5/M7/M8 remain)
- **Goal:** Build SvelteKit + shadcn-svelte as a parallel UI surface for demos and future evaluation while Blazor remains supported. Independent of engine work.
- **dag-map:** M3 (topology rendering), M4 (heatmap mode) delivered. M5 (inspector) will need dag-map edge coloring and click events.

## Near-Term Epics

These depend on the analytical primitives from Phase 3 (except Telemetry Ingestion which is independent). dag-map enhancements are scoped within consuming milestones, not as a separate epic.

#### E-12 — Dependency Constraints & Shared Resources

- **Folder:** `work/epics/E-12-dependency-constraints/`
- **Goal:** Model downstream dependencies as resource constraints with visible bottlenecks and coupling.
- **Status:** M-10.01 and M-10.02 complete (Option A + B foundations). M-10.03 (MCP enforcement) deferred until runtime constraint enforcement (`p3d`) is in place. See `work/gaps.md`.
- **Depends on:** `p3d` (ConstraintAllocator wired into evaluation pipeline)

#### E-13 — Path Analysis & Subgraph Queries

- **Folder:** `work/epics/E-13-path-analysis/`
- **Goal:** Path-level queries and derived metrics (dominant routes, bottleneck attribution, path pain) for UI and MCP.
- **Depends on:** stable post-E-16 analytical facts, then `p3c` and `p3b` for the richer path diagnostics and what-if path work.
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
- **Validation datasets identified:** BPI Challenge 2012 (process mining), Road Traffic Fines, PeMS + OSM (road traffic), MTA + GTFS (transit). See `work/epics/E-15-telemetry-ingestion/reference/dataset-fitness-and-ingestion-research.md`.
- **Note:** Should preserve variability (Cv) when `p3c` ships, so ingestion format should be designed with this in mind.
- **Recommended follow-on:** `work/epics/telemetry-loop-parity/spec.md` before optimization, model fitting, or anomaly automation builds on ingested data.

## Bridge Work (post-purification, pre-advanced leverage)

These are the lowest-risk leverage layers after the E-16 truth gate. They increase usefulness without forcing live sessions or richer orchestration too early.

#### Scenario Overlays & What-If Runs

- **Folder:** `work/epics/overlays/`
- **Status:** Proposed — recommended after p3c + p3b
- **Goal:** Deterministic derived runs from a baseline via validated input patches (parallelism, capacity, arrivals, schedules) with explicit provenance and comparison.
- **Why early:** Clean bridge between a pure engine and scenario exploration; reuses existing run artifacts rather than requiring sessions or streaming state.

#### Telemetry Loop & Parity

- **Folder:** `work/epics/telemetry-loop-parity/`
- **Status:** Proposed — recommended immediately after the first E-15 dataset path
- **Goal:** Prove synthetic runs and telemetry replay runs match within defined tolerances before optimization, fitting, or anomaly automation builds on real data.
- **Why early:** Prevents higher-order features from normalizing ingestion drift.

## Post-Purification Epics (after E-16)

#### E-17 — Interactive What-If Mode

- **Folder:** `work/epics/E-17-interactive-what-if-mode/`
- **Status:** Future — depends on E-16
- **Goal:** Live interactive recalculation. Change a parameter via UI slider, see all metrics/charts/heatmaps update instantly (sub-50ms). No recompilation for parameter value changes. The spreadsheet comes alive.
- **Key milestones:** consume shared runtime parameter foundation, session & push channel, UI parameter controls
- **Shared foundation:** one runtime parameter model + reevaluation API, owned once and reused by E-18 and E-17.
- **Depends on:** E-16 plus the shared runtime parameter foundation (built in E-18 m-E18-01a/b/c)

#### E-18 — Time Machine

- **Folder:** `work/epics/E-18-headless-pipeline-and-optimization/`
- **Status:** Future — depends on E-16
- **Goal:** FlowTime as a client-agnostic callable machine for pipelines, optimization loops, model fitting against real telemetry, sensitivity analysis, AI iteration, and digital twin architectures.
- **Key milestones:** m-E18-01a Time Machine creation + Generator extraction, m-E18-01b tiered validation + telemetry source contract, m-E18-01c runtime parameter foundation + reevaluation, then CLI/sidecar, sweep & sensitivity, optimization & fitting, chunked evaluation, and telemetry source adapters
- **Depends on:** E-16 (pure compiled engine as evaluation function)
- **Analysis modes:** sweep, optimize, fit, sensitivity, Monte Carlo, feedback/chunked
- **Recommended sequencing:** m-E18-01a/b/c first, then E-17 session/push UX, then E-18's richer analysis modes.
- **Stateful extension note:** chunked evaluation belongs after a dedicated streaming/stateful seam exists; do not make it part of the first Time Machine cut.

## UI Paradigm Epics (draft — unnumbered until sequenced)

These epics implement the UI paradigm shift described in
`work/epics/ui-workbench/reference/ui-paradigm.md`. Blazor UI enters maintenance mode;
Svelte UI becomes the platform for these new interaction models.

#### UI Workbench & Topology Refinement

- **Folder:** `work/epics/ui-workbench/`
- **Goal:** Strip the topology DAG to structure + one color dimension. Build a workbench panel for pinning nodes/edges and inspecting metrics side-by-side.
- **Depends on:** E-11 M3-M4 (topology + timeline). Supersedes E-11 M5 (Inspector); does not require full E-11 completion.
- **Rendering:** SVG first; canvas only if measured performance problems.

#### UI Analytical Views

- **Folder:** `work/epics/ui-analytical-views/`
- **Goal:** Purpose-built views alongside topology: heatmap (nodes x bins grid), decomposition (cycle time breakdown + Kingman), comparison (two runs side-by-side), flow balance (conservation checks).
- **Depends on:** UI Workbench epic (view switcher), post-E-16 fact surfaces, and the relevant resumed E-10 primitives.
- **Absorbs:** E-14 (Visualizations). Role-focused chart bundles become presets within views.

#### UI Question-Driven Interface

- **Folder:** `work/epics/ui-question-driven/`
- **Goal:** Structured query panel where users ask analytical questions ("Where is the bottleneck?", "Why is cycle time high?") and get computed, provenanced answers. Foundation for future DSL and LLM integration.
- **Depends on:** UI Workbench epic, UI Analytical Views epic, post-E-16 fact surfaces, and the relevant resumed E-10 primitives.

## Mid-Term / Aspirational Epics (unnumbered until sequenced)

#### Flow-Aware Anomaly & Pathology Detection

- **Folder:** `work/epics/anomaly-detection/`
- **Goal:** Detect incidents and recurring flow pathologies (retry storms, slow drains, stuck queues) using the time-binned DAG model.
- **Depends on:** stable post-E-16 facts, resumed Phase 3 primitives, basic path-analysis context, and telemetry parity before automation against real telemetry.

#### UI Layout Motors (Pluggable Layout Engines)

- **Folder:** `work/epics/ui-layout/`
- **Goal:** Decouple topology layout from rendering behind a stable `LayoutInput -> LayoutResult` contract.
- **Depends on:** dag-map spike results.

#### Browser Execution / WASM Engine

- **Folder:** `work/epics/browser-execution/`
- **Status:** Future — preserved as a legacy design thread, not active scheduled work
- **Goal:** Explore a browser-hosted FlowTime runtime for offline demos and small-model interactive what-if workflows while preserving parity with server execution.
- **Relationship to current plan:** Downstream of `E-17` and `E-18`; browser execution is not the near-term path for interactive modeling.

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
