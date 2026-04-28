# Milestone: Validation Surface (Svelte)

**ID:** m-E21-07-validation-surface
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** complete
**Created:** 2026-04-26
**Started:** 2026-04-27
**Completed:** 2026-04-28
**Branch:** `milestone/m-E21-07-validation-surface` (branched from `epic/E-21-svelte-workbench-and-analysis` after the m-E21-06 merge backfilled into the epic branch on 2026-04-26)

## Goal

Surface the per-run validation/analytics warnings that the Engine already attaches to every state-window response into the Svelte workbench, so the user sees model-level issues alongside the topology and heatmap they already inspect. The surface is read-only and consumer-side; it consumes the warnings that already arrive on `GET /v1/runs/{runId}/state_window` (the same response Svelte already requests for sparklines per m-E21-06). There is no Svelte-side editing, no edit-time validation, no new endpoint, and no `POST /v1/run` shape change.

## Context

### Settled backend seam (decided 2026-04-26)

After deep audit of the Blazor warning-surfacing path, the user has locked the Q1 backend-seam question to **Mirror Blazor**:

- **Single endpoint reused:** `GET /v1/runs/{runId}/state_window` (handler at `src/FlowTime.API/Program.cs:1028`). The Svelte client already calls this endpoint for m-E21-06 sparklines.
- **Warnings in the response body, today:** `BuildWarnings` (`src/FlowTime.API/Services/StateQueryService.cs:4752`) merges two sources into the response's `warnings[]` array — (a) persisted `Warnings[]` from `data/runs/<runId>/run.json` (`RunManifest.Warnings`, written by `RunArtifactWriter` from tier-3 `TimeMachineValidator` analyse output) and (b) per-query analytical warnings derived inside `StateQueryService` while the window is built (e.g., stationarity, retry-budget, queue-saturation projections). `BuildEdgeWarnings` (line 4783) builds the parallel `edgeWarnings: { <edgeId>: StateWarning[] }` map from the same persisted `RunWarning.EdgeIds` field.
- **Wire shape (what Blazor deserializes today):** `TimeTravelStateWarningDto` (`src/FlowTime.UI/Services/TimeTravelApiModels.cs:521`) — `{ code, message, severity, nodeId?, startBin?, endBin?, signal? }`. Blazor consumes this at `src/FlowTime.UI/Pages/TimeTravel/Topology.razor:175-209` (timeline-window tooltip) and `:965-1001` (inspector "Warnings" tab).
- **Implication for Svelte:** every run already carries warnings on every `state_window` response Svelte requests. The task is purely to consume the fields that already arrive — no backend work, no new endpoint, no run-artifact persistence change.

### What the Svelte client deserializes today (drift to fix)

`StateWindowResponse` in `ui/src/lib/api/types.ts:243` already declares `warnings: StateWarning[]`, but the Svelte `StateWarning` interface (line 69) is a **thinner shape** than what the API actually returns: `{ code, message, nodeId?, bins? }` — missing `severity`, `startBin`, `endBin`, `signal`, **and** the parallel `edgeWarnings` map is not declared on `StateWindowResponse` at all. The fields arrive on the wire and are silently dropped during deserialization. This milestone widens the type to mirror `TimeTravelStateWarningDto` exactly and adds the `edgeWarnings` map declaration so the data is actually reachable from Svelte components.

### Tier-1 / tier-2 errors do not reach this surface (matches Blazor)

`POST /v1/run` calls `ModelSchemaValidator.Validate(cleanYaml)` first. Tier-1 (schema) and tier-2 (compile) failures **reject the run** at the API level — the user gets `400 { error: "..." }` from the run attempt, not a warnings-panel item. No run artifacts are written and no `state_window` response exists for a rejected run. Consequently, only tier-3 (analyse) warnings — the ones `RunArtifactWriter` persists into `run.json` plus the analytical warnings `StateQueryService` derives per query — ever reach this surface. Blazor behaves the same way; this milestone matches that behaviour and does not invent UX for failed runs.

### What exists in the Svelte workbench today

