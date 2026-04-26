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
- **Status:** Complete (m-E20-01–10 all complete)
- **Goal:** Replace C# object-graph evaluation with Rust column-store + evaluation-plan engine. Standalone CLI binary (`flowtime-engine`). Foundation for E-17/E-18.
- **Depends on:** E-10 (complete), E-16 (complete)
- **Reference:** `docs/research/engine-rewrite-language-and-representation.md`

#### dag-map Library Evaluation (Spike)

- **Reference:** `docs/architecture/dag-map-evaluation.md`
- **Status:** Not started — runs in parallel with Phase 1+2
- **Goal:** Evaluate and extend the dag-map metro-map layout library for FlowTime topology rendering. ~2-3 days. Determines viability of SVG-based topology and informs UI Analytical Views and UI Layout Motors epics.

#### E-11 — Svelte UI (Parallel Frontend Track)

- **Folder:** `work/epics/E-11-svelte-ui/`
- **Status:** paused after M6; absorbed into **E-21**. M1-M4 + M6 delivered; M5 (Inspector) → E-21 workbench (m-E21-01/02), M7 (Dashboard) deferred, M8 (Polish) → E-21 m-E21-08. E-17 completed on Svelte (WebSocket bridge + parameter panel + topology heatmap + warnings + edge heatmap + time scrubber).
- **Goal:** Build SvelteKit + shadcn-svelte as a parallel UI surface for demos and future evaluation.
- **Fork decision (2026-04-15, active as of m-E18-14 wrap):** Svelte UI is the platform for all new telemetry/fit/discovery surfaces. Blazor UI is in maintenance mode at current functionality — bug fixes and contract alignment only, no new features. New Svelte work is tracked under E-21 below.
- **dag-map:** M3 (topology rendering), M4 (heatmap mode) delivered. Click/hover events + `selected` render option added in E-21 m-E21-01.

#### E-24 — Schema Alignment (completed)

- **Folder:** `work/epics/completed/E-24-schema-alignment/`
- **Status:** **Complete** — all five milestones merged to main (2026-04-25)
- **Goal:** Unify FlowTime's post-substitution model representation. One C# type (`ModelDto` + `ProvenanceDto` in `FlowTime.Contracts`), one YAML schema (`docs/schemas/model.schema.yaml`), one validator. `SimModelArtifact` and six satellites deleted; Sim emits the unified type directly; Engine parses it directly. `Template` (authoring-time) stays distinct. Forward-only — no bundle migration. camelCase throughout. `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` promoted to a hard `val-err == 0` build-time gate at `ValidationTier.Analyse`.
- **Five milestones:** m-E24-01 Inventory & Design Decisions (doc-only) → m-E24-02 Unify Model Type (`SimModelArtifact` + 6 satellites deleted; YamlDotNet 17.0.1) → m-E24-03 Schema Unification (schema rewritten top-to-bottom against unified `ModelDto`; nested 7-field camelCase provenance; consumer citations) → m-E24-04 Parser/Validator Scalar-Style Fix (mirrored `ParseScalar` `ScalarStyle.Plain` guard + sibling `QuotedAmbiguousStringEmitter` for round-trip symmetry) → m-E24-05 Canary Green + Hard Assertion (regression-catching verified end-to-end).
- **ADRs:** ADR-E-24-01 Unify (m-E24-01) · ADR-E-24-02 Forward-only regeneration (m-E24-01) · ADR-E-24-03 Schema declares only consumed fields (m-E24-01) · ADR-E-24-04 `ScalarStyle.Plain` gates `ParseScalar` (m-E24-04) · ADR-E-24-05 `QuotedAmbiguousStringEmitter` round-trip symmetry (m-E24-04).
- **Unblocks:** E-23 m-E23-02 + m-E23-03 (byte-trivial mechanical cleanup); m-E21-07 Validation Surface eventually; E-15 Telemetry Ingestion `nodes[].source` forward contract.
- **Decisions:** `D-2026-04-24-036` (E-23 paused, E-24 created) · `D-2026-04-24-037` (Option E ratified; 5-milestone plan) · `D-2026-04-25-038` (E-24 closed; E-23 ready to resume)
- **Reference:** `work/epics/completed/E-24-schema-alignment/spec.md`

#### E-23 — Model Validation Consolidation

- **Folder:** `work/epics/completed/E-23-model-validation-consolidation/`
- **Status:** **Completed and merged to main 2026-04-26; archived.** E-24 Schema Alignment closed; E-23 spirit reframed: make `model.schema.yaml` the only declarative source of structural truth and `ModelSchemaValidator` the only runtime evaluator — eliminate every "embedded schema" outside the canonical schema (`ModelValidator.cs` hand-rolled rules, parser tolerations, silent emission defaults, post-parse orchestration checks). E-24 fixed type + schema-document embedment; E-23 closed the rule-evaluator embedment. m-E23-01 audited 94 rules, landed 16 schema-add edits + the 5-arm `oneOf` schema restructure + the silent-error fallback + 12 named adjunct methods on `ModelSchemaValidator`, with a 26-test negative-case regression catalogue locking coverage. m-E23-02 migrated 3 production call sites + 28 test calls to `ModelSchemaValidator`, removed the `TimeMachineValidator` redundant-delegation block, and on close-out fixed a real `ProvenanceService.StripProvenance` round-trip bug surfaced by the new strict validator (Dictionary round-trip → YamlStream surgical removal; scalar styles preserved); +16 net new tests including a watertight `/v1/run`-level integration regression. m-E23-03 deleted `ModelValidator.cs` outright (`ValidationResult` relocated to its own file, namespace preserved); zero live references remain; full suite **1862 / 0 / 9** — identical to m-E23-02 tip. `ModelSchemaValidator.Validate` is now the single model-YAML validator in the codebase.
- **Goal:** Single declarative source (`model.schema.yaml`) + single runtime evaluator (`ModelSchemaValidator` with named adjuncts where JSON Schema draft-07 cannot express a rule). Delete `src/FlowTime.Core/Models/ModelValidator.cs` outright; route every call site (`POST /v1/run`, Engine CLI, `TimeMachineValidator`, tests) through `ModelSchemaValidator`. Directly enforces the 2026-04-23 Truth Discipline guard *"'API stability' does not mean 'keep old functions around.'"*
- **Milestone slate:** m-E23-01 Rule-Coverage Audit (replaces original m-E23-01-schema-alignment which is absorbed by E-24 m-E24-03/m-E24-05; new milestone audits every rule embedment across `ModelValidator.cs`, `ModelParser.cs`, `SimModelBuilder.cs`, and post-parse orchestration; per-rule disposition; schema/adjunct additions land; negative-case canary locks coverage) · m-E23-02 Call-Site Migration · m-E23-03 Delete `ModelValidator`
- **Stashed input material:** branch `milestone/m-E23-01-schema-alignment` + `stash@{0}` (pre-pivot m-E23-01 work, mostly absorbed by E-24); should be retired when E-23 resumes — the rule audit starts fresh from post-E-24 `main`
- **Dependencies:** E-24 Schema Alignment (cleared 2026-04-25)
- **Unblocks (after E-23 closes):** m-E21-07 Validation Surface (Svelte)
- **Regression canaries:** `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (E-24 m-E24-05 hard-asserts `val-err == 0` across 12 templates) + new `RuleCoverageRegressionTests` from m-E23-01 (negative-case proof that `ModelSchemaValidator` catches every audited rule)
- **Reference:** `work/epics/E-23-model-validation-consolidation/spec.md`

#### E-21 — Svelte Workbench & Analysis Surfaces

- **Folder:** `work/epics/E-21-svelte-workbench-and-analysis/`
- **Status:** **Resumed (2026-04-26)** (branch `epic/E-21-svelte-workbench-and-analysis`) — paused 2026-04-23 to run E-24 then E-23; both closed. m-E21-01/02/03/04/05/06 all complete and merged into the epic branch (m-E21-01/02 merged 2026-04-17; m-E21-03 merged 2026-04-17 with ultrareview follow-ups 2026-04-20; m-E21-04 completed 2026-04-22; m-E21-05 completed 2026-04-22; m-E21-06 Heatmap View completed 2026-04-24, **merged into the epic branch 2026-04-26** as a backfill of the missing wrap-time merge; main caught up onto the epic branch in the same pass). Scope split 2026-04-21 during m-E21-04: Optimize moved to m-E21-05; downstream renumbered heatmap → m-E21-06, validation → m-E21-07, polish → m-E21-08 (epic now has 8 milestones, was 7). **Reentry point:** m-E21-07 Validation Surface, which consumes the consolidated `ModelSchemaValidator` E-23 delivers. **Remaining:** m-E21-07 Validation Surface, m-E21-08 Polish.
- **Goal:** Turn the Svelte UI into the primary platform for expert flow analysis and Time Machine surfaces. Workbench paradigm: topology as navigation + click-to-pin inspection; `/analysis` route with tabbed Time Machine surfaces; heatmap view; validation surface; compact density with calm chrome + vivid data-viz palette.
- **Depends on:** E-11 (M1-M4 + M6), E-17, E-18 analysis endpoints (`/v1/sweep`, `/v1/sensitivity`, `/v1/goal-seek`, `/v1/optimize`, `/v1/validate`).
- **Supersedes:** E-11 M5 (evolved into m-E21-01/02 workbench paradigm), M7 (deferred), M8 (absorbed into m-E21-08).

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

- **Folder:** `work/epics/completed/E-17-interactive-what-if-mode/`
- **Status:** Complete — merged to main 2026-04-12
- **Goal:** Live interactive recalculation. Change a parameter via UI slider, see all metrics/charts/heatmaps update instantly (sub-50ms). No recompilation for parameter value changes. The spreadsheet comes alive.
- **Delivered:** 6 milestones — WebSocket bridge, parameter panel, topology heatmap, warnings surface, edge heatmap, time scrubber. Advanced demo models. 200 vitest + 26 Playwright E2E.
- **Depends on:** E-20 (matrix engine — complete)

#### E-18 — Time Machine

- **Folder:** `work/epics/E-18-headless-pipeline-and-optimization/`
- **Status:** In-progress (on `epic/E-18-time-machine`)
- **Gap analysis:** `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md`
- **Goal:** FlowTime as a client-agnostic callable machine for pipelines, optimization loops, model fitting against real telemetry, sensitivity analysis, AI iteration, and digital twin architectures.
- **Completed milestones:**
  - m-E18-01: Parameterized evaluation (Rust) — ParamTable, evaluate_with_params, compile-once eval-many
  - m-E18-02: Engine session + streaming protocol (Rust) — persistent process, MessagePack over stdin/stdout
  - m-E18-06: Tiered validation — `TimeMachineValidator`, `POST /v1/validate`, Rust `validate_schema`
  - m-E18-07: `FlowTime.TimeMachine` created, `FlowTime.Generator` deleted (Path B, no coexistence window)
  - m-E18-08: `ITelemetrySource` interface + `CanonicalBundleSource` + `FileCsvSource`
  - m-E18-09: Parameter sweep — `SweepSpec`/`SweepRunner`/`ConstNodePatcher`, `IModelEvaluator`/`RustModelEvaluator`, `POST /v1/sweep`
  - m-E18-10: Sensitivity analysis — `ConstNodeReader`, `SensitivityRunner` (central difference), `POST /v1/sensitivity`
  - m-E18-11: Goal seeking — `GoalSeeker` (bisection), `POST /v1/goal-seek` *(added; not in original spec)*
  - m-E18-12: Optimization — `Optimizer` (Nelder-Mead, N params), `POST /v1/optimize`
  - m-E18-13: SessionModelEvaluator — compile-once persistent-subprocess bridge; `RustEngine:UseSession` config switch (default true); `RustModelEvaluator` retained as fallback
  - m-E18-14: .NET Time Machine CLI — `flowtime validate/sweep/sensitivity/goal-seek/optimize` as pipeable JSON-over-stdio commands, byte-compatible with `/v1/` endpoints; `--no-session` fallback
- **Active delivery sequence (decided 2026-04-15):**
  1. **UI parity fork** — Svelte platform for new telemetry/fit/discovery surfaces; Blazor enters maintenance mode. Parallel track with E-15 below.
  2. **E-15 Telemetry Ingestion** — Gold Builder → Graph Builder → first dataset path. Critical path for the client-telemetry vision.
  3. **Telemetry Loop & Parity** — parity harness validates synthetic-vs-replay drift bounds.
  4. **E-22 Model Fit + Chunked Evaluation** — carries forward the remaining E-18 scope as a dedicated epic; Fit (`FitSpec`/`FitRunner`/`POST /v1/fit`), chunked stateful session protocol, and the `FlowTime.Pipeline` embeddable SDK wrapper.
- **Deferred with no owner milestone (tracked in `work/gaps.md`):**
  - Optimization constraints (penalty method on `OptimizeSpec`)
  - Monte Carlo (sampling layer on `IModelEvaluator`)
  - `FlowTime.Telemetry.*` adapter projects (Prometheus, OTEL, BPI) — direct-source `ITelemetrySource` implementations that bypass the E-15 Gold Builder pipeline for specific live sources; narrow bypasses, not part of E-15 scope. E-15 is the general path (raw → Gold → `CanonicalBundleSource`); adapters are shortcuts for clients already on specific telemetry stacks.
- **Depends on:** E-16, E-20 (both complete)

#### E-22 — Time Machine: Model Fit & Chunked Evaluation

- **Folder:** `work/epics/E-22-model-fit-chunked-evaluation/`
- **Status:** planning
- **Goal:** Close out the remaining Time Machine analysis modes (model fit against real telemetry, chunked evaluation for feedback simulation) and crystallize the surface as a `FlowTime.Pipeline` embeddable SDK.
- **Depends on:** E-15 Telemetry Ingestion, Telemetry Loop & Parity (both prerequisites for Fit per Option A).
- **Planned milestones:**
  - m-E22-01 Model Fit — `FitSpec`/`FitRunner`/`POST /v1/fit`; `flowtime fit` CLI
  - m-E22-02 Chunked Evaluation — Rust `chunk_step` session command; `POST /v1/chunked-eval`; external-controller integration
  - m-E22-03 `FlowTime.Pipeline` SDK — embeddable library wrapping all analysis modes; existing API/CLI callers dogfood the SDK
- **Out of scope (gaps):** optimization constraints, Monte Carlo, `FlowTime.Telemetry.*` direct-source adapters, tiered validation parity across UIs/MCP.

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

#### Cloud Deployment & Data Pipeline Integration

- **Folder:** not yet created (aspirational)
- **Goal:** Azure-native deployment shapes so client telemetry in ADX / Blob can drive FlowTime batch or event-driven analysis. Three anticipated shapes:
  1. **Scheduled batch** — timer-triggered Azure Function or Container Apps job: queries ADX, runs FlowTime.TimeMachine fit/sweep/sensitivity, writes results back.
  2. **Event-driven** — Event Grid / Service Bus triggers evaluation on a new telemetry window; results push to dashboards / downstream systems.
  3. **Long-running interactive service** — Container App hosting the ASP.NET API for Svelte UI what-if exploration.
- **Depends on:** m-E18-14 (CLI as pipeline entry point); E-15 (telemetry ingestion + canonical source adapters); Telemetry Loop & Parity (for validated fit); cloud `ITelemetrySource` implementations (`AdxTelemetrySource`, `BlobTelemetrySource`, `EventHubsTelemetrySource`); Blob artifact sink; OTEL / App Insights integration.
- **Existing architecture that already fits:** Rust engine as standalone binary; `IModelEvaluator` seam (subprocess / HTTP / FFI interchangeable); `ITelemetrySource` seam (cloud adapters additive); analysis modes as a .NET library callable from any host (Functions / ACI / Container Apps); three-layer engine architecture (D-2026-04-10-031) supporting pluggable sinks.
- **Note on evaluator choice:** `SessionModelEvaluator` (persistent subprocess, compile-once) fits long-running jobs. `RustModelEvaluator` (stateless subprocess per eval) fits Azure Functions where each invocation is short-lived and process isolation is a feature. Both implementations are retained (m-E18-13).
- **Status:** Aspirational — not scheduled. Marker so near-term work (CLI, ITelemetrySource, artifact sink, observability) stays shaped for these scenarios. Concrete Azure work begins only when a specific client deployment target is chosen.

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
