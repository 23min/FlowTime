# FlowTime Roadmap — Updated 2026-04-07

This roadmap reflects the current state of FlowTime Engine + Sim and the strategic direction established during the E-16 planning cycle. Architecture **epics** and milestone docs provide the implementation detail (see `work/epics/epic-roadmap.md`).

## Scope & Assumptions
- Engine remains responsible for deterministic execution, artifact generation, and `/state` APIs (see `docs/flowtime-engine-charter.md`).
- FlowTime.Sim owns template authoring, stochastic inputs, and template catalog endpoints.
- Product-level scope is summarized in `docs/flowtime-charter.md`.
- The engine deep review (`docs/architecture/reviews/engine-deep-review-2026-03.md`) is the primary input for correctness priorities.

## Thesis: Pure Engine, Then Power

FlowTime is a **spreadsheet for flow dynamics** — a deterministic graph of pure transforms over named time series. Queueing theory made executable.

The strategic arc is three phases:

1. **Make it pure (E-16).** The engine's analytical identity and semantic meaning are still reconstructed late from strings in the API and UI. E-16 moves all of that into the compiled Core: typed references, compiled analytical descriptors, pure evaluation. After E-16, the engine is an honest formula evaluator — the compiler owns meaning, evaluation is pure, consumers read facts.

2. **Make it interactive (E-17).** Once the engine is pure and evaluation takes microseconds, live what-if becomes possible. Change a parameter, see every metric update instantly. The spreadsheet comes alive. This needs runtime parameter identification, server-side sessions, and a push channel to the UI.

3. **Make it programmable (E-18).** Once the engine is a callable pure function, embed it in pipelines. Parameter sweeps, optimization loops, model fitting against real telemetry, sensitivity analysis, digital twin architectures. FlowTime becomes an instrument, not just a simulator.

This arc describes the product capability ladder, not strict implementation order. In implementation, the shared runtime parameter foundation lands first in the E-18 Time Machine foundation and is then consumed by E-17's session/push UX. E-16 completes first, then E-10 resumes, then E-12-E-15 build on the analytical layer.

## Delivered (Completed Epics)

9 epics completed. See `work/epics/completed/` for full specs.

- **Time Travel V1** — `/state`, `/state_window` APIs, telemetry capture/bundles, DLQ/backlog semantics.
- **Evaluation Integrity** — Compile-to-DAG contract, centralized model compiler.
- **Edge Time Bins** — Per-edge throughput/attempt volumes, conservation checks, UI overlays.
- **Classes & Routing** — Multi-class flows with class-aware routing and visualization.
- **Service With Buffer** — First-class `serviceWithBuffer` node type replacing legacy backlog.
- **MCP Modeling & Analysis** — Draft/validate/run/inspect loop, data intake, profile fitting, storage.
- **Engine Semantics Layer** — Stable `/state`, `/state_window`, `/graph` contracts.
- **UI Performance** — Input/paint/data lane separation, eliminated main-thread stalls.
- **Package Updates** — .NET 9 dependencies and MudBlazor updated (M-11.01, M-11.02).

## E-10 — Engine Correctness & Analytical Primitives (completed)

**Epic:** `work/epics/completed/E-10-engine-correctness-and-analytics/spec.md`
**Status:** Complete — all 8 milestones delivered

The engine deep review found 3 P0 correctness bugs, engineering debt, documentation drift, and a missing analytical layer. All phases delivered: Phases 0-2 (bugs, engineering, docs), Phase 3 analytical primitives (cycle time, projection hardening, constraint enforcement, variability, WIP limits with overflow routing and SHIFT-based backpressure feedback).

## E-16 — Formula-First Core Purification (completed)

**Epic:** `work/epics/completed/E-16-formula-first-core-purification/spec.md` | **Status:** completed (`m-E16-06` completed on `milestone/m-E16-06-analytical-contract-and-consumer-purification`)

The architecture gate is complete. Semantic meaning and analytical truth are now compiled into Core once and consumed as facts everywhere else.

