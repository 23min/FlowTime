# Decisions

Shared decision log for active architectural and technical decisions.

<!-- Format:
## D-YYYY-MM-DD-NNN: <short title>
**Status:** active | superseded | withdrawn
**Context:** <why this decision was needed>
**Decision:** <what was decided>
**Consequences:** <what follows from this>
-->

## D-2026-03-30-001: dag-map for Svelte UI topology rendering
**Status:** active
**Context:** M3 originally planned to wrap topologyCanvas.js (10K LOC from Blazor UI). Initial integration worked but was rough — canvas sizing issues, requires overlay payload before draw, and the approach duplicates the Blazor rendering code.
**Decision:** Use dag-map library instead. dag-map is our own library with a general-purpose flow visualization roadmap. Extend dag-map with features needed by FlowTime (heatmap mode, click events, hover) rather than wrapping the Blazor-specific canvas JS.
**Consequences:** M4 (timeline) now depends on dag-map heatmap mode being implemented first. dag-map features must remain general-purpose, not FlowTime-specific. topologyCanvas.js stays in Blazor UI only.

## D-2026-03-30-002: pnpm for Svelte UI package management
**Status:** active
**Context:** Root repo uses npm (for Playwright tests). The `ui/` project needed a package manager.
**Decision:** Use pnpm — aligns with shadcn-svelte documentation conventions, already installed in devcontainer (v10.33).
**Consequences:** `ui/` has pnpm-lock.yaml, not package-lock.json. init.sh runs `pnpm install --frozen-lockfile` for ui/.

## D-2026-03-30-003: Manual shadcn-svelte initialization
**Status:** active
**Context:** `shadcn-svelte init` CLI is interactive-only (prompts for preset selection), cannot be run non-interactively in CI or automation.
**Decision:** Manually create `components.json`, `utils.ts`, and `app.css` theme variables. Add components individually via `yes | pnpm dlx shadcn-svelte add <component>`.
**Consequences:** Works in non-TTY environments. Must manually keep components.json aligned with shadcn-svelte schema on upgrades.

## D-2026-03-30-004: Pin bits-ui to 2.15.0
**Status:** superseded by D-2026-04-02-002
**Context:** bits-ui 2.16.4 has broken dist/types.js — references `../bits/pin-input/pin-input.svelte.js` and `./attributes.js` which don't exist in the published package.
**Decision:** Pin bits-ui to 2.15.0 until the issue is fixed upstream.
**Consequences:** Check for fix on bits-ui releases periodically. Can unpin when 2.16.5+ ships.

## D-2026-03-30-005: dag-map lineGap default for single-route graphs
**Status:** active
**Context:** dag-map's lineGap (parallel line offset at shared nodes) defaults to 5px. For auto-discovered routes, this causes the trunk to wobble even when there's only one visual route.
**Decision:** Default lineGap to 0 when routes are auto-discovered (not consumer-provided). Only use non-zero lineGap when consumer explicitly provides multiple routes.
**Consequences:** Single-route graphs render with straight trunks. Multi-route flow layouts still get parallel line separation.

## D-2026-03-30-006: Svelte UI heatmap uses derived.utilization from state API
**Status:** active
**Context:** The FlowTime state API returns metrics at multiple levels: `metrics.*` (raw), `derived.*` (computed), `byClass.*` (per-class). Needed to pick the right field for heatmap coloring.
**Decision:** Use `derived.utilization` as primary heatmap metric, `derived.throughputRatio` as fallback. Other focus metrics (SLA, error rate, queue depth) to be added via a metric selector chip.
**Consequences:** Heatmap works end-to-end for utilization. Need to add metric selector for other derived fields.

## D-2026-03-31-001: Fix P0 engine bugs before further Svelte UI work
**Status:** active
**Context:** Engine deep review found 3 P0 bugs (shared series mutation, missing capacity dependency, dispatch-unaware invariant). Svelte UI shows data from these APIs — incorrect engine data means incorrect visualization.
**Decision:** Prioritize Phase 0 bug fixes (BUG-1, BUG-2, BUG-3) before continuing Svelte UI M4 completion or M5/M6.
**Consequences:** Svelte UI work pauses briefly. Engine correctness gates all downstream work.

## D-2026-04-02-001: Run orchestration defaults to simulation mode
**Status:** active
**Context:** Telemetry mode requires capture CSV files on disk (under `examples/time-travel/{captureKey}/`). In dev environments these may not exist, causing 500 errors. Simulation mode always works.
**Decision:** M6 run orchestration defaults to simulation mode for all templates. Telemetry mode support deferred until capture generation workflow is in the UI.
**Consequences:** Runs always succeed but produce synthetic data. Telemetry mode (real CSV data) needs a separate workflow to generate captures first.

## D-2026-04-02-002: Upgrade bits-ui to 2.16.5
**Status:** active
**Context:** bits-ui 2.15.0 was missing RadioGroup exports (empty `export {}`). bits-ui 2.16.4 had broken type exports. bits-ui 2.16.5 fixes both issues.
**Decision:** Upgrade bits-ui from 2.15.0 to 2.16.5. Remove the pin.
**Consequences:** RadioGroup (and other newer primitives) now available. shadcn-svelte radio-group component works correctly.

## D-2026-04-02-003: Epic numbering convention (E-xx)
**Status:** active
**Context:** Epics had no IDs, only slugs. Hard to see sequence at a glance in roadmap and folder listings.
**Decision:** Number epics sequentially starting at E-10. Affects folder name (`work/epics/E-{NN}-<slug>/`), branch name (`epic/E-{NN}-<slug>`), and milestone IDs (`m-E{NN}-<MM>-<slug>`). Forward-only — completed epics before E-10 stay unnumbered. Mid-term/aspirational epics get numbered when sequence is certain.
**Consequences:** All `.ai/` templates, skills, and paths updated. Active/planned epics need renaming when numbers assigned.

## D-2026-04-02-004: dag-map work scoped within consuming epics
**Status:** active
**Context:** dag-map is a cross-cutting library that multiple epics need (path highlighting for Path Analysis, edge coloring for Inspector, constraint visualization for Dependency Constraints). Considered making it a separate epic.
**Decision:** dag-map enhancements are scoped as deliverables within the consuming epic's milestones, not a separate epic. Same pattern as M4 pulling in "dag-map heatmap mode."
**Consequences:** Each epic that needs dag-map features includes them in its milestone specs. No separate dag-map epic or backlog.

## D-2026-04-02-005: Epic sequence E-10 through E-15
**Status:** active
**Context:** Needed to assign E-xx numbers to active and planned epics.
**Decision:** E-10 Engine Correctness, E-11 Svelte UI, E-12 Dependency Constraints, E-13 Path Analysis, E-14 Visualizations, E-15 Telemetry Ingestion. Mid-term epics (E-16+) numbered when sequenced.
**Consequences:** E-12 is mostly done (M-10.01/02 complete, only MCP enforcement remains). E-13 Path Analysis includes dag-map path highlighting work. E-14 Visualizations is Svelte chart work (no dag-map). E-15 Telemetry Ingestion is independent but sequenced last.

## D-2026-04-02-006: Reprioritize E-10 Phase 3 before E-11 continuation
**Status:** active
**Context:** E-10 Phase 3 (analytical primitives: cycle time, WIP limits, variability, constraint enforcement) was paused after Phases 0-2 to work on E-11 Svelte UI. Phase 3 unlocks E-12/E-13/E-14 downstream and the specs are all approved.
**Decision:** Resume E-10 Phase 3 immediately (p3a → p3b → p3c → p3d). E-11 Svelte UI paused after M6 until Phase 3 completes. Epics and milestones proceed in sequence from here.
**Consequences:** E-11 M5/M7/M8 deferred. `milestone/m-svui-06` branch needs merge to main first. Next work: create `milestone/m-ec-p3a` from main.

## D-2026-04-03-001: Split post-p3a projection hardening into its own milestone
**Status:** active
**Context:** Review of m-ec-p3a found the core analytical primitive is sound, but the state projection and contract surfaces still have duplicated capability logic, metadata drift, finite-value hardening gaps, and incomplete client symmetry. Folding that cleanup back into p3a would blur the milestone boundary and make later Phase 3 work harder to sequence.
**Decision:** Track the post-review cleanup as a dedicated follow-on milestone, `m-ec-p3a1`, before continuing to p3b/p3c/p3d. p3a remains the primitive-introduction milestone; p3a1 owns analytical projection and contract hardening.
**Consequences:** Phase 3 order becomes p3a → p3a1 → p3b → p3c → p3d. Future analytical milestones should build on the hardened projection surface rather than duplicating ad hoc snapshot/window logic.

## D-2026-04-03-002: cycleTimeMs coexists with flowLatencyMs
**Status:** active
**Context:** Phase 3a introduced `cycleTimeMs` (per-node: queue + service time) alongside the existing `flowLatencyMs` (cumulative: graph-level propagation from entry to node). Needed to decide whether the new metric replaces or coexists with the old.
**Decision:** Coexist. `cycleTimeMs` answers "how long does work spend at this node?" while `flowLatencyMs` answers "how long does it take for work to get here from entry?" `flowLatencyMs` now uses `CycleTimeComputer` for its per-node base value, but the graph propagation stays in `StateQueryService`.
**Consequences:** Both fields appear in `NodeDerivedMetrics`. `cycleTimeMs` decomposes into `queueTimeMs` + `serviceTimeMs` with `flowEfficiency` as a ratio. `flowLatencyMs` remains the cumulative metric for end-to-end analysis.

## D-2026-04-03-003: Analytical capabilities and computation move to Core
**Status:** active
**Context:** Phase 3a review found the same "does this node have queue/service semantics?" decision duplicated 6 ways in `StateQueryService` (snapshot, window, metadata, stationarity warnings, per-class conversion, flow-latency composition) with diverging predicates. The adapter was doing engine work — capability decisions and metric computation are domain knowledge, not projection concerns.
**Decision:** m-ec-p3a1 moves analytical capability resolution and derived metric computation into `FlowTime.Core`. Core provides an `AnalyticalCapabilities` concept resolved once per node, plus a computation surface (capabilities + raw data → derived metrics with finite-value safety). `StateQueryService` becomes a stateless projector for analytical metrics — it consumes Core output and maps to contract DTOs. `flowLatencyMs` graph propagation stays in the adapter (orchestration concern). Non-analytical derived metrics (utilization, throughputRatio, retryTax) stay in the adapter for now.
**Consequences:** Capability parity (explicit vs logicalType-resolved `serviceWithBuffer`) is guaranteed by construction. Metadata honesty, stationarity warning eligibility, and finite-value safety are driven by capabilities, not ad-hoc adapter predicates. p3b/p3c/p3d add to Core's computation surface; the adapter stays thin.

## D-2026-04-03-004: E-16 owns full purification and the migration is forward-only
**Status:** active
**Context:** The p3a1 pressure test showed that moving the current analytical capability/computation surface into Core was the right bridge, but full purification is larger than one E-10 follow-on milestone. Compiled semantic references, class-truth separation, runtime analytical descriptors, contract redesign, and consumer heuristic deletion all need one clear owner. We also do not want compatibility shims for old runs, fixtures, or hint-based contracts to dilute the cleanup.
**Decision:** Wrap `m-ec-p3a1` as the bridge milestone and assign the full formula-first purification to E-16. E-16 is forward-only: old run directories, generated fixtures, and approved golden snapshots can be deleted and regenerated; contract cleanup does not need additive compatibility phases once the named consumers for a milestone are migrated.
**Consequences:** E-10 Phase 3 pauses after `m-ec-p3a1` until E-16 completes. Milestone planning should prefer explicit deletion and regeneration over fallback layers. Reviews should treat new compatibility heuristics around the old analytical/runtime boundary as regressions.

## D-2026-04-03-005: flowLatencyMs moves to Core in E-16
**Status:** active (supersedes D-2026-04-03-003 for flowLatencyMs specifically)
**Context:** D-2026-04-03-003 kept flowLatencyMs graph propagation in the adapter as an "orchestration concern" during m-ec-p3a1 bridge work. E-16 milestone review found this is graph-level queueing theory: expected sojourn time through a network, computed as topological accumulation of per-node cycle times weighted by edge flow volumes. The Core IS a graph engine — graph-level analytical computation belongs there.
**Decision:** flowLatencyMs computation moves to Core in m-E16-04. The algorithm (`base = cycleTimeMs[node] + weightedAvg(flowLatencyMs[predecessors], edgeFlowVolume)`) becomes a pure Core evaluator function. The adapter passes topology + series inputs, Core returns results.
**Consequences:** m-E16-04 scope includes flowLatencyMs migration. D-2026-04-03-003 remains active for its other points (non-analytical derived metrics like utilization/throughputRatio/retryTax staying in the adapter was a bridge — E-16 now moves utilization to Core too since effective capacity is flow algebra).

## D-2026-04-03-006: Analytical descriptor absorbs AnalyticalCapabilities
**Status:** active
**Context:** m-ec-p3a1 introduced `AnalyticalCapabilities` as a Core bridge resolved from `kind + logicalType` strings. E-16 introduces a compiled analytical descriptor produced by the compiler from typed semantic references. The question is whether they coexist or the descriptor replaces capabilities.
**Decision:** The descriptor absorbs `AnalyticalCapabilities`. Capability flags become compiled descriptor fields. Computation methods (`ComputeBin`, `ComputeWindow`, etc.) move to the Core analytical evaluator (m-E16-04). `AnalyticalCapabilities.Resolve(kind, logicalType)` is deleted — string-based resolution is exactly what E-16 eliminates. `EffectiveKind` is removed as a bridge concept.
**Consequences:** m-E16-03 deletes `AnalyticalCapabilities`. m-E16-04 builds the evaluator from its computation methods. No coexistence period.

## D-2026-04-03-007: Parallelism typing included in E-16 m-E16-01
**Status:** active
**Context:** `NodeSemantics.Parallelism` is `object?`, parsed at runtime in both Core (`InvariantAnalyzer`) and API (`GetEffectiveCapacity`, `ParseParallelismScalar`). gaps.md deferred this as a 21-file cross-cut. Parallelism represents Kubernetes pod instances (service replicas) and scales effective capacity — this is flow algebra, not presentation.
**Decision:** Include Parallelism typing in m-E16-01 as part of typed semantic references. Replace `object?` with a typed reference (numeric constant or series ref) resolved at compile time. Effective capacity computation (`capacity x parallelism`) moves to Core evaluator in m-E16-04.
**Consequences:** The 21-file cross-cut is acceptable because E-16 is already touching the semantic reference boundary. gaps.md entry for Parallelism can be closed after m-E16-01.

## D-2026-04-04-008: Shared runtime parameter foundation is owned once and reused by E-17 and E-18
**Status:** active
**Context:** E-17 (Interactive What-If) and E-18 (Headless Pipeline & Optimization) both need the same foundational capability: identify editable parameters in compiled graphs, apply deterministic overrides without recompilation, and expose metadata suitable for either UI controls or programmatic callers. Duplicating that work across two future epics would create drift between the interactive and headless paths.
**Decision:** Own the shared runtime parameter foundation once in the programmable/headless layer. The foundation includes compiled parameter identity, override points, reevaluation APIs, and the contract for enriching human-facing metadata from authored template parameters when available. E-17 consumes that foundation for sessions, push delivery, and UI controls rather than defining a second runtime parameter model.
**Consequences:** E-18 foundation work (`m-E18-01/02`) precedes or runs alongside the UI/session-specific work in E-17. Reviews should treat any second, UI-only runtime parameter model as a regression.

## D-2026-04-04-009: Chunked evaluation is deferred beyond the first E-18 headless cut
**Status:** active
**Context:** The headless and optimization story benefits from a pure callable engine quickly, but chunked evaluation for feedback simulation depends on real stateful execution semantics. The current `IStatefulNode` seam is only a stub and is not sufficient to justify bundling chunked/stateful execution into the initial headless foundation.
**Decision:** Split E-18 into layers. The first cut covers the shared runtime parameter foundation, evaluation SDK, and headless CLI / sidecar. Advanced analysis modes (sweep, sensitivity, optimization, fitting) build on that. Chunked evaluation waits for a dedicated streaming/stateful execution seam.
**Consequences:** The sidecar/SDK foundation can ship without solving streaming/stateful execution. Reviews should reject attempts to block the headless foundation on chunked evaluation design.

## D-2026-04-06-010: File-backed refs stay opaque and missing class coverage omits byClass
**Status:** active
**Context:** E-16 review found two boundary leaks still active after typed semantic references landed: runtime code inferred producer node identity from file stems, and state projection synthesized wildcard `byClass` payloads from aggregate totals even when runs had no explicit class series. Both behaviors blurred the compiler/runtime boundary and made missing class coverage indistinguishable from explicit fallback coverage.
**Decision:** File-backed compiled references remain opaque at runtime: they provide authored lookup keys, not producer node IDs. State and graph projection only emit wildcard `byClass` when explicit fallback class series exist; runs with missing class coverage omit `byClass` entirely.
**Consequences:** Logical-type promotion, queue-origin checks, and graph dependency resolution cannot rely on file-name heuristics. Tests and approved snapshots that depended on implicit wildcard totals or file-stem inference must be regenerated forward-only.

## D-2026-04-06-011: E-16 explicitly owns the remaining transitional analytical seams
**Status:** active
**Context:** After the E-16-01 and E-16-02 cleanup, three transitional seams still remained visible: `RunManifestReader` could recover telemetry-source facts by reparsing raw YAML text, class ingestion still translated legacy `*` / `DEFAULT` fallback markers, and `MetricsService` still carried a second analytical execution path via model-evaluation fallback when state-window resolution failed.
**Decision:** These are not acceptable permanent bridges. E-16 explicitly owns removing all three: m-E16-01 removes raw-model-text telemetry-source fallback readers, m-E16-02 removes legacy class-fallback translation helpers once regenerated runtime metadata carries explicit fallback labeling, and m-E16-04 removes the duplicate `MetricsService` analytical fallback path in favor of one Core evaluator surface.
**Consequences:** Review and wrap should treat any of these helpers surviving beyond their owning milestone as an incomplete E-16 implementation, not a tolerable compatibility layer.

## D-2026-04-06-012: Pull analytical evaluator extraction into E-16-03
**Status:** active
**Context:** Deleting `AnalyticalCapabilities` in m-E16-03 left its computation methods (`ComputeBin`, `ComputeWindow`, metadata gates, stationarity checks) without a truthful owner. Waiting until m-E16-04 would have required either keeping the bridge alive or renaming it in place, which would violate the no-coexistence rule.
**Decision:** Extract the descriptor-backed `RuntimeAnalyticalEvaluator` in m-E16-03 as the minimal owner for the surviving analytical computation surface. m-E16-04 still owns broader evaluator consolidation (flow-latency migration, emitted-series truth, warning fact cleanup), but not the initial extraction itself.
**Consequences:** `AnalyticalCapabilities` can be deleted cleanly in m-E16-03. The runtime descriptor now carries typed analytical identity and the evaluator consumes descriptor facts directly. m-E16-04 builds on an existing evaluator instead of performing the first bridge cut.

## D-2026-04-06-013: E-16-04 removes legacy analytical run paths forward-only
**Status:** active
**Context:** m-E16-04 removes the duplicate analytical fallback in `MetricsService`. One open question was whether unsupported legacy runs should fail with an explicit regeneration message or be tolerated through an upgrade boundary.
**Decision:** Neither. E-16 remains strictly forward-only: legacy runs that depend on the old analytical/runtime boundary are deleted and replaced with regenerated runs. m-E16-04 removes the fallback path without introducing an "unsupported, regenerate" runtime mode.
**Consequences:** `MetricsService.ResolveViaModelAsync()` and similar legacy analytical rescue paths are pure cleanup targets, not compatibility seams. Tests, fixtures, and local run directories that still depend on those paths must be regenerated or removed during the milestone.

## D-2026-04-06-014: E-16-04 uses one consolidated internal analytical result surface
**Status:** active
**Context:** m-E16-04 must move emitted-series truth, effective capacity, utilization, and graph-level flow latency into Core. An open design question was whether Core should return many narrow result types or one consolidated analytical result.
**Decision:** Use one consolidated internal Core analytical result surface, with explicit nested sections rather than a flat bag of fields. The result owns snapshot/window/by-class analytical outputs, emitted-series truth, effective-capacity facts, and graph-level flow-latency outputs so adapters project one source of truth instead of recomposing partial answers.
**Consequences:** This does not require one public contract type, and it does not prevent later milestone-specific refinements. The important constraint is that adapters and query surfaces consume one coherent Core result model instead of rebuilding analytical policy from multiple partial calculators.

## D-2026-04-06-015: Blazor remains a supported parallel UI surface
**Status:** active
**Context:** Initial E-19 planning language assumed E-11 Svelte UI would replace and eventually retire the Blazor UI. The intended product direction is different: Svelte is a parallel UI track for demos and future evaluation, while Blazor remains useful for debugging, operational workflows, and as a plan-B surface if Svelte is not the long-term primary UI.
**Decision:** Keep `FlowTime.UI` as a supported first-party UI. E-11 builds Svelte in parallel rather than as a replacement. E-19 may remove stale compatibility wrappers and duplicate fallback logic from Blazor, but it does not retire Blazor or strip supported functionality; both UIs must stay aligned with evolving Engine/Sim contracts.
**Consequences:** Planning docs must not describe Svelte as a committed Blazor replacement. Cleanup milestones target stale compatibility seams, not supported Blazor capabilities. Reviews should treat Blazor functionality regressions caused by cleanup as bugs unless explicitly approved.

## D-2026-04-07-016: E-19 owns current-surface orchestration cleanup; E-18 owns the headless foundation
**Status:** active
**Context:** Current template-driven run creation lives in `FlowTime.Sim.Service`, with storage-backed drafts, archived run bundles, bundle import flows, and catalog-era residue still visible on active first-party surfaces. The repo also has a planned headless future in E-18. Without an explicit boundary, today's Sim orchestration and archive/import choreography can harden into the accidental programmable contract.
**Decision:** E-19 owns inventorying, narrowing, and deleting current Sim/UI/catalog/storage compatibility seams and publishing the supported-surface matrix. E-18 owns only the replacement headless foundation: runtime parameter identity, deterministic overrides, reevaluation APIs, evaluation SDK, and headless CLI/sidecar over compiled graphs. Current Sim authoring/orchestration, storage-backed drafts, archived run bundles, and bundle import flows are not the default path forward unless E-19 explicitly retains a surface.
**Consequences:** Planning docs must treat today's Sim orchestration as either current-surface residue or explicitly retained authoring support, not as the future programmable contract. New consumers of draft/catalog/bundle-ref surfaces should be avoided until E-19 inventory is complete. `docs/architecture/template-draft-model-run-bundle-boundary.md` is the terminology and ownership reference for this boundary.
**Subsequent update (2026-04-07):** The word "headless" in this decision is retained as historical text. The execution component is now named `FlowTime.TimeMachine` (the Time Machine) — see D-2026-04-07-018. The scope of E-18 is unchanged; only the component name is updated.

## D-2026-04-07-017: Validation is a first-class, client-agnostic Time Machine operation (E-19 A6)
**Status:** active
**Context:** `POST /api/v1/drafts/validate` exists in `FlowTime.Sim.Service` and calls `TemplateInvariantAnalyzer`, which chains through `ModelCompiler`, `ModelParser`, `Graph.Evaluate`, and `InvariantAnalyzer`. The endpoint does use Core correctly, but (a) no UI calls it — only tests exercise it, (b) its name is misleading because it actually runs the graph, not "just validates," and (c) future clients will include MCP servers and external AI agents generating candidate models who need tiered validation (cheap per-iteration checks, heavier pre-run checks). Keeping a Sim-private mislabeled endpoint alive would lock validation into one privileged client just as it is about to be needed by many.
**Decision:** Retire `POST /api/v1/drafts/validate` in m-E19-02. Preserve every library piece (`ModelSchemaValidator`, `ModelValidator`, `ModelCompiler`, `ModelParser`, `TemplateInvariantAnalyzer`, `InvariantAnalyzer`). Record a hard E-18 dependency: the Time Machine must expose **tiered validation** (Tier 1 schema / Tier 2 compile / Tier 3 analyse) as a first-class operation alongside compile, evaluate, reevaluate, parameter override, and artifact write. All three tiers callable from in-process SDK, CLI, and sidecar protocol with consistent request/response shapes. Client list with no privileged client: Sim UI, Blazor UI, Svelte UI, MCP servers, external AI agents, tests, CI. Validation is not optional for E-18 — without cheap tiers, AI inner loops and editor-time UX both fail for cost reasons.
**Consequences:** m-E19-02 deletes the HTTP endpoint and its tests; `work/epics/E-18-headless-pipeline-and-optimization/spec.md` gains a "Tiered validation (required scope)" section and the tiered validation work lands in m-E18-01b (per the m-E18-01 split in D-2026-04-07-021). The principle is recorded in `docs/architecture/template-draft-model-run-bundle-boundary.md` as part of the m-E19-01 boundary ADR update. Sim's authoring surfaces `/api/v1/templates/{id}/generate` and `/api/v1/drafts/generate` remain unchanged — they validate as a side effect of materialising a model and are not replacements for the first-class validation operation.

## D-2026-04-07-018: Execution component is `FlowTime.TimeMachine` (Time Machine), not "Headless"
**Status:** active
**Context:** E-18's new execution component was provisionally called "Headless" throughout planning docs. The name is a poor fit: it defines the component by what it isn't (no UI), rather than what it is; it's overloaded in software vocabulary (headless CMS, headless browser); and it implies subordination to some "headed" component, when in fact UIs and other clients will be callers of it. Meanwhile FlowTime's execution semantics map exactly to the formal definition of an abstract machine (instructions = compiled graph, state = time grid + accumulating series, execution = deterministic topological stepping) in the BEAM/JVM sense, and FlowTime's existing Blazor "Time Travel" UI feature pairs naturally with a backend component named "Time Machine." The reevaluation semantics (rewind a compiled model, run it forward with different parameters) are also literally time travel.
**Decision:** Name the component `FlowTime.TimeMachine` (referred to in prose as "the Time Machine"). This is a new component added by E-18. `FlowTime.Core`, `FlowTime.API`, and `FlowTime.Sim.*` keep their names — no renames in E-19 beyond introducing the new component. `FlowTime.Generator` is a separate case handled by D-2026-04-07-019 (Path B extraction and deletion). E-18's directory path `work/epics/E-18-headless-pipeline-and-optimization/` is preserved for historical stability; the epic content is titled *E-18 Time Machine*.
**Consequences:** m-E19-01 and the E-18 epic spec body use "Time Machine" / `FlowTime.TimeMachine` throughout. D-2026-04-07-016's "headless foundation" language is historical and superseded by this entry for naming purposes only — the scope and ownership decisions in D-016 are unchanged. `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md`, and the boundary ADR under `docs/architecture/` are also synced to the new naming during m-E19-01 wrap. The research note at `docs/research/flowtime-headless-integration.md` is historical and not renamed.

## D-2026-04-07-019: `FlowTime.Generator` fate — Path B (extraction and deletion) in E-18
**Status:** active
**Context:** `FlowTime.Generator` is today's shared orchestration layer between `FlowTime.Sim.Service` and `FlowTime.API`. It owns `RunOrchestrationService`, `RunArtifactWriter`, deterministic run ID logic, RNG seeding, dry-run/plan mode, and both simulation-mode and telemetry-mode code paths. Sim.Service depends on Generator (not Core) for execution. API depends on both Core and Generator. The Time Machine (`FlowTime.TimeMachine`, D-2026-04-07-018) is a new E-18 component whose responsibilities overlap Generator's almost completely: compile, evaluate, artifact write, run IDs, RNG seeding, dry-run all appear on both sides. Keeping Generator alive alongside the Time Machine would create two shared orchestration layers doing the same pipeline, violating the no-coexistence discipline established in E-16 and E-19. m-E19-01's shared framing originally left Generator's forward role implicit, which was a gap.
**Decision:** Path B — extraction and deletion. In **E-18 m-E18-01a** (the dedicated Path B cut, see D-2026-04-07-021 for the m-E18-01 split rationale), all of Generator's execution-pipeline responsibilities are extracted into the new `FlowTime.TimeMachine` project: `RunOrchestrationService` becomes Time Machine Compile/Evaluate/ArtifactWrite operations, `RunArtifactWriter` becomes Time Machine canonical run directory writer (preserved unchanged in shape and clear-text-debuggable layout), `RunDirectoryUtilities` and `RunOrchestrationContractMapper` become Time Machine supporting infrastructure, deterministic run ID logic becomes Time Machine run identity, RNG seeding becomes Time Machine parameter/run configuration, simulation-mode code becomes the Time Machine evaluate path, and dry-run/plan mode is preserved as a concrete Time Machine capability (it later folds into tier 2 compile-only validation when m-E18-01b introduces the tiered validation surface — but that folding is m-E18-01b's job, not 01a's). Telemetry-generation code (`TelemetryBundleBuilder`, `TelemetryBundleOptions`, `TelemetryCapture`, `TelemetryGenerationService`, `CaptureManifestWriter`, `RunArtifactReader` from `Capture/`) is extracted alongside the execution code per D-2026-04-07-020 — not as a parallel shared layer in the Time Machine, but as the Time Machine's concrete canonical bundle writer plus the concrete `CanonicalBundleSource` reader. In m-E18-01a these exist as concrete classes without an `ITelemetrySource` interface; the interface is introduced later in m-E18-01b. `FlowTime.Sim.Service` and `FlowTime.API` swap their Generator reference for `FlowTime.TimeMachine` (or `FlowTime.Core` directly where API only needs reads). The existing public surfaces `POST /telemetry/captures` and `flowtime telemetry capture` are re-wired to the new home with their request/response contracts unchanged. **`FlowTime.Generator` is deleted in the same milestone cut — no coexistence window.** Path A (rename/promote Generator to TimeMachine) and Path C (shrink Generator to authoring helper) are explicitly rejected: Path A muddles history and conflates rename-with-extend; Path C leaves Generator as a tiny one-consumer project not worth its own boundary. Path B is the cleanest separation.
**Consequences:** E-18 m-E18-01a is a refactor-only milestone — same behavior, new home. The Time Machine ships in 01a with no new features beyond what Generator already does today; tiered validation (01b) and the runtime parameter foundation with reevaluation (01c) are sequenced as separate additive milestones on top, per D-2026-04-07-021. E-18's success criteria gain a grep guard: `rg "FlowTime\.Generator" src/ tests/` returns zero matches after 01a. E-19 is unaffected — Generator is frozen and unchanged during E-19, and m-E19-01's shared framing item 3 records Path B as the decided forward path. The canonical run directory layout at `data/runs/<runId>/` is preserved unchanged (clear-text, debuggable, in-place). Canonical bundle writing is preserved as a concrete capability via the same code that previously lived in Generator's `TelemetryBundleBuilder`. No change to `FlowTime.Core`, the canonical run.json contract, the canonical bundle schema, or the analytical surfaces purified by E-16. Path B is a project-boundary refactor, not a contract or data refactor. The transition window (E-19 → E-18 m-E18-01a) during which Sim.Service still uses Generator for execution is the same transitional execution host behaviour already recorded in A1; when 01a completes, Sim.Service's `/api/v1/orchestration/runs` endpoint is deleted in favour of direct UI-to-Time-Machine calls by default. A temporary thin facade is allowed only if a concrete technical migration constraint is documented in the owning E-18 milestone, with explicit removal criteria.

## D-2026-04-07-020: Telemetry as adapter concern; one canonical bundle format; ITelemetrySource introduced, ITelemetrySink deferred
**Status:** active
**Context:** `FlowTime.Generator` is, despite its current "shared orchestration" role, originally and primarily a telemetry generator: it contains `TelemetryGenerationService`, `TelemetryBundleBuilder`, `TelemetryBundleOptions`, `TelemetryCapture`, `CaptureManifestWriter`, `RunArtifactReader` (under Capture/), and `GapInjector` (under Processing/). Today it produces canonical bundles in the format defined by E-15 (`model.yaml`, `manifest.json`, `series/`, CSV files; schema at `docs/schemas/telemetry-manifest.schema.json`) via `POST /telemetry/captures` and `flowtime telemetry capture`. The repo also has an unnumbered `telemetry-loop-parity` epic at `work/epics/telemetry-loop-parity/spec.md` that defines the **telemetry loop** as a 4-step round-trip (baseline run → capture bundle → replay → parity comparison) with established vocabulary: capture, replay, parity, baseline run, replay run. The Path B Generator extraction (D-019) needs an explicit answer for where Generator's telemetry-generation code goes and what the contracts look like.
**Decision:** (1) Telemetry is an **adapter concern outside** the Time Machine. `FlowTime.TimeMachine` contains no external-telemetry-format-specific code (no Prometheus, no OTEL, no BPI event log parsing). External-format ingestion lives in adapter projects under `FlowTime.Telemetry.*`. (2) **Exception**: writing the **canonical bundle format** is a Time Machine **core capability**, not a pluggable adapter, because it serves the telemetry loop that is fundamental to FlowTime's bootstrap/self-consistency/AI-iteration use cases. Canonical bundle writing is a concrete `WriteCanonicalBundle` method on the Time Machine, not behind an interface. (3) **Two artifact kinds preserved**: the canonical run directory (`data/runs/<runId>/model/`, `series/`, `run.json`) is the in-place clear-text debugging surface, always written; the canonical bundle (`model.yaml`, `manifest.json`, `series/`, CSV) is the portable interchange format, written on demand. They are intentionally distinct, with different purposes and potentially different shapes. The bundle format may evolve independently of the run directory format if a future milestone decides interchange needs differ from in-place needs. (4) **`ITelemetrySource` is introduced** by E-18 m-E18-01b as the input contract (after m-E18-01a creates the concrete `CanonicalBundleSource` reader without an interface), with multiple implementations once 01b ships: `CanonicalBundleSource` (replay; lifted from concrete class to interface implementor), `FileCsvSource` (extracted from Core's existing `file:` references), and future Prometheus/OTEL/event-log source adapters delivered by m-E18-06 (and E-15). The contract must carry enough metadata to round-trip the canonical bundle losslessly. (5) **`ITelemetrySink` is explicitly deferred** until a second sink format is required. Per the "don't create abstractions for one-time operations" principle, no sink interface is built on speculation. Canonical bundle writing is a concrete capability, not a pluggable sink. (6) **Path B telemetry extraction**: `TelemetryBundleBuilder`, `TelemetryCapture`, `TelemetryGenerationService`, `CaptureManifestWriter`, `RunArtifactReader` move into the Time Machine's canonical bundle writer and `CanonicalBundleSource`. `GapInjector` becomes an internal transform inside whichever adapter consumes it (or moves to the Telemetry Loop & Parity epic if it primarily drives parity tolerance tests). Existing `POST /telemetry/captures` API and `flowtime telemetry capture` CLI are re-wired to the new home without contract changes. (7) **The telemetry loop is a first-class use case** with three purposes: specification/bootstrap (generate target telemetry from a model to define what the real system must emit), self-consistency testing (round-trip parity), and AI iteration / model fitting (compare generated to observed real telemetry, adjust model, iterate). Round-trip consistency for the canonical bundle format is a hard requirement.
**Consequences:** E-18 m-E18-01a (extraction cut, per D-2026-04-07-019 and D-2026-04-07-021) extracts the concrete canonical bundle writer and the concrete `CanonicalBundleSource` reader from Generator into the Time Machine, without an `ITelemetrySource` interface yet. E-18 m-E18-01b introduces the `ITelemetrySource` interface, lifts `CanonicalBundleSource` to implement it, adds `FileCsvSource` as a second implementation, and ships tiered validation. m-E18-06 is reshaped from "Telemetry I/O" to "Telemetry Ingestion Source Adapters" — source-only adapters for real-world formats (Prometheus, OTEL, BPI, etc.), no Time Machine changes, no sinks. m-E18-04 (Optimization & Fitting) gains a hard prerequisite on the Telemetry Loop & Parity epic completing first. The parity harness, drift tolerance rules, and CI gating are explicitly out of E-18's scope and remain owned by the Telemetry Loop & Parity epic. The canonical run directory layout is preserved unchanged; canonical bundle writing is preserved as a Time Machine core capability via the same code that previously lived in Generator. E-15's canonical bundle schema is the alignment dependency. m-E19-01's shared framing item 9 records this principle for downstream E-19 milestones to cite. Open question deferred to m-E18-01a detail design: the exact relationship between `TelemetryBundleBuilder`'s output and the canonical run directory layout (sub-question of how the bundle format derives from or differs from the run directory in concrete terms; the user has confirmed they are conceptually distinct artifacts with different purposes, so any shared shape is incidental rather than required).

## D-2026-04-07-021: Split E-18 m-E18-01 into 01a (Path B extraction), 01b (validation + source contract), 01c (parameter foundation + reevaluation)
**Status:** active
**Context:** Earlier drafts of E-18 had m-E18-01 carrying every foundation responsibility in one milestone: create `FlowTime.TimeMachine`, extract Generator's execution code, extract Generator's telemetry-generation code, delete Generator, design and implement `ITelemetrySource` contract, implement `CanonicalBundleSource` and `FileCsvSource`, build tiered validation (schema/compile/analyse) as first-class operations, design the shared runtime parameter foundation with stable parameter identity, and implement the reevaluation API. That bundle is realistically three distinct chunks of work with different natures (refactor / additive features / new design), tied together only by sharing the same target project. Sizing them as one milestone hides honest dependency structure and risks the milestone becoming impossible to ship in a single cut. The user explicitly asked whether the m-E18-01 work could be split across additional milestones.
**Decision:** Split m-E18-01 into three sub-milestones: **01a — Time Machine Creation & Generator Extraction (Path B core cut).** Pure refactor: same behavior as today, new home. Creates `FlowTime.TimeMachine`, extracts execution-pipeline code and telemetry-generation code from Generator (per D-2026-04-07-019 and D-2026-04-07-020), updates Sim.Service and API references, re-wires `POST /telemetry/captures` and `flowtime telemetry capture` to the new home without contract changes, deletes `FlowTime.Generator`. No new features. Concrete `CanonicalBundleSource` exists without an `ITelemetrySource` interface. Dry-run/plan mode is preserved as a concrete capability. This is the no-coexistence cut Path B requires. **01b — Tiered Validation & Telemetry Source Contract.** Additive features on top of 01a. Defines the `ITelemetrySource` interface rich enough to round-trip the canonical bundle format losslessly. Refactors `CanonicalBundleSource` to implement `ITelemetrySource`. Introduces `FileCsvSource` (extracted from Core's existing `file:` reading) as a second implementation. Implements tiered validation (Tier 1 schema, Tier 2 compile, Tier 3 analyse) as first-class Time Machine operations callable identically from SDK, CLI, and sidecar — satisfying E-19 m-E19-01 A6 (D-2026-04-07-017). Folds the dry-run/plan capability inherited from 01a into Tier 2 compile-only validation. **01c — Runtime Parameter Foundation & Reevaluation.** The shared parameter model that E-17 also consumes: compiled parameter identities (stable handles into compiled graphs), parameter override surface, reevaluation API (compile once, evaluate many), optional enrichment contract for template-authored parameter metadata. Substantial new design work. Independent of 01b — could run in parallel after 01a — but listed sequentially in the milestone table for simplicity. Existing m-E18-02 through m-E18-06 are unchanged and depend on the union of 01a, 01b, and 01c being complete (see specific dependencies in each milestone).
**Consequences:** E-18's milestone table is updated to list m-E18-01a, m-E18-01b, m-E18-01c as three rows. m-E18-02 (Time Machine CLI/Sidecar) depends on 01a + 01b + 01c so it can expose validation and reevaluation through the surfaces. m-E18-03 (Sweep & Sensitivity) depends on 01c (parameter foundation, reevaluation). m-E18-04 (Optimization & Fitting) depends on 01c plus the Telemetry Loop & Parity epic. m-E18-05 (Chunked Evaluation) depends on the stateful execution seam (unchanged). m-E18-06 (Telemetry Ingestion Source Adapters) depends on 01b (the `ITelemetrySource` contract). Body references in the E-18 spec to "m-E18-01" are updated to point at the specific sub-milestone where each responsibility lives. D-2026-04-07-019 and D-2026-04-07-020 are updated to reference m-E18-01a as the extraction cut. m-E19-01's shared framing item 3 (which says Path B happens in "the same E-18 milestone") still reads correctly because m-E18-01a is that milestone — but if downstream E-19 work needs to cite a specific sub-milestone, it should cite 01a. Splitting does not introduce a coexistence window: 01a is still a single no-coexistence cut, and 01b/01c are additive on top of an already-clean Time Machine without Generator.

## D-2026-04-07-022: E-19 shared framing locks the current component roles without renames
**Status:** active
**Context:** m-E19-01 needed one authoritative framing for current cleanup so every later deletion or retention call used the same component boundaries.
**Decision:** No project renames happen in E-19. `FlowTime.Core` remains the pure evaluation library, `FlowTime.API` remains the query/operator surface over canonical runs, `FlowTime.Sim.Service` remains the template authoring surface plus transitional first-party execution host, and `FlowTime.TimeMachine` remains the E-18-owned replacement execution component. Forward-only cleanup deletes obsolete first-party endpoints outright rather than preserving 410, redirect, or advisory tombstone stubs. See `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md` and `docs/architecture/supported-surfaces.md`.
**Consequences:** E-19 narrows current surfaces without redefining the long-term component split. Cleanup milestones cite the shared framing rather than inventing local boundary language.

## D-2026-04-07-023: Sim orchestration remains a first-party UI-only bridge until the Time Machine ships
**Status:** active
**Context:** `POST /api/v1/orchestration/runs` is the live first-party UI path today, but letting it attract new callers would turn a transitional Sim host into a support obligation.
**Decision:** Keep Sim orchestration only as a first-party UI bridge. `/api/v1/orchestration/runs` remains supported for Blazor and Svelte, `/api/v1/drafts/run` remains only as the narrower inline-YAML path, no new non-UI callers land on either surface, and both are deleted by default when the Time Machine ships unless an E-18 milestone documents a concrete temporary facade requirement.
**Consequences:** m-E19-02 narrows `drafts/run` to inline-only, but the final sunset belongs to E-18. New programmable or external integration work must not target Sim orchestration.

## D-2026-04-07-024: Stored drafts are retired; only the inline run path may survive temporarily
**Status:** active
**Context:** Stored draft CRUD has no supported first-party UI caller. The only live reason to keep `drafts/run` is the explicit inline-YAML “run this now” path.
**Decision:** Retire stored drafts entirely in m-E19-02. Delete `/api/v1/drafts` CRUD, `StorageKind.Draft`, and `data/storage/drafts/`. Keep only the inline-source `POST /api/v1/drafts/run` path until the Time Machine replacement is ready.
**Consequences:** Draft persistence is no longer treated as a product promise. If model versioning is wanted later, it must be designed against compiled/runtime identity rather than resurrecting stored drafts.

## D-2026-04-07-025: Sim-side archived run bundles and bundle refs are retired from the current surface
**Status:** active
**Context:** Sim still writes ZIP/archive bundle residue under `data/storage/runs/` and returns `bundleRef`, but there is no production caller using that archive layer as the real run contract.
**Decision:** Delete the Sim-side archived run-bundle layer in m-E19-02. Remove ZIP writes to `data/storage/runs/`, remove `bundleRef`/`StorageRef` return values from orchestration responses, and keep the canonical run directory under `data/runs/<runId>/` as the runtime truth.
**Consequences:** Canonical runs remain the first-party runtime/query artifact. Portable bundle writing survives only as the deliberate E-18 Time Machine capability, not as a current Sim-side archive product surface.

## D-2026-04-07-026: Engine bundle-import branches and POST /v1/runs are deleted outright
**Status:** active
**Context:** `POST /v1/runs` still carries bundle-import branches that only tests exercise. Preserving the route only to return a rejection stub would still keep a legacy endpoint alive for advisory purposes, which conflicts with the repo's forward-only cleanup rule.
**Decision:** Delete bundle-import branches from `POST /v1/runs` in m-E19-02 and delete the route itself. No 410-style rejection stub is retained. More generally, E-19 cleanup milestones do not preserve obsolete first-party endpoints solely to tell callers where behavior moved.
**Consequences:** Current runtime ownership becomes clearer because dead routes disappear instead of lingering as migration hints. If cross-environment import returns later, it must come back as an explicitly designed Time Machine concern instead of surviving as unowned residue.

## D-2026-04-07-027: Catalog surfaces are retired from current first-party support
**Status:** active
**Context:** Catalog endpoints and UI helpers survive mostly as mock/default residue. Active first-party callers already behave as if catalogs are not used.
**Decision:** Delete catalog endpoints, services, UI selectors, placeholder `catalogId` plumbing, and `data/catalogs/` usage in m-E19-02.
**Consequences:** No current first-party caller is allowed to rely on catalogs. If catalog-like behavior is wanted later, it must be redesigned from a real use case rather than preserved through zombie endpoints.

## D-2026-04-07-028: Blazor and Svelte stay parallel supported UIs without parity requirements
**Status:** active
**Context:** E-19 needed an explicit UI support policy so cleanup work would not silently turn into Blazor retirement or parity gating.
**Decision:** Keep Blazor and Svelte as parallel supported first-party UIs. Shared contract changes must keep both functional, but feature parity is not required. Blazor-specific debugging/operator workflows remain supported, and cleanup work should remove stale wrappers rather than supported capabilities.
**Consequences:** m-E19-04 focuses on stale-wrapper removal and contract alignment, not UI retirement. Svelte is not blocked on missing Blazor features, and Blazor is not required to preserve deprecated compatibility paths.