- `/time-travel/topology` (the workbench surface). The route is structured as a single full-height flex column inside a `<main>` that sits next to the app-sidebar (the navigation rail rendered in `ui/src/routes/+layout.svelte` — Home / Time Travel / Analysis nav, shadcn collapsible-icon). The route's flex column holds, top-to-bottom: toolbar (run selector, metric selector, class filter, view switcher, node-mode toggle), view switcher, optional timeline scrubber strip, and a single `flex-1` split region whose top half is the canvas (DAG or heatmap, swapped by `<ViewSwitcher>`) and whose bottom half is the **workbench panel**. The workbench panel is the bottom region under the splitter.
- **Workbench panel layout today** (`ui/src/routes/time-travel/topology/+page.svelte` ~line 567-600): a single `<div style="height: {100 - splitRatio}%" class="overflow-auto bg-background">` whose content is either the empty-state message (`Click a node or edge to inspect`) or a single horizontal flex row `<div class="flex gap-2 p-2 flex-wrap items-start">` of `WorkbenchCard` (pinned nodes) followed by `WorkbenchEdgeCard` (pinned edges). There is no left column or right column inside this panel today; cards just flex-wrap across the full width.
- `ui/src/lib/api/flowtime.ts` — typed client; `getStateWindow(runId, startBin, endBin, mode?)` at line 109 is what m-E21-06 uses. The response is typed as `StateWindowResponse`, which under-declares warnings as noted above.
- `ui/src/lib/stores/view-state.svelte.ts` — shared view-state store. Owns `activeView`, `activeClasses`, scrubber state, sort/row-stability/fit-width/node-mode toggles, and `selectedCell: { nodeId, bin } | null`. **Pin state lives in `workbench.svelte.ts` and is exposed via proxies on the view-state store** — `pinNode(id, kind?)`, `unpinNode(id)`, `pinnedNodes`, `pinnedEdges`, `pinnedIds`, `isPinned(id)`. The pin proxies and `selectedCell` already compose: clicking a topology node calls `workbench.toggle(...)` and then `viewState.setSelectedCell(nodeId, currentBin)` so the workbench-card title cross-highlights.
- `ui/src/lib/components/workbench-card.svelte` — per-node panel card; cross-links to selected cell via `selected` prop driven by `viewState.selectedCell?.nodeId === pin.id` (m-E21-06 wiring).
- No prior validation UI in Svelte.

### Why this milestone is unblocked now

E-23 closed and merged 2026-04-26 — `ModelSchemaValidator` is the single validator and `TimeMachineValidator` writes tier-3 warnings into run artifacts. m-E21-06 delivered the shared view-state store and the workbench-card cross-link convention. The merge of `milestone/m-E21-06-heatmap-view` into `epic/E-21-svelte-workbench-and-analysis` is the prerequisite to branching this milestone — handled at start-milestone time, not here.

### Settled scope (user-confirmed at planning, 2026-04-26)

1. **Q1 — Backend seam: Mirror Blazor.** One consolidated view of all warnings carried in the `state_window` response. Errors-vs-warnings segregation is driven by the per-item `severity` field the API returns; tier classification is not surfaced (only tier-3 reaches the UI).
2. **Q2 — Panel placement: layout option (b1) — left column inside the workbench panel.** The validation panel is a left column **inside the existing workbench panel** (the bottom region under the splitter — same region that holds pinned cards today). Pinned cards continue to fill the rest of the panel to the right of the warnings column. **Collapse-on-zero behaviour:** when the run has zero warnings, the warnings column **collapses to zero width** (or to a minimal collapsed-header glyph that can be expanded), so pinned cards reclaim the full panel width; when warnings exist, the column expands to a sensible default (recommend ~280-320 px) and renders the list. The previously-empty workbench-panel state (`Click a node or edge to inspect`) becomes a same-region message in the pinned-card area to the right of the (zero-or-minimal-width) warnings column. **Note:** the app-sidebar in `ui/src/routes/+layout.svelte` (Home / Time Travel / Analysis nav rail) is **not** where this lives.
3. **Q3 — Severity rendering: flat list with per-row chip.** Mirror Blazor's inspector behaviour exactly — one merged warnings list, severity rendered as a per-row chip (`error` / `warning` / `info`), no divider segregation between severities. Sort order keeps errors first, warnings second, info third (driven by the `severity` field), then by `nodeId` (empty / null sorts last), then by `message`.
4. **Q4 — No tier indicator.** Only tier-3 warnings ever reach this UI (tiers 1 & 2 reject `POST /v1/run` and surface as HTTP errors elsewhere). A tier indicator would be decorative noise — drop it entirely; rely on the severity chip alone.
5. **Q5 — Empty-state string: `No validation issues for this run.`** This message is only briefly visible during the loading-but-not-yet-collapsed transition, since the panel collapses on zero warnings (decision 2). Keep the string for that transition window and for the optional minimal-header expanded state.
6. **Q6 — Heatmap-row badges deferred.** Per-edge UI surfacing — heatmap-row badges, edge-card warnings tab — stays out of scope. The type widening at AC1 makes this the cheap follow-up the data already supports.
7. **Topology graph indication — newly in scope.** Topology nodes and edges show warning/error indicators driven from the same `state_window` `warnings[]` + `edgeWarnings` source the panel reads. Node indicators come from the `nodeId` field on each warning; edge indicators come from the keyed `edgeWarnings` map. **Severity drives styling** (`error` vs `warning` vs `info`); when multiple warnings target the same node or edge, the most-severe wins.
8. **Bidirectional cross-link — newly in scope.** The shared `view-state.svelte.ts` store carries the cross-link state (no new store; reuse existing fields and pin proxies where possible). Two directions:
    - **Warning row → graph + card.** Click a warning row → if the row has a `nodeId`, call the existing `viewState.pinNode(nodeId, kind?)` proxy (which delegates to `workbench.toggle`) and set `viewState.setSelectedCell(nodeId, viewState.currentBin)` so the workbench-card title cross-highlights, matching the topology-click handler at `+page.svelte:106-119`. If the row carries an edge identity (an `edgeWarnings` row), pin the corresponding edge via the workbench's edge-pin path.
    - **Node / edge → warnings panel.** When `viewState.selectedCell` (or the active pin) names a node, the warnings panel highlights the matching warning rows; analogously for an edge selection. The simplest read is "filter the panel to that node/edge while one is selected"; an alternative read is non-filter highlight (de-emphasize others). The exact pixel behaviour is an open implementation note in the tracking doc, not an open question — `selectedCell` already carries `nodeId`, and the panel can derive its highlight set from it without new store fields. If a new field (e.g. `selectedWarningId`) genuinely surfaces as needed during build, name it inline at that time.

### Tier-3 warning shape (what actually arrives in `state_window.warnings[]`)

Each item: `{ code, message, severity ("warning"|"info"|"error" — default "warning"), nodeId?, startBin?, endBin?, signal? }`. Examples: queue-saturation projections, retry-budget exhaustion, stationarity failures, exhaust-path-with-no-sink. The UI must render `code`, `message`, optional `nodeId`, optional bin range; `nodeId` cross-links into the workbench's pin-by-node mechanism (AC7).

The parallel `edgeWarnings: { <edgeId>: StateWarning[] }` map carries warnings tied to specific edges (constraint-edge issues, propagation anomalies). This milestone consumes both — node-keyed for node-side surfacing, edge-keyed for edge-side surfacing — and renders both sets in the same warnings panel.

## Acceptance Criteria

1. **Svelte `StateWindowResponse` type widened to match the wire DTO.** `ui/src/lib/api/types.ts` `StateWarning` gains `severity`, `startBin?`, `endBin?`, `signal?` (mirroring `TimeTravelStateWarningDto`). `StateWindowResponse` gains `edgeWarnings: Record<string, StateWarning[]>`. The `bins?: number[]` field is removed (not in the wire DTO; was a phantom). No `getStateWindow(...)` signature change. Vitest covers a fixture-based round-trip parse asserting all fields populate.

2. **Validation panel surface — left column of the workbench panel.** A new validation panel renders as a **left column inside the existing workbench panel** on `/time-travel/topology` (the bottom region under the splitter at `+page.svelte` ~line 567-600). Pinned `WorkbenchCard` / `WorkbenchEdgeCard` items continue to fill the rest of the panel, to the right of the warnings column. The panel is reachable from both Topology and Heatmap views (the workbench panel persists across view switches per m-E21-06 AC2). The panel renders one merged list of items, sorted by severity (`error` → `warning` → `info` → unknown), then by `nodeId` (empty / null sorts last), then by `message`. Each item displays a chrome severity chip, the human-readable `message`, the `nodeId` when present, and a bin-range chip when `startBin` / `endBin` are present. Edge-keyed warnings (from `edgeWarnings`) render in the same flat list, identified by their edge identity (e.g. `from→to`) instead of `nodeId`. Items without `nodeId` and without an edge identity render with no pin affordance.

3. **Collapse-on-zero behaviour.** When the loaded run has zero warnings (combined `warnings.length === 0` and empty `edgeWarnings` map), the warnings column **collapses to zero width** (or to a minimal collapsed-header glyph that can be expanded), so pinned cards reclaim the full panel width. When warnings exist, the column expands to a sensible default (recommend ~280-320 px) and renders the list. **Resize / collapse-toggle UX:** a minimum collapsed footprint (zero or a thin header strip) and an expanded default width are settled at start-milestone confirmation; if the column is user-resizable in this milestone, the resize handle behaviour and persistence are recorded as an implementation note in the tracking doc rather than a separate AC. Empty-state string `No validation issues for this run.` applies during the loading-but-not-yet-collapsed transition and to the optional minimal-header expanded state.

4. **Empty-state versus no-data-state distinguished.** The panel distinguishes two states (the third — "no warnings field on the wire" — cannot occur because the field is non-optional in the API):
    - **Issues present:** items rendered per AC2.
    - **Empty (run completed, zero warnings):** column collapses per AC3; the explicit `No validation issues for this run.` string renders during the load transition and in any expanded minimal-header state.
    The classifier is a pure helper covered by vitest. (The "no run loaded yet" state is the existing pre-load empty surface and is owned by the surrounding workbench panel's `Click a node or edge to inspect` message, not this panel's concern.)

5. **Trigger T1 — surface warnings after a run.** When a run completes via the existing Svelte run flow and `/time-travel/topology` loads the new run, the workbench validation panel renders the warnings from that run's `state_window` response without any additional fetch. Branch covered by Playwright (see AC11).

6. **Trigger T2 — surface warnings when opening an already-run model.** When the user selects a run from the run-selector dropdown, the workbench validation panel renders the warnings from that run's `state_window` response. Branch covered by Playwright (see AC11).

7. **Topology node warning styling.** Nodes whose `nodeId` matches at least one entry in the loaded `warnings[]` array render a chrome warning indicator (small badge / dot / glyph or border treatment on the topology node). The indicator uses **chrome-scale tokens distinct from the data-viz palette**: new tokens `--ft-warn` (severity `warning`) and `--ft-err` (severity `error`), mirroring the `--ft-pin` / `--ft-highlight` precedent from m-E21-06. Severity drives the styling; when multiple warnings target the same node, the most-severe wins (`error` > `warning` > `info`). Nodes with no warnings render no indicator. The indicator is hidden (not rendered, not invisible) when the node has no warnings.

8. **Topology edge warning styling.** Edges whose identity (`from→to` or matching `edgeId` form used by `edgeWarnings`) appears as a key in `edgeWarnings` render a chrome warning indicator on the topology edge — analogous to AC7 but for edges. Same chrome tokens (`--ft-warn` / `--ft-err`); same severity-max collapse. The exact visual treatment (recoloured stroke, dashed accent, badge on the edge midpoint, or a sibling glyph) settles in the tracking doc Design Notes alongside the m-E21-06 conventions, but it must be visually distinct from the existing `--ft-viz-amber` selected-edge stroke at `+page.svelte:609-613`.

9. **Click-a-warning-row to pin and cross-highlight.** Clicking an item in the validation panel:
    - **If the row has a `nodeId`:** pins that node into the workbench (calls `viewState.pinNode(nodeId, kind?)`, which delegates to `workbench.toggle`) and sets `viewState.setSelectedCell(nodeId, viewState.currentBin)` so the workbench-card title cross-highlight renders. Mirrors the topology-click handler convention at `+page.svelte:106-119`.
    - **If the row is an edge-keyed warning:** pins the corresponding edge into the workbench via the existing edge-pin path so a `WorkbenchEdgeCard` renders for it.
    - **If the row has neither identity:** renders with no pin affordance; clicking is a no-op.
    Clicking is independent of which view is active (Topology or Heatmap); both views are kept in sync via the shared store per m-E21-06 AC13.

10. **Bidirectional cross-link from selection to warnings panel.** When a node or edge is selected in the workbench (driven by `viewState.selectedCell` for node selection or by the existing edge-pin state for edges), the warnings panel highlights the matching warning rows so the user can see which warnings apply to the current selection. The exact pixel behaviour (filter-only-matching vs highlight-and-de-emphasize-others) is an implementation-note decision recorded in the tracking doc rather than an AC; the contract is "selection → matching warning rows are visually distinguished." No new fields on the shared view-state store are required for the contract — the panel derives the highlight set from `viewState.selectedCell?.nodeId` and from the workbench's pinned-edge state. If a `selectedWarningId` field ends up genuinely needed during build, it lives in `view-state.svelte.ts` alongside `selectedCell` and is named at that time.

11. **Single source of truth.** Validation items for a given run live in exactly one place in the UI (one derived store keyed off the loaded `state_window` response). The warnings panel, the topology node styling (AC7), and the topology edge styling (AC8) all read from it. No duplicate fetching, no per-component re-derivation of "is this node warning-flagged?". Switching views (Topology ↔ Heatmap) does not refetch; selecting a different run does (because the underlying `state_window` call is what changes).

12. **New chrome tokens land alongside existing chrome scale.** `--ft-warn` and `--ft-err` are added to the chrome-token CSS surface alongside `--ft-pin` and `--ft-highlight` (per m-E21-06). Both light and dark themes; designed not to clash with the data-viz palette (per m-E21-02) or with `--ft-pin` / `--ft-highlight`. An `--ft-info` token is optional in this milestone — add only if the chosen Playwright fixture exercises an `info`-severity warning; otherwise note it as a follow-up.

13. **Tier-3-only scope explicit.** No tier-1 / tier-2 UX appears in this milestone. The user gets `400 { error }` from `POST /v1/run` for tier-1 / tier-2 failures via the existing run-orchestration surface; this milestone does not surface those failures in the workbench. (Captured in *Out of Scope*; restated as an AC so the build cannot quietly add a "validation failed" surface.)

14. **Testing — Playwright + vitest, every reachable branch covered.** Per the project's UI-testing hard rule: Playwright drives every shipped trigger end-to-end in a real browser; vitest covers pure helpers. **Vitest:**
    - Widened-type round-trip parse (AC1).
    - State classifier `classifyValidationState({ warnings, edgeWarnings }): 'empty' | 'issues'` — both branches.
    - Item sort `sortValidationItems(items): items[]` — severity desc → nodeId / edge-id (empty last) → message; ties at every level.
    - Severity-to-chrome-token helper — `error` / `warning` / `info` / unknown-fallback.
    - Severity-max collapse `maxSeverityForKey(items): 'error' | 'warning' | 'info' | null` — every combination plus empty.
    - Cross-link state-transition helper that derives "rows matching the current selection" from `viewState.selectedCell` and the warnings array (covers nodeId-match, edge-match, neither-match, no-selection).

    **Playwright** (extending or siblinging `tests/ui/specs/svelte-topology.spec.ts`; graceful-skip on dev-server / API unavailability per the project rule):
    - Trigger T1 — run a model with zero warnings → after run loads, the warnings column collapses (zero width or minimal-header) and pinned cards fill the panel; no topology warning indicators visible.
    - Trigger T1 — run a model with at least one warning (fixture chosen to exercise tier-3 analyser output reliably) → warnings column expands → row lists `nodeId`, `message`, severity chip → corresponding topology node renders a warning indicator with the right severity token.
    - Trigger T2 — open an already-run model with persisted warnings → warnings column populates from that run's `state_window` response.
    - Click a node-attributed warning row → corresponding workbench card pins → workbench-card title cross-highlights (m-E21-06 selected-state convention).
    - Click an edge-attributed warning row → corresponding `WorkbenchEdgeCard` pins → topology edge styled per AC8.
    - Click a topology node that has warnings → warnings panel highlights / filters to its rows (AC10).
    - Click a topology edge that has warnings → warnings panel highlights / filters to its rows (AC10).
    - Validation panel persists across view switch (Topology → Heatmap → Topology); node + edge indicators re-render correctly when switching back to Topology.
    - One spec asserting at least two distinct severities render with distinct chrome tokens (data fixture chosen to exercise both `warning` and `error` if a tier-3 fixture exists; otherwise skip with a clear message and file follow-up).
    A line-by-line branch audit of the new UI + helpers against tests is recorded in the tracking doc's Coverage Notes section, matching the m-E21-05 / m-E21-06 audit structure.

## Constraints

- **No edit-time validation.** No live `POST /v1/validate` against an in-progress text edit. The Svelte UI has no model editor today; this is a guardrail against scope creep into the expert-authoring epic.
- **No new backend endpoint and no `POST /v1/run` shape change.** Settled at planning (2026-04-26). The data already arrives on `GET /v1/runs/{runId}/state_window`; this milestone widens the consumer-side type and surfaces the fields. If a tier-1 / tier-2 / edit-time validation surface is wanted later, it is a separate milestone with its own backend decision (the live `POST /v1/validate` endpoint is already available for that future work).
- **Single source of truth.** Validation items for a given run live in exactly one place in the UI (one store / one shape) keyed off the loaded `state_window` response. Warnings panel, topology node styling, and topology edge styling all read from it.
- **Severity semantics come from the backend.** The Svelte UI does not invent or remap severities. The mapping is `severity` field → chrome token (`warning` → `--ft-warn`, `error` → `--ft-err`, `info` → optional `--ft-info` or default chrome treatment). If the backend ever introduces a new severity literal, the UI falls back to a default chrome treatment and the type widening at AC1 narrows the contract surface.
- **No data-viz palette used for warning chrome.** Indicators and severity chips use chrome-scale tokens (`--ft-warn`, `--ft-err`) — never the metric data-viz hues (teal/cyan, magenta/pink, etc., per m-E21-02's data-viz palette). Same reason as m-E21-06's `--ft-pin` / `--ft-highlight` separation: chrome signals must not compete with data signals. The new edge-warning treatment must also be visually distinct from `--ft-viz-amber` (selected-edge stroke at `+page.svelte:609-613`).
- **Cross-link state goes in the existing shared store.** No new store. Reuse `viewState.selectedCell` and the existing pin proxies; if a new field is genuinely needed during build (e.g. `selectedWarningId`), name it inline in `view-state.svelte.ts` at that time.
- **Tier-1 / tier-2 errors are not surfaced here.** `POST /v1/run` rejects them with `400 { error }`. The user gets an HTTP error response from the run attempt; this milestone does not invent a new UX for failed runs (matches Blazor behaviour). Restated as AC13 so the build cannot quietly add it.
- **Existing what-if and run-orchestration surfaces stay green.** This milestone touches the workbench surface (`/time-travel/topology`); it must not regress `/what-if`, `/run`, or any existing route.

## Design Notes

- **Panel layout — left column inside the workbench panel.** The existing workbench panel today is a single horizontal `flex gap-2 p-2 flex-wrap items-start` of pinned cards (`+page.svelte` ~line 574). This milestone wraps the panel content in a two-column layout where the left column is the warnings panel and the right column is the existing pinned-card flex row. When the warnings column collapses to zero width, the pinned-card region reclaims the full panel width, preserving today's behaviour for runs with no warnings.

- **Severity indicator candidates (panel rows + topology indicators):**
    - Coloured dot keyed to `--ft-warn` / `--ft-err` (chrome tokens, not data-viz). Most compact.
    - One-letter glyph chip (`E` / `W` / `I`).
    - Word label (`error` / `warning` / `info`) — most readable, highest chrome cost.
    Default leaning: **coloured dot** for the topology indicator; **dot + word label** for the panel-row chip — matches Blazor's chip-color convention. Locked at start-milestone confirmation. (Tier indicator from the prior draft is dropped per Q4.)

- **Token additions (chrome scale):**
    - `--ft-warn` — warning indicator and panel-row chip background (yellow/amber family).
    - `--ft-err` — error indicator and panel-row chip background (red family).
    - `--ft-info` — optional info indicator (blue family); add only if the chosen Playwright fixture exercises an `info`-severity warning.
    All chrome-tone, both light and dark themes; designed not to clash with `--ft-pin` / `--ft-highlight` from m-E21-06 or with `--ft-viz-amber`.

- **Topology indicator implementation hints:** for nodes, an SVG `<g>` overlay rendered from a `warningsByNodeId` derived map; position top-right of each topology node (or whatever stays out of the way of the metric label and the pin glyph). For edges, settle at start-milestone whether the chrome lives on the edge stroke (recoloured / dashed accent) or as a sibling glyph at the edge midpoint — record the choice in the tracking doc.

- **State-classifier helper:** pure function `classifyValidationState(input: { warnings: StateWarning[]; edgeWarnings: Record<string, StateWarning[]> }): 'empty' | 'issues'`. Empty when both `warnings.length === 0` and `Object.keys(edgeWarnings).length === 0`; otherwise `issues`. Vitest covers both branches.

- **Sort helper:** pure function `sortValidationItems(items: ValidationRow[]): ValidationRow[]` — severity descending (`error` > `warning` > `info` > unknown), then keyed identity (`nodeId` for node rows, `edgeId` for edge rows; empty / null sorts last), then `message`. Vitest covers ties at every level.

- **Severity-max collapse for indicators:** pure function `maxSeverityForKey(items: StateWarning[]): 'error' | 'warning' | 'info' | null`. Used identically for the per-node and per-edge severity-max computation. Vitest covers each combination plus the empty case.

- **Cross-link derivation helper:** pure function `rowsMatchingSelection(items: ValidationRow[], selection: { nodeId?: string; edgeId?: string } | null): Set<string>` — returns the row-id set the panel highlights for the current selection. Vitest covers nodeId-match, edge-match, neither-match, no-selection.

- **Survey-canary implication for fixtures:** all twelve shipped templates produce zero analyse-tier warnings today (`val-err == 0` is hard-asserted). Playwright fixtures for the "warnings present" scenarios must inject deliberately-broken inputs — either a fixture YAML that the analyser flags, or a contrived per-query analytical-warning trigger (e.g. retry-budget set to provoke a stationarity warning). Mechanism settles in the tracking doc at start-milestone.

- **Cross-reference for downstream UI:** m-E21-06's `viewState.selectedCell` cross-link convention is the model — workbench-card title turns turquoise when `selectedCell.nodeId` matches the card's `nodeId`. AC9's "click a row to pin and cross-highlight" reuses that exact convention and the existing pin proxies (`viewState.pinNode`, `workbench.toggle`). No new selection-state shape introduced for the contract; if one is needed during build it lives inline in `view-state.svelte.ts`.

## Out of Scope

- **Edit-time validation.** No live `POST /v1/validate` against an in-progress text edit. The Svelte UI has no editor; the expert-authoring epic owns this when it lands.
- **Tier-1 / tier-2 error surfacing.** Schema and compile errors reject the run at `POST /v1/run` with `400 { error }`; the user sees an HTTP error from the run attempt. Inventing a "validation failed before the run could start" UX is a separate milestone with its own backend decision (the live `POST /v1/validate` endpoint is available for that future work). Matches Blazor.
- **Per-tier filter / per-tier collapse / per-tier toggle.** Only tier-3 reaches this surface; per-tier UX is not meaningful.
- **Severity filter / severity-only-show toggle.** The list is sorted by severity; filtering is a follow-up after the surface has feedback.
- **Warning grouping by `code` or by node.** Items render flat in this milestone. Grouping is a follow-up.
- **Heatmap-row warning badges.** The widened type from AC1 carries the `edgeWarnings` map and this milestone consumes it for topology edge styling (AC8) and panel rows; the **heatmap row gutter does not get warning badges in this milestone.** This is the cheap follow-up the type widening enables — picked up in m-E21-08 polish or a dedicated follow-up.
- **Warning grouping in the panel by node / edge.** Flat list this milestone; grouping is a follow-up.
- **Validation history per run.** No "validation re-ran on `<date>`" timeline.
- **Re-run validation from the UI button.** No "re-validate" affordance. The surface is read; trigger is run.
- **Inline error-message rich rendering.** Markdown / link rendering inside messages is out — messages are plain text with whitespace preserved.
- **Validation diff between runs.** "These warnings are new since the previous run" is a real future ask but out of scope here.

## Open Questions

None. All Q1-Q6 plus the topology-indication and bidirectional-cross-link decisions are settled in *Settled scope* above. Implementation-note items (exact panel-column resize behaviour, exact filter-vs-highlight pixel behaviour for the bidirectional cross-link, exact edge-indicator visual treatment) are recorded in the tracking doc's Design Notes section at start-milestone, not as open questions on the spec.

## Dependencies

- **m-E21-06 Heatmap View** must be merged into `epic/E-21-svelte-workbench-and-analysis` before this milestone branches. The shared view-state store, the workbench-card cross-link convention (m-E21-06 AC12 / AC13), and the chrome-token additions (`--ft-pin`, `--ft-highlight`) are the seams this milestone composes against. The merge of `milestone/m-E21-06-heatmap-view` into the epic branch is handled at start-milestone time, not in this draft.
- **`GET /v1/runs/{runId}/state_window`** at `src/FlowTime.API/Program.cs:1028` — confirmed live; already called by `flowtime.getStateWindow(...)` in `ui/src/lib/api/flowtime.ts:109`. This milestone consumes the `warnings[]` and `edgeWarnings` fields that already arrive on the response.
- **`StateQueryService.BuildWarnings` + `BuildEdgeWarnings`** at `src/FlowTime.API/Services/StateQueryService.cs:4752,4783` — confirmed live; merges persisted manifest warnings (`RunManifest.Warnings`) with per-query analytical warnings, returns the unified list.
- **`RunManifest.Warnings`** (`src/FlowTime.Adapters.Synthetic/RunManifest.cs:16`) — `RunWarning[] { Code, Message, NodeId?, Bins?, Value?, Severity ("warning" default), EdgeIds? }` — the persisted shape `RunArtifactWriter` writes into `data/runs/<runId>/run.json` from `TimeMachineValidator` tier-3 output.
- **Wire DTO reference** — `TimeTravelStateWarningDto` (`src/FlowTime.UI/Services/TimeTravelApiModels.cs:521`) is the deserialization shape Blazor uses; Svelte's widened `StateWarning` type per AC1 mirrors this exactly.
- **E-23 Model Validation Consolidation** (closed and merged to main on 2026-04-26): provides the consolidated `ModelSchemaValidator` that `TimeMachineValidator` (tier-3 analyse) sits on top of. This milestone consumes its output indirectly via the persisted run-artifact warnings; no further E-23 work required.
- **Workbench / view-state stores** at `ui/src/lib/stores/workbench.svelte.ts` and `ui/src/lib/stores/view-state.svelte.ts` — pin-state and shared-view-state seams this milestone wires into via the existing `viewState.pinNode`, `viewState.selectedCell`, and `viewState.setSelectedCell` surface.

## ADRs

None at draft time. The Q2-Q6 resolutions plus topology-indication and bidirectional-cross-link decisions are UX/chrome decisions that do not warrant ADRs (they live in the tracking-doc Design Notes section per the m-E21-05 / m-E21-06 precedent). If a downstream milestone surfaces tier-1 / tier-2 errors via `POST /v1/validate`, that backend seam decision will warrant an ADR at that time.