Six milestones in sequence:
1. **m-E16-01** — Compiled Semantic References (typed refs replace raw string parsing, Parallelism typing) — completed on `milestone/m-E16-01-compiled-semantic-references`
2. **m-E16-02** — Class Truth Boundary (real by-class data vs wildcard fallback made explicit) — completed on `milestone/m-E16-01-compiled-semantic-references`
3. **m-E16-03** — Runtime Analytical Descriptor (absorbs AnalyticalCapabilities, compiled by compiler not resolved from strings) — completed on `milestone/m-E16-01-compiled-semantic-references`
4. **m-E16-04** — Core Analytical Evaluation (all analytical math moves to Core including flowLatencyMs graph propagation) — completed on `milestone/m-E16-01-compiled-semantic-references`
5. **m-E16-05** — Warning Facts & Primitive Cleanup (backlog/stationarity/overload warnings move to Core analyzers) — completed on `milestone/m-E16-05-analytical-warning-facts-and-primitive-cleanup`
6. **m-E16-06** — Contract & Consumer Purification (publish facts in API, delete IsServiceLike/Classify heuristics from UI) — completed on `milestone/m-E16-06-analytical-contract-and-consumer-purification`

Key decisions: D-2026-04-03-005 (flowLatencyMs to Core), D-2026-04-03-006 (descriptor absorbs AnalyticalCapabilities), D-2026-04-03-007 (Parallelism typing in E-16). See `work/decisions.md`.

Migration is forward-only. Existing runs, fixtures, and approved snapshots are regenerated, not compatibility-layered.

## E-11 — Svelte UI (Parallel Frontend Track)

**Epic:** `work/epics/E-11-svelte-ui/spec.md` | **Status:** paused after M6; absorbed into E-21 (M1-M4 + M6 done; M5 → E-21 workbench, M7 deferred, M8 → E-21 m-E21-08)

Build a parallel SvelteKit + shadcn-svelte UI surface for demos and future evaluation while keeping the Blazor UI supported and in sync. Independent of engine work — both UIs consume existing APIs with zero backend changes.

Superseded on 2026-04-15 (fork decision): Svelte becomes the platform for all new surfaces and Blazor enters maintenance mode. Remaining work moved to **E-21 — Svelte Workbench & Analysis Surfaces** below.

## E-24 — Schema Alignment (completed)

**Epic:** `work/epics/completed/E-24-schema-alignment/spec.md` | **Status:** completed — all five milestones merged to main (2026-04-25)

Unified FlowTime's post-substitution model representation. One C# type (`ModelDto` + `ProvenanceDto` in `FlowTime.Contracts`), one YAML schema (`docs/schemas/model.schema.yaml` rewritten against the unified type), one validator. `SimModelArtifact` and its six satellites deleted; Sim emits the unified type directly; Engine parses it directly. `Template` (authoring-time) stays distinct. Forward-only — no migration of stored bundles. camelCase throughout. The `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` canary is now a hard `val-err == 0` build-time gate at `ValidationTier.Analyse` across all twelve shipped templates.

**Five milestones:** m-E24-01 Inventory & Design Decisions (doc-only) → m-E24-02 Unify Model Type (`SimModelArtifact` + 6 satellites deleted; YamlDotNet 17.0.1) → m-E24-03 Schema Unification (schema rewritten top-to-bottom; nested 7-field camelCase provenance; consumer citations on every property) → m-E24-04 Parser/Validator Scalar-Style Fix (mirrored `ParseScalar` `ScalarStyle.Plain` guard in both validators + sibling `QuotedAmbiguousStringEmitter` for round-trip symmetry) → m-E24-05 Canary Green + Hard Assertion (regression-catching verified end-to-end).

**ADRs:** ADR-E-24-01 Unify the post-substitution model type · ADR-E-24-02 Forward-only regeneration · ADR-E-24-03 Schema declares only consumed fields · ADR-E-24-04 `ScalarStyle.Plain` gates `ParseScalar` coercion · ADR-E-24-05 `QuotedAmbiguousStringEmitter` round-trip symmetry.

**Decisions:** `D-2026-04-24-036` (E-23 paused, E-24 created) · `D-2026-04-24-037` (Option E ratified; 5-milestone plan) · `D-2026-04-25-038` (E-24 closed; E-23 ready to resume).

**Unblocks:** E-23 m-E23-02 (call-site migration) and m-E23-03 (`ModelValidator` delete) become byte-trivial mechanical cleanup; m-E21-07 Validation Surface eventually; E-15 Telemetry Ingestion `nodes[].source` forward contract.

## E-23 — Model Validation Consolidation (in-progress — m-E23-01 started 2026-04-26)

**Epic:** `work/epics/E-23-model-validation-consolidation/spec.md` | **Status:** in-progress (m-E23-01 started 2026-04-26 on `milestone/m-E23-01-rule-coverage-audit`). Rescoped 2026-04-26 — E-24 Schema Alignment closed (all five milestones landed on `epic/E-24-schema-alignment`). E-23's spirit reframed: make `model.schema.yaml` the only declarative source of structural truth and `ModelSchemaValidator` the only runtime evaluator — eliminate every "embedded schema" outside the canonical schema (`ModelValidator.cs` hand-rolled rules, parser tolerations, silent emission defaults, post-parse orchestration checks). E-24 fixed type + schema-document embedment; E-23 closes the rule-evaluator embedment.

Mini-epic (3 milestones). Collapses the codebase's two silently-disagreeing model validators to one: `ModelValidator` is **deleted**, `ModelSchemaValidator` is the single schema-driven entry point. Directly enforces the 2026-04-23 Truth Discipline guard *"'API stability' does not mean 'keep old functions around.'"*

**Milestone slate (3 milestones):**
- m-E23-01 Rule-Coverage Audit — doc-only audit of every rule embedded in `ModelValidator.cs`, `ModelParser.cs`, `SimModelBuilder.cs`, and post-parse orchestration. Per-rule disposition (schema-covered / schema-add / adjunct / parser-justified / drop); schema additions and `ModelSchemaValidator` adjuncts land where the audit needs them; negative-case canary `RuleCoverageRegressionTests` locks coverage in. **Replaces** the original m-E23-01-schema-alignment milestone — schema-alignment work was absorbed by E-24 m-E24-03/m-E24-05; the unowned piece (`ModelValidator` rule audit) becomes the new m-E23-01 focus. Slug change also avoids collision with E-24's "schema-alignment" slug. **Status: in-progress (started 2026-04-26).**
- m-E23-02 Call-Site Migration — switch `POST /v1/run`, Engine CLI, `TimeMachineValidator` tier-1 + four test files to `ModelSchemaValidator.Validate`; error-phrasing audit; `ModelValidator.cs` left on disk as single-revert safety net.
- m-E23-03 Delete `ModelValidator` — delete the file; move `ValidationResult` to its own file; assert grep returns zero callers; archive E-23.

**Out of scope (firm):** Sim's emission shape (E-24 territory; E-23 only revisits if the audit shows an unwritten emission rule), Blazor/Svelte UI code, active validation UI (lives in m-E21-07 after E-21 resumes), new validator features, `Template`-layer validation (`TemplateSchemaValidator` stays distinct for pre-substitution authoring templates).

**Stashed input material:** branch `milestone/m-E23-01-schema-alignment` + `stash@{0}` hold pre-pivot m-E23-01 work. Most absorbed by E-24; should be retired when E-23 resumes — the rule audit starts fresh from post-E-24 `main`.

**Dependencies:** E-24 Schema Alignment (cleared 2026-04-25). After E-23 lands, m-E21-07 Validation Surface resumes with a single consolidated validator to render.

## E-21 — Svelte Workbench & Analysis Surfaces (paused — interrupted by E-23)

**Epic:** `work/epics/E-21-svelte-workbench-and-analysis/spec.md` | **Status:** paused 2026-04-23 — interrupted to run E-23 Model Validation Consolidation. m-E21-06 completed on branch `milestone/m-E21-06-heatmap-view`; merge into `epic/E-21-svelte-workbench-and-analysis` deferred until E-21 resumes. Reentry point: m-E21-07 Validation Surface, which consumes the consolidated `ModelSchemaValidator` E-23 delivers.

Transform the Svelte UI from a Blazor-parallel clone into the primary platform for expert flow analysis and Time Machine surfaces. Workbench paradigm: topology as navigation + click-to-pin inspection panel; `/analysis` route with tabbed Time Machine surfaces (sweep, sensitivity, goal-seek, optimize); heatmap view; validation surface; compact density with calm chrome + vivid data-viz palette.

**Depends on:** E-11 (M1-M4 + M6), E-17, E-18 analysis endpoints.

**Completed milestones:**
- m-E21-01: Workbench Foundation — density tokens, dag-map `bindEvents`/`selected` (library), click-to-pin node cards (merged 2026-04-17)
- m-E21-02: Metric Selector & Edge Cards — metric chip bar, edge cards, class filter, custom TimelineScrubber (merged 2026-04-17)
- m-E21-03: Sweep & Sensitivity Surfaces — `/analysis` route with tabs, sweep config + results, sensitivity bar chart (merged 2026-04-17; ultrareview follow-ups 2026-04-20)
- m-E21-04: Goal Seek Surface — goal-seek panel on `/analysis`, shared `AnalysisResultCard` + `ConvergenceChart` components, additive `trace` on `/v1/goal-seek` and `/v1/optimize` per D-2026-04-21-034 (completed 2026-04-22)
- m-E21-05: Optimize Surface — live `/v1/optimize` wired to the `/analysis` Optimize tab, N-param Nelder-Mead under bounds, per-param result table with range bars, new `flowtime.optimize(...)` client, sibling `optimize-helpers.ts` module (completed 2026-04-22)
- m-E21-06: Heatmap View — nodes-x-bins grid as sibling of topology under `/time-travel/topology`, typed `<ViewSwitcher>` (inline views, no registry per ADR-m-E21-06-01), shared view-state store, shared full-window 99p-clipped color-scale normalization (topology straight-swaps from per-bin per ADR-m-E21-06-02), shared-toolbar `[ Operational | Full ]` node-mode toggle reaching Blazor parity. 15/15 ACs; 770 ui-vitest (+269) across 32 suites; 16 Playwright specs on `svelte-heatmap.spec.ts`; zero backend work (completed 2026-04-24)

**Remaining:** m-E21-07 Validation Surface, m-E21-08 Polish.

## E-19 — Surface Alignment & Compatibility Cleanup (completed)

**Epic:** `work/epics/completed/E-19-surface-alignment-and-compatibility-cleanup/spec.md` | **Status:** completed — all four milestones merged to main (2026-04-08)

After E-16 purifies analytical truth, FlowTime still carries broader non-analytical compatibility debt across first-party UI, Sim, docs, examples, and schema surfaces. E-19 removes stale fallback layers and clarifies supported surfaces in a forward-only cut while keeping Blazor current as a supported parallel UI.

This cleanup lane also draws the boundary between today's Sim authoring/orchestration residue and the future E-18 Time Machine foundation, so the current Sim path does not harden into the default programmable contract.

This epic starts immediately after E-16 as a cleanup lane, but it does not replace E-10 Phase 3 resume. Runtime/schema/doc cleanup can run in parallel with resumed analytical work, and Blazor alignment runs alongside the E-11 Svelte track rather than behind a replacement cutoff.

## Near-Term Epics

These depend on the analytical primitives from E-10 Phase 3 (except E-15 which is independent):

1. **E-12 — Dependency Constraints & Shared Resources** (`work/epics/E-12-dependency-constraints/`)
   - Runtime constraint enforcement (depends on Phase 3 p3d). M-10.01/02 complete. M-10.03 deferred.

2. **E-13 — Path Analysis & Subgraph Queries** (`work/epics/E-13-path-analysis/`)
   - Path-level queries, bottleneck attribution, dominant routes, path pain.

3. **E-14 — Visualizations** (`work/epics/E-14-visualizations/`)
   - Absorbed into UI Analytical Views epic. See `work/epics/ui-analytical-views/spec.md`.

4. **E-15 — Telemetry Ingestion, Topology Inference + Canonical Bundles** (`work/epics/E-15-telemetry-ingestion/`)
   - Gold Builder + Graph Builder + bundle assembly. Independent of Phase 3.

## Bridge Work (recommended before advanced leverage)

These are the lowest-risk leverage layers after purification. They make the pure engine more useful without forcing live sessions, streaming state, or optimization frameworks too early.

1. **Scenario Overlays & What-If Runs** (`work/epics/overlays/overlays.md`)
   - Deterministic derived runs created from a baseline via validated input patches. Recommended after p3c + p3b so variability- and WIP-aware experiments have a clean execution path.

2. **Telemetry Loop & Parity** (`work/epics/telemetry-loop-parity/spec.md`)
   - Automated parity harness between baseline synthetic runs and telemetry replay runs. Recommended immediately after the first E-15 dataset path and before model fitting, optimization, or anomaly automation.

## E-20 — Matrix Engine (complete)

**Epic:** `work/epics/E-20-matrix-engine/spec.md` | **Status:** complete (m-E20-01–10 all complete)

Replace the C# object-graph evaluation with a Rust column-store + evaluation-plan engine. All series live in one flat `f64[series_count × bins]` matrix. The evaluation plan is an ordered list of ops (pure functions on columns). Ships as a standalone CLI binary (`flowtime-engine eval/validate/plan`). The .NET API calls the Rust binary as a subprocess.

Three-layer architecture (D-2026-04-10-031): engine core (pure function) → artifact sink (mandatory, pluggable persistence) → consumer adapters (per-surface formatting). All 10 milestones complete. The Rust engine replaces `RunArtifactWriter`. E-17/E-18 are unblocked.

**Depends on:** E-10 (complete), E-16 (complete)

## E-17 — Interactive What-If Mode (complete)

**Epic:** `work/epics/completed/E-17-interactive-what-if-mode/spec.md` | **Status:** complete | **Merged:** 2026-04-12

Live interactive recalculation: change a parameter, see every metric update instantly (<50ms). 6 milestones: WebSocket bridge → parameter panel → topology heatmap → warnings surface → edge heatmap → time scrubber. Advanced demo models (SaaS API, e-commerce pipeline). 200 vitest + 26 Playwright E2E.

**Depends on:** E-20

## E-18 — Time Machine (in-progress)

**Epic:** `work/epics/E-18-headless-pipeline-and-optimization/spec.md` | **Status:** in-progress (branch `epic/E-18-time-machine`)
**Gap analysis:** `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md`

FlowTime as a callable pure function in pipelines, optimization loops, model fitting, digital twin architectures.

**Depends on:** E-20 (complete)

**Completed milestones:**
- m-E18-01: Parameterized evaluation (Rust) — ParamTable, evaluate_with_params, compile-once eval-many
- m-E18-02: Engine session + streaming protocol (Rust) — persistent process, MessagePack over stdin/stdout
- m-E18-06: Tiered validation — `TimeMachineValidator` (schema/compile/analyse), `POST /v1/validate`, Rust `validate_schema`
- m-E18-07: `FlowTime.TimeMachine` project created; `FlowTime.Generator` deleted (Path B)
- m-E18-08: `ITelemetrySource` interface + `CanonicalBundleSource` + `FileCsvSource`
- m-E18-09: Parameter sweep — `SweepSpec`/`SweepRunner`/`ConstNodePatcher`, `IModelEvaluator`, `POST /v1/sweep`
- m-E18-10: Sensitivity analysis — `ConstNodeReader`, `SensitivityRunner` (central difference), `POST /v1/sensitivity`
- m-E18-11: Goal seeking — `GoalSeeker` (bisection), `POST /v1/goal-seek` *(added; not in original spec)*
- m-E18-12: Optimization — `Optimizer` (Nelder-Mead, N params), `POST /v1/optimize`
- m-E18-13: SessionModelEvaluator — compile-once persistent-subprocess bridge using m-E18-02 session protocol; `RustEngine:UseSession` config switch (default true); `RustModelEvaluator` retained as fallback
- m-E18-14: .NET Time Machine CLI — `flowtime validate/sweep/sensitivity/goal-seek/optimize` as pipeable JSON-over-stdio commands, byte-compatible with `/v1/` endpoints; `--no-session` fallback

**Active delivery sequence (decided 2026-04-15):**

