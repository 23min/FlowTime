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

## E-10 — Engine Correctness & Analytical Primitives

**Epic:** `work/epics/E-10-engine-correctness-and-analytics/spec.md`

The engine deep review found 3 P0 correctness bugs, engineering debt, documentation drift, and a missing analytical layer. Phases 0-2 complete. Phase 3 bridge complete (m-ec-p3a cycle time, m-ec-p3a1 analytical projection hardening — both merged to main). Remaining Phase 3 resumes in the order p3d -> p3c -> p3b.

### Phase 3: Analytical Primitives (resume sequence)
With E-16 complete, Phase 3 resumes in this execution order:
1. **p3d — Constraint Enforcement** — Wire ConstraintAllocator into evaluation pipeline so declared constraints are real rather than advisory.
2. **p3c — Variability** — Preserve Cv from PMFs for Kingman's approximation and theory-vs-runtime diagnostics.
3. **p3b — WIP Limits** — Optional wipLimit on ServiceWithBufferNode (Kanban what-if).

The milestone IDs are historical; the recommended implementation order above is the lower-risk sequence.

## E-16 — Formula-First Core Purification (completed)

**Epic:** `work/epics/E-16-formula-first-core-purification/spec.md` | **Status:** completed (`m-E16-06` completed on `milestone/m-E16-06-analytical-contract-and-consumer-purification`)

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

**Epic:** `work/epics/E-11-svelte-ui/spec.md` | **Status:** paused after M6 (M1-M4 + M6 done, M5/M7/M8 remain)

Build a parallel SvelteKit + shadcn-svelte UI surface for demos and future evaluation while keeping the Blazor UI supported and in sync. Independent of engine work — both UIs consume existing APIs with zero backend changes.

## E-19 — Surface Alignment & Compatibility Cleanup (planning)

**Epic:** `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md` | **Status:** m-E19-01 completed, m-E19-02 in-progress

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

## E-17 — Interactive What-If Mode (future)

**Epic:** `work/epics/E-17-interactive-what-if-mode/spec.md` | **Status:** future

Once E-16 makes the engine a pure compiled evaluator, live interactive recalculation becomes possible. Change a parameter via a UI slider, see all metrics/charts/heatmaps update instantly (sub-50ms). No recompilation for parameter value changes.

Requires: runtime parameter model, server-side session management, WebSocket/SignalR push channel, auto-generated UI parameter controls.

Recommended engineering order: build the shared runtime parameter foundation in the E-18 Time Machine first, then add E-17 session management, push delivery, and UI controls.

**Depends on:** E-16

## E-18 — Time Machine (future)

**Epic:** `work/epics/E-18-headless-pipeline-and-optimization/spec.md` | **Status:** future

Make FlowTime usable as a client-agnostic callable machine in pipelines, optimization loops, model discovery workflows, digital twin architectures, and AI-assisted iteration. SPICE-inspired analysis modes still sit on top, but the first responsibility is the new `FlowTime.TimeMachine` execution component.

Requires: `FlowTime.TimeMachine`, tiered validation, in-process SDK, reevaluation/parameter override foundation, CLI/sidecar surfaces, optimization framework, and telemetry source adapters.

Recommended execution layers: (1) m-E18-01a Path B extraction cut, m-E18-01b tiered validation + `ITelemetrySource`, m-E18-01c runtime parameter foundation + reevaluation, (2) CLI/sidecar and richer sweep/optimization/fitting work, (3) chunked/stateful extensions only after a dedicated streaming/stateful execution seam exists.

**Depends on:** E-16

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

## Dependency Graph

```
E-10 Phases 0-2 (done)
  |
  v
E-10 Phase 3 bridge: p3a + p3a1 (done, merged to main)
  |
  v
E-16 Formula-First Core Purification (NOW — 6 milestones)
  |
   +--→ E-10 Phase 3 resume: p3d → p3c → p3b
   |      |
   |      +--→ E-12 Dependency Constraints (after p3d)
   |      +--→ E-13 Path Analysis (after p3c + p3b)
   |      +--→ Scenario Overlays (after p3c + p3b)
   |      +--→ Anomaly Detection (after Phase 3 + path/parity basics)
    |
   +--→ E-19 Surface Alignment & Compatibility Cleanup (parallel post-purification cleanup lane)
    |      |
   |      +--→ shared UI contract discipline across Blazor + Svelte
   |
   +--→ E-15 Telemetry Ingestion
   |      +--→ Telemetry Loop & Parity
   |             +--→ E-18 fit / optimization against real telemetry
   |
   +--→ E-18 Time Machine foundation: runtime parameter model + evaluation SDK + CLI/sidecar
             |
             +--→ E-17 Interactive What-If
             +--→ E-18 advanced modes (sweep / optimize / fit)

E-11 Svelte UI (independent — paused after M6)
UI Paradigm epics (after E-11 M3-M4 foundation + relevant Phase 3 work)
```

## References
- `docs/architecture/reviews/engine-deep-review-2026-03.md` — Full engine deep review
- `docs/architecture/reviews/engine-review-findings.md` — Initial review findings
- `docs/architecture/reviews/review-sequenced-plan-2026-03.md` — Sequenced plan (historical rationale)
- `work/epics/epic-roadmap.md` — Architecture epics with links to specs
- `work/decisions.md` — Architectural decisions (dated D-2026-… identifiers)
- `docs/architecture/whitepaper.md` — Engine vision + future primitives
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals
