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