1. **UI parity fork** — Svelte UI becomes the platform for new telemetry/fit/discovery surfaces. Blazor enters maintenance mode at current functionality. Parallel track with E-15 below.
2. **E-15 Telemetry Ingestion** — Gold Builder (raw → canonical bundle) → Graph Builder (telemetry → inferred topology) → first dataset path. Critical path for the client-telemetry vision.
3. **Telemetry Loop & Parity** — parity harness validates synthetic-vs-replay drift bounds. Required before fit results are trustworthy.
4. **E-22 Model Fit + Chunked Evaluation** — carries forward the remaining E-18 scope (`FitSpec`/`FitRunner`/`POST /v1/fit` and chunked stateful session protocol) plus the `FlowTime.Pipeline` embeddable SDK wrapper. Completes the discovery pipeline and crystallizes the embeddable surface. See epic: `work/epics/E-22-model-fit-chunked-evaluation/spec.md`.

**Deferred with no owner milestone (tracked in `work/gaps.md`):**
- Optimization constraints (penalty method on `OptimizeSpec`)
- Monte Carlo (sampling layer on `IModelEvaluator`)
- `FlowTime.Telemetry.*` adapter projects (Prometheus, OTEL, BPI) — direct-source `ITelemetrySource` implementations that bypass the E-15 Gold Builder pipeline for specific live sources; narrow bypasses, not part of E-15 scope

## E-22 — Time Machine: Model Fit & Chunked Evaluation (planning)

**Epic:** `work/epics/E-22-model-fit-chunked-evaluation/spec.md` | **Status:** planning

Closes out the remaining Time Machine analysis modes from E-18: model fitting against real telemetry, chunked evaluation for feedback simulation, and the `FlowTime.Pipeline` embeddable SDK wrapper. Completes the "FlowTime as a callable function" arc.

**Depends on:** E-15 Telemetry Ingestion (first dataset path), Telemetry Loop & Parity (validated drift bounds). Sequenced after both per D-2026-04-15-032 Option A.

**Planned milestones:**
- m-E22-01 Model Fit — `FitSpec`/`FitRunner`/`POST /v1/fit` composing `ITelemetrySource` + `Optimizer`; `flowtime fit` CLI
- m-E22-02 Chunked Evaluation — Rust `chunk_step` session command; `POST /v1/chunked-eval`; external-controller integration
- m-E22-03 `FlowTime.Pipeline` SDK — embeddable library wrapping all analysis modes; existing API/CLI callers rewritten to dogfood the SDK

**Out of scope (tracked as gaps):** optimization constraints, Monte Carlo, `FlowTime.Telemetry.*` direct-source adapters, tiered validation parity across UIs/MCP.

## UI Paradigm Epics (draft — unnumbered until sequenced)

See `work/epics/ui-workbench/reference/ui-paradigm.md` for the architectural proposal.

- **UI Workbench & Topology Refinement** — Strip topology to structure + one color dimension, workbench panel for inspection.
- **UI Analytical Views** — Purpose-built views: heatmap, decomposition, comparison, flow balance. Absorbs E-14.
- **UI Question-Driven Interface** — Structured query panel for analytical questions with provenanced answers.

## Mid-Term / Aspirational

| Epic | Key Dependency | Notes |
|------|---------------|-------|
| **Anomaly & Pathology Detection** | Phase 3 + path/parity basics | Needs analytical primitives plus basic path context and telemetry parity before real-data automation |
| **UI Layout Motors** | dag-map spike | Pluggable layout engines behind stable contract |
| **Ptolemy-Inspired Semantics** | — | Conceptual guardrails for engine evolution |
| **Streaming & Subsystems** | Stable engine semantics | Long-term exploratory |
| **Cloud Deployment & Data Pipeline Integration** | E-15 + m-E18-14 CLI | Azure-native shape: Functions / Container Apps / ACI jobs. See below. |

### Cloud Deployment & Data Pipeline Integration (aspirational)

A natural deployment target for FlowTime is an Azure-hosted data pipeline where client
telemetry lands in ADX or Blob, and FlowTime runs batch or event-driven analysis against it.
This section captures the aspirational shape so that current architectural decisions stay
compatible with it — without yet committing to implementation.

**Three deployment shapes anticipated:**

1. **Scheduled batch.** Timer-triggered Azure Function (or Container Apps job) queries ADX,
   loads canonical series via `ITelemetrySource`, runs FlowTime.TimeMachine fit / sweep /
   sensitivity, writes results back to ADX or Blob.
2. **Event-driven.** Event Grid / Service Bus triggers a Function on a new telemetry window;
   FlowTime evaluates; results push to a dashboard or downstream system.
3. **Long-running interactive service.** Container App hosting the existing ASP.NET API for
   Svelte UI what-if exploration. Separate process from the batch pipeline.

**What the current architecture already gets right:**
- Rust engine as a standalone binary — language-neutral, callable any way
- `IModelEvaluator` seam — swap subprocess for HTTP client, FFI, or WASM without changing analysis code
- `ITelemetrySource` seam — cloud adapters (ADX, Blob, Event Hubs) are additive
- Analysis modes as a library (`FlowTime.TimeMachine`) — callable from any .NET host, not tied to the API server
- Three-layer engine architecture (D-2026-04-10-031) — engine / sink / consumer separation supports Blob sinks

**What we expect to add when Azure becomes concrete:**
- **Pipeline-grade .NET CLI (m-E18-14 will start this).** Stdin JSON in / stdout JSON out. Azure Functions custom-handler-compatible. Self-contained binary deployable to ACI.
- **Cloud `ITelemetrySource` adapters.** `AdxTelemetrySource`, `BlobTelemetrySource`, `EventHubsTelemetrySource`. Additive to the existing interface.
- **Blob-backed artifact sink.** Parallel implementation of the filesystem sink under the same directory contract.
- **OTEL / App Insights integration.** Structured spans around evaluator calls, sweeps, fits — long-running operations need observability.
- **Key Vault secrets integration.** ADX connection strings, SAS tokens via standard Azure identity patterns.

**Note on per-eval vs. session evaluator:** Both paths have a legitimate deployment shape.
`SessionModelEvaluator` (persistent subprocess, compile-once) fits Container Apps jobs and
long-running services where startup cost is amortized over many evaluations.
`RustModelEvaluator` (stateless subprocess per eval) fits Azure Functions where each invocation
is short-lived and process isolation is a feature. Both implementations are retained.

**Status:** Not scheduled. Marker section so that the .NET CLI, ITelemetrySource, artifact sink,
and observability work stay shaped for these scenarios as they land. Concrete Azure work begins
only when a specific client deployment target is chosen.

## Dependency Graph

```
E-10 (done) + E-16 (done) + E-19 (done) + E-20 (done) + E-17 (done)
  |
  +--→ E-18 Time Machine (in-progress)
  |      m-E18-13 SessionModelEvaluator   ← done
  |      m-E18-14 .NET Time Machine CLI   ← done
  |      (later) m-E18-XX Model Fit       ← blocked on E-15 + Telemetry Loop & Parity
  |      (later) Chunked evaluation       ← after discovery pipeline works end-to-end
  |
  +--→ UI parity fork                     ← NEXT
  |      Svelte UI: platform for new surfaces (telemetry, fit, discovery)
  |      Blazor UI: maintenance mode, frozen at current functionality
  |
  +--→ E-15 Telemetry Ingestion (critical path for client-telemetry vision)
  |      Gold Builder → Graph Builder → first dataset path
  |      +--→ Telemetry Loop & Parity
  |             +--→ E-18 Model Fit (completes discovery pipeline)
  |
  +--→ E-12 Dependency Constraints (engine feature — after discovery pipeline)
  +--→ E-13 Path Analysis (engine feature — after discovery pipeline)
  +--→ Scenario Overlays (parameter override as plan operation)
  +--→ Anomaly Detection (after path/parity basics)
```

## References
- `docs/architecture/reviews/engine-deep-review-2026-03.md` — Full engine deep review
- `docs/architecture/reviews/engine-review-findings.md` — Initial review findings
- `docs/architecture/reviews/review-sequenced-plan-2026-03.md` — Sequenced plan (historical rationale)
- `work/epics/epic-roadmap.md` — Architecture epics with links to specs
- `work/decisions.md` — Architectural decisions (dated D-2026-… identifiers)
- `docs/architecture/whitepaper.md` — Engine vision + future primitives
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals
