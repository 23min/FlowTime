# Architecture Gaps

This document tracks architecture gaps that are surfaced during implementation but not yet captured as epics or milestones.
It is intentionally short, factual, and forward-looking.

---

## Legacy / Compatibility Surface Cleanup

### Why this was a gap

E-16 owns analytical truth and consumer-fact purification, but broader non-analytical compatibility debt still remains across first-party UI, Sim, docs, schemas, and examples:

- first-party UI endpoint and metrics fallbacks
- legacy demo/template generation on active surfaces
- deprecated schema/example material living on current paths instead of archive/historical paths
- Blazor parallel-support and sync discipline still implicit rather than sequenced work

These do not belong inside E-16's analytical boundary, but they still need an owner.

### Status

Promoted to epic planning as `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md`.

### Immediate implications

- Do not add new compatibility helpers to first-party UI/Sim/docs/example surfaces without explicit exit criteria.
- Prefer archive-or-delete over "keep both for now" once replacement surfaces are confirmed.
- Do not strip supported functionality from Blazor as part of this cleanup; keep it aligned with current Engine/Sim contracts.
- Treat E-19 as the post-E-16 cleanup lane; it should start after E-16 but does not automatically block E-10 Phase 3 resume.

---

## Deferred deletion: Engine `POST /v1/run` and `POST /v1/graph`

### Why this is a gap

m-E19-01 A5/matrix scheduled `POST /v1/run` and `POST /v1/graph` for deletion in m-E19-02 on the premise that they are not used by first-party UIs. During m-E19-02 implementation (2026-04-08), discovery showed these routes are the primary run-creation mechanism for the Engine test suite:

- `ProvenanceHashTests.cs` — 13 call sites
- `ProvenanceHeaderTests.cs` — 8 call sites
- `ProvenanceEmbeddedTests.cs` — 8 call sites
- `ProvenancePrecedenceTests.cs` — 7 call sites
- `ProvenanceStorageTests.cs` — 6 call sites
- `ProvenanceQueryTests.cs` — 2 call sites
- `Legacy/ApiIntegrationTests.cs` — 3 `/v1/run` + 2 `/v1/graph` call sites
- `ParityTests.cs` — 2 call sites
- `CliApiParityTests.cs` — 1 call site

Total: 50 `/v1/run` call sites and 2 `/v1/graph` call sites across 9 test files covering Engine-side runtime provenance, CLI ↔ API parity, and legacy integration. These are coverage-load-bearing for runtime provenance behaviour adjacent to the E-16 purity work.

### Status

Deletion deferred out of m-E19-02. Recorded in `work/decisions.md` as D-2026-04-08-029. Supported-surface matrix updated: row for these routes now reads `transitional` with owning milestone `deferred (see work/gaps.md)` instead of `delete m-E19-02`.

### Resolution path

Before deletion can proceed, the 50+ test call sites must migrate to an alternative run-creation path. Two plausible options:

1. **Test-only in-process adapter** over `Graph.Evaluate` or `RunOrchestrationService` that bypasses HTTP entirely. Cleanest option because it does not pull Sim infrastructure into Engine tests.
2. **Sim orchestration endpoint with template fixtures** (`POST /api/v1/orchestration/runs`). Requires each test to publish a template YAML and spin up Sim infrastructure; increases cross-surface coupling in Engine tests.

Once the migration lands, `POST /v1/run` and `POST /v1/graph` can be deleted outright along with the `Legacy/ApiIntegrationTests.cs` Legacy suite. A dedicated follow-up milestone (candidate: `m-E19-02a-engine-runtime-route-retirement` or similar) should own the migration. Not scheduled yet.

### Immediate implications

- Do not add new callers to `/v1/run` or `/v1/graph` on any surface.
- Do not add new Provenance tests that use the HTTP `/v1/run` path; favour a direct in-process path instead.
- Track the eventual deletion as an explicit unit of work rather than letting it float as a tolerated coexistence state.

---

## Silent no-op + self-match false positives in m-E19-02 and m-E19-03 grep-guard scripts (resolved 2026-04-08)

### Why this was a gap

Discovered during the `epic/E-19 → main` merge sanity check (2026-04-08). Running the three E-19 grep-guard scripts with ripgrep explicitly on `$PATH`:

- `scripts/m-E19-04-grep-guards.sh` — 11/11 passing
- `scripts/m-E19-03-grep-guards.sh` — 9/11 passing (2 false-positive self-matches)
- `scripts/m-E19-02-grep-guards.sh` — 20/21 passing (1 false-positive self-match)

All three scripts were authored with the intent of being runnable locally and in the wrap pass. The problem has two layers:

1. **Silent no-op when `rg` is missing from `$PATH`.** Every guard in the m-E19-02 and m-E19-03 scripts wraps its `rg` invocation with `2>/dev/null || true`. On a machine without ripgrep on `$PATH` (for example the devcontainer this codebase is currently developed in — ripgrep exists under `/vscode/vscode-server/bin/.../node_modules/@vscode/ripgrep/bin/rg` but is not linked onto `$PATH`), every `rg` call errors, stderr is swallowed, `|| true` absorbs the non-zero exit, and the command substitution returns the empty string. The `check "<label>" "$matches"` helper treats empty `matches` as PASS. Result: every guard silently reports PASS without actually inspecting the tree. When the m-E19-02 and m-E19-03 milestones recorded "21/21 passing" and "11/11 passing" in their tracking docs, those counts were true against an environment with ripgrep available but not against the devcontainer where the milestones were actually wrapped. The issue was not caught at wrap time because the false positives also pass-through silently.

2. **Self-match false positives when ripgrep IS available.** With ripgrep on `$PATH`, the scripts surface their own hits because their default search scope (`src tests docs examples templates scripts`) includes `scripts/` — and each guard's script code contains the forbidden literal in comments explaining the guard. On top of that, m-E19-03's Guard 8 also self-matches `docs/architecture/supported-surfaces.md:83`, which contains the literal `/api/templates/` inside the quoted description "No current docs reference `/api/templates/` or other pre-v1 template routes" — a row that DESCRIBES the invariant, not a violation of it.

Specific false-positive findings:

| Script | Guard | False-positive source |
|--------|-------|-----------------------|
| `scripts/m-E19-02-grep-guards.sh` | AC2 draftId source type literal | Self-match in the script's own comment |
| `scripts/m-E19-03-grep-guards.sh` | Guard 7 `docs/ui/template-integration-spec` | Self-match in the script's own comment at lines 105/107/113 |
| `scripts/m-E19-03-grep-guards.sh` | Guard 8 `/api/templates/` pre-v1 route literal | Self-match in the script's own comment at lines 118/120/123/124 + description row in `docs/architecture/supported-surfaces.md:83` |

Neither layer represents a real regression on the `main` tree: every underlying m-E19-02 and m-E19-03 cleanup invariant (stored drafts gone, ZIP archive gone, `POST /v1/runs` bundle-import gone, catalogs gone, `binMinutes` authoring shape gone, schema-migration examples archived, `docs/ui/template-integration-spec.md` archived, catalog-stale phrasing cleaned) is in fact intact on current `main`, verified by hand.

### Status

**Resolved 2026-04-08** via `chore/grep-guard-cleanup` patch branch. All three scripts now run correctly:

- `scripts/m-E19-02-grep-guards.sh` — 20/20 passing
- `scripts/m-E19-03-grep-guards.sh` — 11/11 passing
- `scripts/m-E19-04-grep-guards.sh` — 11/11 passing (unchanged; already correct)

All three scripts now fail fast with exit code 2 and an install hint when ripgrep is missing from `$PATH`.

### Resolution

1. **Fail-fast `command -v rg` check** backported from `scripts/m-E19-04-grep-guards.sh` to the m-E19-02 and m-E19-03 scripts. Silent no-ops on machines without ripgrep are now loud failures with an install hint.
2. **`scripts/m-E19-03-grep-guards.sh` Guards 7 and 8** gained `--glob '!scripts/m-E19-03-grep-guards.sh'` exclusions so the script no longer self-matches its own explanatory comments. Guard 6 was left alone — it uses a regex that doesn't match any literal in the script body, so it can't self-match.
3. **`scripts/m-E19-02-grep-guards.sh` AC2 draftId source type literal guard dropped entirely.** The pattern `"draftId"|"draftid"` was too broad to express AC2's real invariant ("no `draftId` on `/drafts/run` specifically"). The preserved `/api/v1/drafts/map-profile` endpoint legitimately returns `["draftId"] = draftId` at `src/FlowTime.Sim.Service/Program.cs:932`, and a simple global grep cannot distinguish the two handlers. Allowlisting `Program.cs` would hide real regressions in `/drafts/run`, so dropping the guard is safer. AC2's invariant is still enforced at build/test time — the `/drafts/run` handler body no longer resolves `DraftSource.type == "draftId"` and the tests verify inline-only behavior. A `NOTE:` comment replaces the dropped guard line documenting the rationale. m-E19-02 is now 20 guards instead of 21.
4. **`docs/architecture/supported-surfaces.md:83`** reworded from `"No current docs reference \`/api/templates/\` or other pre-v1 template routes"` to `"No current docs reference the pre-v1 template routes"` — drops the forbidden literal from the description row while preserving meaning.

### Retained learning

- **When writing future grep-guard scripts, use the m-E19-04 template** (fail-fast `command -v rg` + `--glob` exclusions that keep the script body out of its own search path) as the reference.
- **Global grep patterns cannot enforce handler-scoped invariants.** If an AC's real invariant is "no X in handler Y", a global grep across the containing file will false-positive on any other handler that legitimately uses X. Either scope the search with a more precise tool (AST-based, or a scoped extraction like the awk block extraction used in m-E19-04 Guard 9) or accept that the invariant is not expressible as a simple grep and rely on build/test coverage instead.
- **If the guards are eventually wired into CI**, the CI image must have ripgrep installed (`apt-get install ripgrep` on Debian/Ubuntu) — otherwise CI fail-fasts (exit code 2) which is the correct behavior. Silent no-op is no longer possible.
- **Trust completed-milestone "N/N passing" counts** from m-E19-02 (now 20/20) and m-E19-03 (now 11/11) as of this fix, but not retroactively — the earlier tracking-doc counts pre-date the fail-fast check.

---

## Path Analysis / Path Filters

### Why this is a gap
FlowTime now emits edge time bins and derived sink/path latency, but there is no formal architecture for **path analysis**:

- Which paths are used (per class, per time window)?
- How much flow traverses each path?
- What are the dominant or toxic routes?
- Can we extract a subgraph that is “active” for a given class/time window?

Today, clients (UI/MCP) can inspect nodes and edges, but cannot ask for **path-level** answers without custom, client-side logic.

### Path filters vs path analysis

- **Path filters** are a *subgraph extraction* feature:
  - Given a start/end node, class, and time window, return only the nodes/edges that carry flow.
  - This is a query/selection problem and could be exposed as an API option.

- **Path analysis** is broader and deeper:
  - Path discovery, path frequency, ranking, and decomposition.
  - Dominant route identification, path changes over time, and route anomalies.
  - May require additional aggregation and/or derived outputs beyond edge time bins.

Path filters can be built from edge time bins, but they still need **well-defined query semantics**
(e.g., thresholds, class handling, time window behavior) that are not yet specified.

### Relationship to Edge-Time-Bin epic
Edge time bins provide the necessary *inputs* for path analysis, but do not define
how to aggregate or query paths. A formal epic is needed to standardize this.

### Proposed direction
Create a dedicated epic, tentatively **Path Analysis & Subgraph Queries**, to cover:

- Server-side path/subgraph query semantics (path filters).
- Path-level aggregations (counts, shares, dominant routes).
- Optional derived outputs with provenance (for MCP and UI).

This epic should be coordinated with:
- `work/epics/completed/edge-time-bin/` (inputs)
- `work/epics/anomaly-detection/` (pathologies)
- `work/epics/completed/ai/` (MCP consumption)

### Immediate implications
- MCP should remain **pass-through** for edge data in M-08.05.
- Any path filters or summary helpers should be **server-side** (authoritative),
  and should live in a follow-up epic or milestone.

---

## Summary Helpers (Edge/Path Analytics)

There is no API contract for **summary helpers** such as:

- Edge retry ratios (retryVolume / flowVolume).
- Conservation deltas at node boundaries.
- Path or route summaries.

These would be useful for MCP and UI, but require a contract that clearly labels
values as derived. This gap is best addressed alongside path analysis.

---

## Dependency Constraint Enforcement (Deferred M-10.03)

### What was planned
M-10.03 scoped MCP-side pattern enforcement: a dependency pattern selector routing user intent to Option A or Option B, rejecting unsupported patterns (feedback loops, retries), and promoting engine warnings to hard errors during MCP model generation.

### Why deferred
1. The engine review (2026-03) found that `ConstraintAllocator` has **zero callers** in the evaluation pipeline — constraints are declared in models but silently ignored at runtime. MCP-side enforcement alone doesn't fix this.
2. The sequenced plan recommends wiring `ConstraintAllocator` into `Graph.Evaluate()` (Phase 3.5) before MCP enforcement adds value.
3. Near-term priority is correctness bugs and analytical primitives (Phases 0–3 of the sequenced plan).

### When to revisit
After Phase 3.5 (runtime constraint enforcement) is complete. At that point, M-10.03 should be re-scoped to include both runtime enforcement and MCP guardrails.

### Reference
- Spec: `work/epics/E-12-dependency-constraints/M-10.03-dependency-mcp-pattern-enforcement.md`
- Review: `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (Phase 3.5, Phase 4.1)

---

## dag-map Layout Quality (Svelte UI)

### Wiggly trunk in dag-map bezier layout
The main trunk path in dag-map's `layoutMetro` wobbles vertically when branches push the Y-positions around. Visible on the FlowTime "Transportation Network" model (12 nodes). The trunk should be a straight horizontal line with branches diverging above/below.

### No class differentiation from FlowTime API
The FlowTime `/v1/runs/{id}/graph` endpoint returns node `kind` (service, queue, dlq) but not flow classes. dag-map's layout engine uses `cls` to assign route colors and lane spread. Without meaningful classes, all nodes land on one route.

### Possible fixes
- dag-map: improve trunk stability in layoutMetro (prioritize trunk straightness)
- FlowTime API: expose class-to-node mapping so dag-map can assign routes
- dag-map: add heatmap mode so coloring comes from metrics, not static classes

---

## dag-map Features Needed for Svelte UI M5+

- ~~**Heatmap mode**: per-node/edge metric coloring~~ **Done** (dag-map `metrics`/`edgeMetrics`/`colorScales`)
- **Click/tap events**: callback with node ID (M5 blocker for inspector). `data-id` attributes exist; need event delegation in Svelte wrapper or library-level callback.
- **Hover tooltips**: on stations (M5 blocker for inspector)
- **Selected node highlighting**: visual state (M5 blocker for inspector)
- **Node shape differentiation**: custom shapes per node kind (service=rect, queue=diamond, dlq=triangle). Possible via `renderNode` callback.

See dag-map ROADMAP.md “Planned” sections.

## Svelte UI: SVG Performance at Scale

The “all dependencies” non-operational view has ~60-80 nodes and 200+ edges. SVG should handle this (est. ~600 DOM elements), but hasn't been tested. If it struggles:
- Try DOM-based metric updates (setAttribute) instead of full SVG re-render
- Consider canvas hybrid (dag-map for layout, canvas for rendering) as last resort
- Semantic zoom (dot → station → card at zoom levels) could reduce element count

---

## Client-Side Route Derivation for layoutFlow

FlowTime classes are metric dimensions, not graph routes. To use dag-map's `layoutFlow`, we need routes: `{ id, cls, nodes: [nodeIds] }`. The API provides `ByClass` on edges/nodes in state data, but no path-level query.

**Workaround:** Trace edges with non-zero `ByClass[classId].flowVolume` per class to derive approximate routes. Not authoritative — a proper Path Analysis API is needed for production.

**Status:** Not attempted yet. Needs experimentation.

---

## Router Convergence Guard (Deferred from Phase 1)

### What was planned
Add a max-iteration limit and convergence detection to `RouterAwareGraphEvaluator`, which currently performs a single re-evaluation pass.

### Why deferred
The single-pass design is correct for static router weights. A convergence guard is only needed if dynamic/expression-based router weights are introduced (e.g., "route to least-utilized path"). No current or planned models require dynamic routing. FlowTime operates on aggregated time series per bin — routing fractions are either observed (telemetry) or assumed (static weights).

### When to revisit
When dynamic routing is designed as a feature. At that point, add iteration with convergence detection and a max-iteration safety limit.

### Reference
- `src/FlowTime.Core/Routing/RouterAwareGraphEvaluator.cs`
- Phase 1 spec: `work/epics/E-10-engine-correctness-and-analytics/m-ec-p1-engineering-foundation.md`

---

## Parallelism `object?` Typing (Deferred from Phase 1)

### What was planned
Replace `NodeSemantics.Parallelism` (`object?`) with a proper discriminated union type. The loose typing exists because YAML deserialization can produce a string (file URI or node reference), a numeric scalar, or a double array.

### Why deferred
The change touches 21 files across Core, Contracts, Sim, API, and UI — a cross-cutting refactor with high risk for a foundation milestone. CUE (https://cuelang.org/) was noted as a potential future approach for model schema validation with native union type support.

### When to revisit
Addressed by E-16 m-E16-01 (Compiled Semantic References). See D-2026-04-03-007. Parallelism becomes a typed reference resolved at compile time. Close this gap after m-E16-01 completes.

### Reference
- `src/FlowTime.Core/Models/NodeSemantics.cs` (line 21)
- `src/FlowTime.Core/DataSources/SemanticLoader.cs` (ResolveParallelism method)
- Phase 1 spec: `work/epics/E-10-engine-correctness-and-analytics/m-ec-p1-engineering-foundation.md`

---

## Continuous Prediction / Crystal Ball Usage Pattern

### Why this is a gap

The crystal ball capability — feeding observed arrivals into a calibrated model to predict future system state faster than real time — emerges from the intersection of E-15 (topology inference + telemetry), E-18 (headless evaluation), and streaming (real-time ingestion). But no single epic owns this usage pattern, and design decisions in each epic could accidentally make continuous prediction harder.

The roadmap thesis (Pure → Interactive → Programmable) covers the building blocks but misses this third mode of operation:
- **E-17** (Interactive) assumes a human adjusting sliders.
- **E-18** (Programmable) assumes a pipeline running parameter sweeps.
- **Crystal ball** needs a continuously refreshed model without a human in the loop — a session without a user.

### Immediate implications

- E-18 spec work should consider "continuous evaluation with external data feed" as a first-class use case alongside batch parameter sweeps.
- Streaming epic promotion from working note to real epic should reference the crystal ball framing as a motivating use case.
- E-17's session management design should not assume sessions are always human-initiated.

### Reference

- `docs/notes/crystal-ball-predictive-projection.md`
- `docs/notes/predictive-systems-and-uncertainty.md`
- `work/epics/streaming/streaming-architecture-working-note.md`

---

## Streaming Epic Not Formalized

### Why this is a gap

The streaming architecture working note (`work/epics/streaming/streaming-architecture-working-note.md`) is a draft with no epic number, no milestones, and no acceptance criteria. For the crystal ball, fresh arrivals data is a hard requirement — without it, predictions use stale data and the prediction horizon is degraded.

A workaround exists: rapid batch ingestion via E-15's batch pipeline (poll every 5 minutes). This gives a useful but degraded crystal ball. True real-time prediction needs the streaming epic to be real.

### When to revisit

After E-15's first dataset path proves batch ingestion works. At that point, the streaming note should be promoted to an epic with milestones.

### Reference

- `work/epics/streaming/streaming-architecture-working-note.md`
- `docs/notes/crystal-ball-predictive-projection.md` (requirement 2: fresh arrivals data)

---

## E-18 Model Calibration Needs Crystal Ball Design Input

### Why this is a gap

E-18 mentions "model fitting against real telemetry" in scope but defers it to the analysis layer. The crystal ball's prediction accuracy depends fundamentally on calibration — how well the model's capacity, failure rates, and retry kernels match reality.

Unresolved questions that should be design inputs when E-18 fitting milestones are specced:
- Which model parameters are fittable? (Capacity, failure rate, retry kernel coefficients — but not topology or grid resolution.)
- What is the objective function? (Minimize series-level MSE between model output and observed telemetry? Per-node? Per-class?)
- How often does recalibration happen? (Per-run? Sliding window? Triggered by drift detection from anomaly epic?)
- Should the calibrated model carry provenance about its fit quality? (Residuals, confidence, calibration timestamp.)

### Immediate implications

- E-18 spec work should reference the crystal ball note when designing the fitting/optimization layer.
- Anomaly detection should consider "model-vs-reality divergence" as a calibration trigger, not just an alert.

### Reference

- `work/epics/E-18-headless-pipeline-and-optimization/spec.md`
- `docs/notes/crystal-ball-predictive-projection.md`
- `docs/notes/predictive-systems-and-uncertainty.md`

---

## Rust Engine Parity — Evaluation Core Gaps

### Why this is a gap

The Rust matrix engine (E-20) handles compilation, evaluation, and basic artifact writing for simple models, but cannot replace the C# evaluation pipeline for models that use classes, edges, or output filtering. These are evaluation-layer gaps per D-2026-04-10-031 — the engine core must return complete results before the artifact sink or consumers can use them.

### Gaps (engine core — must be in Rust)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| Per-class column decomposition | Critical | Deferred (m-E20-06, matrix-engine.md) | Rust computes class routing internally (`__class_` columns) but does not expose per-class series in EvalResult. C# uses ClassContributionBuilder. |
| Edge series materialization | High | Undocumented | C# EdgeFlowMaterializer produces per-edge throughput/attempt series. Rust uses edges for ordering only. |
| `outputs:` filtering/renaming | Medium | Undocumented | `OutputDefinition` parsed in model.rs but never used in compiler or writer. Models use this to select/rename output series. |

### Gaps (artifact sink — separate from engine core)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| `model/model.yaml` | Low | Undocumented | Copy of input model in run directory. |
| `model/metadata.json` | Medium | Undocumented | Template metadata, telemetry sources, mode. Needs metadata extraction from YAML. |
| `model/provenance.json` | Low | Partially documented (D-2026-04-10-030) | Sim-specific provenance deferred until bridge exercised by real Sim runs. |
| `spec.yaml` at run root | Low | Undocumented | Normalized topology with file:// URIs. Needed by StateQueryService. |
| Per-class CSV naming (`{node}@{component}@{class}.csv`) | High | Deferred (m-E20-06) | Depends on per-class decomposition in engine core. |
| Edge CSV naming (`edge_{id}_{metric}@{component}@{class}.csv`) | Medium | Undocumented | Depends on edge series in engine core. |
| Full `series/index.json` schema (kind, unit, class, componentId, hash) | Medium | Undocumented | Current Rust schema is minimal. |
| Full `run.json` schema (classes, classCoverage, source, inputHash) | Medium | Undocumented | Current Rust schema is minimal. |
| Full `manifest.json` schema (rng, provenance section, classes) | Low | Partially done (m-E20-07 added hashes) | Extend existing. |
| `aggregates/` directory | Low | Undocumented | Placeholder in C# today. |
| Series ID format (`{nodeId}@{COMPONENT}@{CLASS}`) | Medium | Undocumented | Current Rust uses bare `{nodeId}`. |

### Gaps (full parity harness)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| Parity test across all 21 Rust fixtures | High | In-scope (E-20 spec) but incomplete | m-E20-07 tested 3 models. Need coverage of all topology, routing, constraint, PMF fixtures. |
| Casing normalization | Low | Discovered in m-E20-07 | Rust lowercases topology node IDs; C# preserves casing. Parity helper uses case-insensitive matching. |

### Resolution path

Per D-2026-04-10-031, work splits into:
1. **Engine core gaps** (per-class, edge series, outputs filtering) → new milestones, prerequisite for E-17/E-18
2. **Artifact sink gaps** (directory layout, metadata, naming) → separate milestones, can follow engine core work
3. **Parity harness** → validates engine core correctness across all fixtures, should land before core gaps to establish baseline

### Blocking relationships

- E-17 (Interactive What-If) requires: per-class decomposition, outputs filtering, full index.json/run.json schema
- E-18 (Time Machine/Pipelines) requires: all of the above + edge series + artifact sink parity
- Svelte UI state display requires: StateQueryService compatibility (spec.yaml with file:// URIs, per-class series)

### Reference

- D-2026-04-10-031 (three-layer architecture)
- D-2026-04-10-030 (provenance strategy)
- `work/epics/E-20-matrix-engine/spec.md` (original scope)
- `docs/architecture/matrix-engine.md` (future work section)

---

## `IModelEvaluator` Series-Key Shape Divergence

### Why this is a gap

Discovered during m-E18-13 implementation (2026-04-15). The two production
`IModelEvaluator` implementations return different key shapes for the same underlying
series:

| Implementation | Example keys |
|----------------|--------------|
| `RustModelEvaluator` (via `RustEngineRunner`, reads run artifacts) | `arrivals@ARRIVALS@DEFAULT`, `served@SERVED@DEFAULT` |
| `SessionModelEvaluator` (via Rust session protocol) | `arrivals`, `served` |

The numeric values are identical (both invoke the same engine), but the dictionary keys
are not interchangeable. `RustEngineRunner` reads artifacts laid out per E-20 conventions
(`{nodeId}@{COMPONENT}@{CLASS}`). The session protocol returns bare column-map IDs from
`session.rs::extract_all_series`.

### Immediate implications

- `SweepRunner.FilterSeries` does case-insensitive exact-match lookup on keys. Sweeps
  that specify `captureSeriesIds: ["served"]` work correctly against `SessionModelEvaluator`
  but return empty dictionaries against `RustModelEvaluator` — keys won't match because
  the evaluator's keys are `served@SERVED@DEFAULT`.
- Sweeps with `captureSeriesIds: null` (API default) work with both — all series pass through.
- No existing test catches this because sweep unit tests use `FakeEvaluator` with bare
  keys, and no integration test drove `RustModelEvaluator` with `captureSeriesIds`.

### Resolution options

1. Normalize `RustModelEvaluator` to strip `@COMPONENT@CLASS` suffix when the default
   component/class is the only one present — matches session protocol. Preserves per-class
   series when classes are used.
2. Teach `SweepRunner.FilterSeries` to do prefix-match on `captureSeriesIds` (match
   `"served"` to any key starting with `served@`). More tolerant but may over-match when
   classes are involved.
3. Document the divergence as acceptable and require callers to know which evaluator is
   wired. Worst option — leaves a footgun.

### Status

Not scheduled. Tracked pending a decision on normalization. `SessionModelEvaluator` is
the default evaluator in production, so the divergence does not currently break sweeps.
If `RustEngine:UseSession=false` is flipped, sweeps with `captureSeriesIds` will break.

### Reference

- `src/FlowTime.TimeMachine/Sweep/RustModelEvaluator.cs`
- `src/FlowTime.TimeMachine/Sweep/SessionModelEvaluator.cs`
- `src/FlowTime.TimeMachine/Sweep/SweepRunner.cs` — `FilterSeries`
- `engine/cli/src/session.rs` — `extract_all_series`
- `tests/FlowTime.Integration.Tests/SessionModelEvaluatorIntegrationTests.cs` —
  `SessionVsPerEval_NumericValuesAgree`

---

## E-18 Optimization Constraints (no owner milestone)

### Why this is a gap

`OptimizeSpec` has no constraint field. The E-18 spec describes constraints as:

```
--constraint "max(node.queue.utilization) < 0.8"
```

This was explicitly deferred out of m-E18-12 because the Nelder-Mead implementation
did not need it to meet the milestone acceptance criteria, and it adds non-trivial
complexity to the simplex inner loop.

### Design notes

Implementation approach: **penalty method** inside the Nelder-Mead loop. When a
candidate point violates a constraint, add a large penalty to its objective value
so the simplex naturally avoids the infeasible region. This does not require
fundamental changes to the optimizer — add `ConstraintSpec` to `OptimizeSpec`,
evaluate constraints after each `IModelEvaluator.EvaluateAsync` call, sum
penalties into the returned metric value.

Alternative: **projection** — clamp candidate points back into the feasible region
before evaluation. Simpler to implement but loses some search flexibility near
constraint boundaries.

### Resolution path

A future E-18 milestone (no ID assigned yet) should:
1. Add `ConstraintSpec` — expression (metric series ID), comparator (`<`, `>`), threshold
2. Evaluate constraints after each evaluator call; add penalty if violated
3. Surface constraint satisfaction in `OptimizeResult` (were all constraints satisfied at optimum?)

### Status

Not scheduled. No owner milestone. Tracked here pending planning.

### Reference

- `src/FlowTime.TimeMachine/Sweep/OptimizeSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-12-optimization.md` (deferred note)
- `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md` (gap #4)

---

## Sim-generated model shape vs. Rust engine compiler expectations

### Why this is a gap

Discovered 2026-04-17 while wiring the Svelte `/analysis` surface
(m-E21-03 sweep + sensitivity) against real runs. The Rust engine's
compiler (`engine/core/src/compiler.rs`) has a hard split between
**top-level `nodes:`** (only `const`/`expr`/`pmf`/`router` accepted)
and **`topology.nodes:`** (where `queue`/`service`/`servicewithbuffer`/
`dlq` belong).

Sim-generated models (Supply Chain Multi-Tier Classes, Warehouse Picker
Wave Dispatch, and similar templates) emit service/queue/
servicewithbuffer nodes at the top level of `nodes:` instead. The Rust
engine rejects them at compile time:

- `Unsupported node kind 'servicewithbuffer' on node 'intake_queue'`
- `Router node 'ReturnsRouter' requires router field` (shape mismatch
  when routing is expressed via edge weights rather than a node field)

As a result, **no existing Sim-generated run compiles in the Rust
engine session**, which means the full `/v1/sweep`, `/v1/sensitivity`,
`/v1/goal-seek`, and `/v1/optimize` surfaces cannot be exercised
against real runs until the shape mismatch is closed.

### Current mitigation

- `/analysis` route has a **Run / Sample** source toggle. Sample mode
  ships bundled, Rust-compatible minimal models (`simple-queue`,
  `two-stage-pipeline` at `ui/src/lib/utils/sample-models.ts`) so the
  analysis flow can be demoed without a real run.
- Sweep/sensitivity API endpoints now return the engine's compile
  error as a structured 400 (rather than a 500 developer page), so
  the UI surfaces an actionable message.

### Resolution path

Two non-exclusive options:

1. **Shape-normalizing transform in Sim or a pre-compile step**:
   translate `kind: servicewithbuffer` / `kind: service` /
   `kind: queue` at the top-level into `topology.nodes` entries before
   handing the YAML to the Rust engine. Routing weights on edges could
   similarly be translated into router-node form.
2. **Widen the Rust engine compiler** to accept the Sim-generated
   shape directly (promote top-level service/queue nodes into the
   topology layer inside `compiler.rs`).

Option 1 keeps the Rust engine's compile contract strict; option 2
reduces friction but blurs the boundary that E-16/E-20 established.
Decision belongs to the engine epic owner — not in E-21 scope.

### Immediate implications

- Do not assume Sim-generated runs work with `/v1/sweep`,
  `/v1/sensitivity`, or downstream analysis APIs. Sample-model mode
  is the working path for demos until this gap is closed.
- Telemetry Loop & Parity (prerequisite for fitting) will hit the
  same issue — parity testing requires the Rust engine to be able
  to evaluate the models Sim produces.

### Reference

- `engine/core/src/compiler.rs` lines 637-670 (top-level node kind
  dispatch), lines 1163-1190 (topology-node kind handling)
- `work/epics/E-21-svelte-workbench-and-analysis/m-E21-03-sweep-sensitivity.md`
- `ui/src/lib/utils/sample-models.ts` (bundled Rust-compatible fixtures)

---

## Ultrareview findings on `epic/E-21-svelte-workbench-and-analysis` (2026-04-20)

### Why this is a gap

The `/ultrareview` sweep against `main` for the E-21 epic branch surfaced four low-severity issues — one pre-existing path-traversal pattern in the Engine API and three nit-level UX/correctness defects on the new `/analysis` and topology surfaces. None block the milestone wrap; all are worth tracking so they do not decay into tolerated coexistence.

### Findings

1. **Path-traversal pattern on `GET /v1/runs/{runId}/model` (pre-existing class).**
   `src/FlowTime.API/Program.cs:1090-1117` passes `runId` straight to `Path.Combine(artifactsDirectory, runId)` with no validation. A `runId` of `..` resolves `runPath` outside the artifacts root; if a `model/model.yaml` happens to exist there, it is served as `text/yaml`. Bounded by ASP.NET's single-segment route constraint (no embedded `/`) and the fixed `model/model.yaml` suffix, but the defensive check is still missing. Same shape on sibling endpoints: lines 1071, 1126, 1188, 1224. Fix: extract a shared `GetRunDirectorySafe(artifactsDirectory, runId)` helper (regex allow-list on `runId`, or `Path.GetFullPath` + `StartsWith(artifactsDirectory)` canonicalisation) and apply to all call sites in one pass.

2. **`selectedSampleId` not persisted on `/analysis`.**
   `ui/src/routes/analysis/+page.svelte:229-255` persists `ft.analysis.tab`, `ft.analysis.source`, and `ft.analysis.infoHidden` to `localStorage` but not the chosen sample id. On reload with `sourceMode='sample'`, `selectedSampleId` resets to `SAMPLE_MODELS[0].id` and `onMount` silently loads that instead of the user's last choice. Fix: add `localStorage.setItem('ft.analysis.sample', id)` inside `loadSampleModel` and a matching read in `onMount`, with a `SAMPLE_MODELS.some(...)` guard against removed samples.

3. **Sweep silently truncated to 200 points with only generic `> 50` warning.**
   `ui/src/lib/utils/analysis-helpers.ts:58-79` caps `generateRange` output at `maxPoints=200` but the `/analysis` Sweep tab only emits `⚠ large sweep (> 50 points)`. A user entering `from=0/to=1000/step=1` (expecting 1001 points) sees `200 points` with the generic warning; 801 values are dropped with no distinct truncation signal. Fix: detect `out.length === maxPoints && (to - from)/step + 1 > maxPoints` and surface a dedicated `truncated — first 200 of N` indicator, or extend `generateRange` to return `{ values, truncated, requestedCount }`.

4. **Unescaped node ids in topology edge-highlight CSS selector.**
   `ui/src/routes/time-travel/topology/+page.svelte:108-120` interpolates `edge.from`/`edge.to` directly into a CSS attribute selector. A node id containing `"`, `\`, or `]` makes `querySelectorAll` throw `SyntaxError`; because the effect clears `.edge-selected` before the loop, one bad id both unselects existing edges and blocks all future highlight updates for the session. Fix: wrap interpolations with `CSS.escape()`, or wrap `querySelectorAll` in `try/catch` with a `console.warn` fallback.

### Status

Not scheduled. Candidate owners:

- Finding 1 → E-19 follow-up or a standalone patch alongside the deferred `POST /v1/run`/`POST /v1/graph` retirement; should close the whole class (all 5 call sites), not patch a single endpoint.
- Findings 2–4 → m-E21-07 polish milestone, or a patch on the epic branch before epic wrap if priorities permit.

### Immediate implications

- Do not add new `Path.Combine(artifactsDirectory, {userInput})` call sites without the shared safe helper.
- Do not add new `localStorage`-backed UI state on `/analysis` without mirroring the sample-id persistence gap — persist all three axes together (tab, source, sample).
- Do not interpolate graph ids into CSS selectors elsewhere; prefer `CSS.escape` as the default for any new DAG-map handlers.
- Do not raise the `generateRange` cap above 200 without also fixing the truncation signal — a higher cap without a clearer indicator makes the silent-drop cliff worse, not better.

### Reference

- Remote review task id `rtdmj8ob8` (2026-04-20)
- `src/FlowTime.API/Program.cs:1071,1090,1126,1188,1224`
- `ui/src/routes/analysis/+page.svelte:172-199,229-266,601-605`
- `ui/src/lib/utils/analysis-helpers.ts:58-79`
- `ui/src/routes/time-travel/topology/+page.svelte:108-120`

---

## `.codex/` missing from framework adapter-ignore list (2026-04-20)

### Why this is a gap

The `.ai/sync.sh` refactor merged in `23min/ai-workflow#5` formalised the track-vs-ignore convention (CLAUDE.md tracked; all other adapters gitignored) and made `sync.sh` reconcile `.gitignore` on every run. The `ADAPTER_ENTRIES` array in `sync.sh` covers `.github/copilot-instructions.md`, `.github/skills/`, `.claude/agents/`, `.claude/skills/`, and `.claude/rules/ai-framework.md` — but **not `.codex/instructions.md`**, which is equally a generated adapter.

`flowtime-vnext` handled this locally by appending `.codex/` to its own `.gitignore` and running `git rm --cached .codex/instructions.md`. Works for this repo, but any other consumer of the framework would need to replicate the same manual step.

### Resolution path

Follow-up PR to `23min/ai-workflow`: add `.codex/instructions.md` (or `.codex/`) to `ADAPTER_ENTRIES` in `sync.sh`, update the corresponding test assertions in `tests/test-sync.sh`, and add a MIGRATIONS entry noting the additional ignore line so existing consumers untrack their tracked `.codex/instructions.md`.

### Immediate implications

- Do not re-add `.codex/instructions.md` to git tracking in this repo.
- When the framework adds the entry, local override becomes redundant and can be simplified (drop `.codex/` from this repo's `.gitignore`).

### Reference

- `23min/ai-workflow#5` — sync.sh consolidation PR (merged 2026-04-20)
- This repo's `.gitignore` — `.codex/` entry added locally

---

## Heatmap view — deferred enhancements (m-E21-06 Q&A, 2026-04-23)

### Why this is a gap

The m-E21-06 Heatmap View design Q&A (14 questions) surfaced several enhancements that are real analytical value but are not required for the first shipping heatmap. Default behavior for the milestone favors paradigm coherence (shared normalization with topology, topological default sort, etc.); these items are alternative modes and secondary analytical tools.

### Deferred items

- **Fixed per-metric color ranges** (Q3-D). Utilization anchored to `[0, 1]`, bounded metrics pinned to their natural domain, etc. Stable across runs (e.g. utilization of 0.5 always looks the same). Requires metric-registry enrichment (per-metric `domain` metadata) and doesn't work for unbounded metrics without convention. Default normalization is shared full-window with 99th-percentile clipping.
- **Per-row (per-node) color normalization toggle** (Q3-C). Each row normalizes over its own min/max, surfacing "temporal pattern within this node" at the cost of cross-node comparability. Useful secondary mode for "what's the shape of this node's pattern?" — defer until asked.
- **Current-bin value sort mode** (Q7 extra). Sorts rows by value at the scrubber's current bin; re-sorts when the scrubber moves. Cute but volatile; unclear real analytical use.
- **Trend / slope sort mode** (Q7 extra). Rank rows by slope of their time series to answer "which nodes are getting worse over time?" Genuinely analytical but adds statistical complexity; defer.
- **View-registry graduation** (Q13). m-E21-06 ships a typed `<ViewSwitcher>` with views listed inline on the topology page and shared state in a store. When a third layered view lands (decomposition / comparison / flow-balance — currently out-of-scope of E-21) with real asymmetry from topology/heatmap, graduate to a manifest-based registry + `ViewContext` pattern. Premature to build now.

### Immediate implications

- These are UX-enrichment items, not correctness gaps. Shipping m-E21-06 without them is honest: the default behavior is the right default for a first heatmap.
- If future layered-view milestones surface asymmetry that the inline `<ViewSwitcher>` handles awkwardly, graduate to a registry pattern then — not speculatively.

### Reference

- E-21 m-E21-06 Q&A conversation, 2026-04-23 (in-session, not archived).
- Source spec: `work/epics/unplanned/ui-analytical-views/spec.md` V1 Heatmap View.
- E-21 epic spec: `work/epics/E-21-svelte-workbench-and-analysis/spec.md` m-E21-06 row.

---

## Topology DAG has no keyboard nav or ARIA structure (m-E21-06 AC12 homework)

### Why this is a gap

m-E21-06 establishes the accessibility bar for the Svelte workbench: heatmap cells are keyboard-reachable via Tab + arrow keys, carry `role="grid"`/`role="row"`/`role="gridcell"` with `aria-label` containing node id + bin + metric + value, render a visible focus ring, and fire tooltip-on-focus. During the AC12 homework audit the topology DAG area was found to lag that bar:

- DAG SVG is rendered via `{@html renderSVG(...)}` from the dag-map library; nodes have no `tabindex` and cannot be reached by keyboard.
- No ARIA roles on the SVG container, nodes, or edges — a screen-reader user gets no structure.
- Node-click is the only input modality.
- Edge interaction (click-to-pin-edge) has no keyboard equivalent.

Per milestone confirmation #3, this was not retrofitted inside m-E21-06. The heatmap ships at the higher bar; topology remains at the earlier bar.

### Status

Open. Blazor UI's original topology had keyboard + ARIA; Svelte topology regressed here and the regression predates m-E21-06.

### Immediate implications

- Do not ship accessibility audits against the Svelte workbench as "complete" until topology reaches heatmap's bar.
- When a future milestone (likely m-E21-08 polish) adds pattern encoding / high-contrast tuning, include topology keyboard + ARIA retrofit in the same pass.
- The dag-map library itself may need to grow tabindex / role options on its rendered nodes; coordinate with the library owners before forking rendering in the topology Svelte component.

---

## Data-viz palette not validated for color-blindness (m-E21-06 AC12 homework)

### Why this is a gap

The `--ft-viz-*` palette introduced in m-E21-02 (teal, pink, coral, blue, green, amber, purple) was chosen for general aesthetic contrast but was not validated against color-blindness simulators. Under ADR-m-E21-06-02 both topology and heatmap now share the same teal → amber → red gradient from `dag-map`'s `colorScales.palette`, so the issue amplifies — users with deuteranopia or protanopia may see low-utilization teal and high-utilization red as the same muted hue.

### Status

Open. Deferred from m-E21-06 per confirmation #3 — pattern-encoding (redundant hatch overlay) and high-contrast tuning land in m-E21-08 polish milestone.

### Immediate implications

- Until polish lands, users with color-vision differences will rely on the `data-value-bucket` attribute semantics (`low` / `mid` / `high`) and on hover tooltips for correctness.
- When m-E21-08 runs, add a deuteranopia / protanopia / tritanopia simulator pass to the workbench smoke test; pattern-encode heatmap cells when enabled via a user preference toggle.

---

## Bidirectional card ↔ view selection (reverse cross-link)

### Why this is a gap

m-E21-06 Heatmap View ships a **one-way** cross-link: clicking a heatmap cell or a topology node pins the node and sets `viewState.selectedCell`; the matching workbench card's title renders in turquoise (`--ft-highlight`). The reverse path — click a workbench card → the corresponding cell in the heatmap and node in topology light up as the selected item — is not implemented.

Today, clicking a card body does nothing (only the ✕ close button is interactive). That's fine for shipping m-E21-06, but it's the natural other half of the "same model, multiple views" principle the milestone is built on. For long pin-stacks it's common to wonder "where is this node in the graph?" without needing to hover-scan the heatmap or DAG.

### Proposed shape

- **Card body click** → `viewState.setSelectedCell(card.nodeId, viewState.currentBin)`.
  - Heatmap: the existing `selectedCell`-driven overlay rect automatically appears at (nodeId, currentBin). Zero new code in heatmap.
  - Topology: dag-map nodes are rendered as SVG via `{@html renderSVG(...)}`. Add a CSS rule `.node-selected { stroke: var(--ft-highlight); stroke-width: 2; }` and a `$effect` that toggles the class by id — same pattern as the existing `.edge-selected` toggle in `ui/src/routes/time-travel/topology/+page.svelte:127–128`.
- Keep the ✕ close button as the sole unpin surface on the card. Card body click = select only (no toggle / no unpin).
- Keyboard reachability on the card body for a11y (space / enter → same effect).

### Status

Open, captured 2026-04-24 as a "natural next step" after m-E21-06 card cross-link work.

### Immediate implications

- Bundle into **m-E21-08 Polish** — that milestone already has topology keyboard-nav + ARIA retrofit and color-blind validation queued. Adding the reverse-cross-link while topology SVG is being touched is cheap.
- Before shipping: decide whether card body click conflicts with any future card interactivity (expand/collapse, drill-in). If so, scope the click to the card title bar rather than the whole body — leaves the body area free for subsequent interactive content.
- No backend changes needed.

### Reference

- `ui/src/routes/time-travel/topology/+page.svelte:127-128` — existing `.edge-selected` class toggle pattern.
- `ui/src/lib/stores/view-state.svelte.ts` — `setSelectedCell` / `clearSelectedCell` already in place.
- `ui/src/lib/components/workbench-card.svelte` — needs a click handler on the card body or title.

---

## Heatmap sliding-window scrubber (Blazor-parity zoom-and-pan)

### Why this is a gap

m-E21-06 Heatmap View ships a **fit-to-width** toggle (`ft.heatmap.fitWidth`, default off) that compresses `CELL_W` to `max(3, min(18, floor(containerWidth / binCount)))` so wide runs (e.g. 288 bins for the multi-tier supply chain model) fit the viewport without horizontal scroll. That solves the overview-first case but sacrifices per-cell fidelity — at 3–5 px per bin, individual tile values are hard to read, tooltip hover is fiddly, and the column-highlight marker shrinks accordingly.

The Blazor UI's scrubber has a **draggable window** affordance — a resizable/pannable range on the scrubber track that selects a subset of bins for inspection. The Svelte timeline scrubber currently only exposes a single-thumb `currentBin`. A Blazor-parity dual-handle "window scrubber" would let users keep the default 18 px cell size at full fidelity while panning across a long run:

- Window size = e.g. 64 bins; drag the window across 288 bins = five screens of detail.
- Heatmap renders `binCount=64` with `CELL_W=18`, no compression.
- The scrubber track doubles as a minimap-style summary.
- `state_window` already accepts `startBin` / `endBin`, so the data plane is ready.

### Status

Open, deferred by user decision 2026-04-24. Fit-to-width is the 80 % solution for overview; the sliding window is the 80 % solution for detail-at-scale. Shipping the window properly needs a dedicated milestone because:

- `TimelineScrubber` needs dual handles (window-start, window-end) plus a window body for drag-to-pan.
- `currentBin` vs `windowBin` semantics must be nailed down (what does "pin this cell" mean when the user is viewing bins 128–191 but the full-run thumb is at 30?).
- Heatmap and topology both need to consume the window range from the shared view-state store (new `windowRange` field).
- Playwright coverage for drag-pan, resize, and keyboard equivalents (PgUp / PgDn to pan).

### Immediate implications

- Until the sliding-window milestone lands, fit-to-width is the only knob for wide runs on the heatmap.
- Plan for a new E-21 milestone after m-E21-07 Validation and m-E21-08 Polish — or fold into polish if scope allows.
- When the milestone lands, the Blazor-parity gesture needs to be muscle-memory-compatible for existing users (drag the window body; resize via handles).

---

## Open Questions

- Should path filters be part of the time-travel API or a separate analysis endpoint?
- What thresholds/semantics define a “path” in time-binned data?
- Should derived path outputs be stored in run artifacts or computed on demand?
- How should node kind (service/queue/dlq) map to visual differentiation in dag-map?
- Should the Svelte UI support both operational and full graph views? What layout handles 80+ nodes?
