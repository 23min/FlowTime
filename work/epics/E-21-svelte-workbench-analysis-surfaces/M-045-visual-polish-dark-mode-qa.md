---
id: M-045
title: Visual Polish & Dark Mode QA
status: done
parent: E-21
---

**Created:** 2026-04-28
**Started:** 2026-04-28
**Completed:** 2026-04-28
## Goal

Close E-21 with a polish pass over the workbench and analysis surfaces: bring topology to the heatmap's accessibility bar (keyboard + ARIA), complete the bidirectional cross-link the M-043 / M-044 milestones started (node *and* edge selection now flows symmetrically between the panel, the cards, the topology DAG, and the heatmap), audit dark-mode + elevation tokens across every E-21 surface, fill the empty→populated flicker on `/time-travel/topology` and `/analysis` with skeletons, settle the transitions rule, and clean up two cheap follow-ups left in place by M-044. The milestone is the visual-polish + a11y gate before E-21 wraps; it ships no new analytical capability.

## Context

### What landed in the previous E-21 milestones

- **Workbench paradigm (M-038 / M-039):** density tokens, dag-map click/hover events, click-to-pin node + edge cards, metric chip bar, class filter, dark/light theme.
- **Analysis surfaces (M-040 → M-042):** `/analysis` route with sweep / sensitivity / goal-seek / optimize tabs; shared `AnalysisResultCard` + `ConvergenceChart`.
- **Heatmap view (M-043):** typed `<ViewSwitcher>`, shared view-state store (`view-state.svelte.ts`) with `selectedCell`, full-window 99p-clipped color scale, shared node-mode toggle, `--ft-pin` / `--ft-highlight` chrome tokens.
- **Validation surface (M-044):** widened `StateWarning` type, validation panel as left column inside the workbench panel, topology node + edge warning indicators, workbench-card warning dot + severity row tinting, validation-row click → pin + cross-highlight, FLIP card animations, `--ft-warn` / `--ft-err` / `--ft-info` chrome tokens.

### What is still open at epic-close

Three deferred items in `work/gaps.md` are explicitly tagged for this milestone:

1. **Topology DAG has no keyboard nav or ARIA structure** (`work/gaps.md` §"Topology DAG has no keyboard nav or ARIA structure"). The heatmap ships `role="grid"` / `role="gridcell"` with Tab + arrow-key reachability, focus ring, and `aria-label` containing `node id + bin + metric + value`. The topology DAG SVG (rendered via `{@html renderSVG(...)}` from dag-map) has no `tabindex`, no roles, and is mouse-only. Blazor's original topology had keyboard + ARIA — Svelte regressed against that bar. Without this retrofit the workbench cannot honestly claim a11y parity at epic close.

2. **Bidirectional card ↔ view selection** (`work/gaps.md` §"Bidirectional card ↔ view selection (reverse cross-link)"). M-044 partially closed this for nodes — the workbench card's body click already calls `viewState.setSelectedCell(pin.id, viewState.currentBin)` (`+page.svelte:774`) and the card title turns turquoise via the `selected` prop. **What is still missing for nodes:** the topology DAG has no `.node-selected` stroke rule. When `selectedCell.nodeId` is set, the card title is turquoise but the corresponding graph node renders no visual change — the cross-link is asymmetric. **For edges:** the cross-link does not exist at all. The edge card's `selected` prop is driven by a "last-pinned-only" heuristic (`edgeIdx === workbench.pinnedEdges.length - 1` at `+page.svelte:794`); clicking an edge card calls `workbench.bringEdgeToFront(...)` — a card-stack reordering, not a global selection — and does not light up the topology edge or the heatmap. The existing `.edge-selected` topology class fires on every pinned edge, conflating "pinned" with "selected." This milestone introduces a single `selectedEdge` concept that mirrors `selectedCell` and is driven symmetrically from edge cards, validation rows, and topology edge clicks.

3. **Data-viz palette colour-blind validation / pattern encoding** (`work/gaps.md` §"Data-viz palette not validated for color-blindness"). **Out of scope for this milestone** by user decision 2026-04-28 — the simulator pass and the `--ft-pattern-encode` toggle are deferred to a follow-up. Recorded here so the gap is not silently lost at epic close.

### Visual polish surfaces

- **Dark mode audit.** Every E-21 surface — workbench cards, validation panel + severity row tints, topology indicators (`--ft-warn` / `--ft-err` / `--ft-info` / `--ft-pin` / `--ft-highlight`), heatmap, sweep / sensitivity / goal-seek / optimize panels, run-selector dropdown — needs a single sweep in dark mode looking for token-resolution bugs, contrast issues, and severity-row tints reading correctly against the dark background. M-044 designed severity-row backgrounds to be transparent in dark mode; the audit confirms it works as designed and surfaces any other dark-mode breakage.
- **Loading skeletons.** Today, switching runs on `/time-travel/topology` and re-running sweep / sensitivity / goal-seek / optimize on `/analysis` produces an empty→populated flicker. The user reads this as "did it break?". Replace with `Skeleton` placeholders that match the eventual content geometry.
- **Transitions audit.** M-044 added FLIP animations on workbench card insert/remove (`animate:flip` at `+page.svelte:761,782`). The audit produces a written rule for what gets motion (cards, result swaps), what stays instant (view-switcher, context changes), and applies the rule consistently to the analysis-tab result swap and the run-selector dropdown content swap.
- **Elevation audit.** Quick pass over the shadow / border / layering tokens identifying any inconsistency across cards, panels, popovers, dropdowns; normalize to a single elevation scale.

### Cheap follow-ups

- **`validation-panel.svelte:157` no-op terminal collapse.** `{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}` is a no-op left in by an in-flight refactor; collapse to `{row.key}`. Flagged in M-044 wrap audit.
- **"Indicators absent on heatmap" Playwright assertion.** `tests/ui/specs/svelte-validation.spec.ts` does not currently assert that warning indicators are *absent* when `viewState.activeView === 'heatmap'`. The early-return guard at `+page.svelte:197` is exercised only indirectly today. Adding an explicit assertion tightens the contract for future surface authors.

### Why this milestone closes E-21

The E-21 epic spec lists eight milestones; M-038 through M-044 are merged. M-045 is the last item on the table. It is the "Visual Polish & Dark Mode QA" milestone in the epic spec, and the deferred-gap items above (topology a11y, full bidirectional cross-link) are the substantive work that makes the epic honest at close. Loading skeletons, transitions, elevation, dark-mode audit, and the two cheap follow-ups are the polish work the milestone name implies. After M-045 lands, E-21 wraps.

## Acceptance Criteria

1. **Topology keyboard navigation + ARIA structure (a11y parity with heatmap).** The topology DAG container exposes `role="application"` (or equivalent — settle in tracking doc) with an `aria-label` describing the surface as a topology graph. Each rendered node carries `tabindex="0"`, `role="button"`, and an `aria-label` containing at minimum the node id + class (when known) + the current metric value (matching the heatmap's `id + bin + metric + value` pattern from M-043). Tab + Shift-Tab walks the node set; Enter / Space activates pin (matching mouse click). A visible focus ring renders on the focused node — chrome-token, not data-viz, distinct from `--ft-pin` and `--ft-highlight`. Edges keep keyboard-reachability via the same scheme (`tabindex` + `role="button"` + `aria-label` "edge from X to Y") so click-to-pin-edge has a keyboard equivalent. Implementation may live in the topology Svelte component (post-render `$effect` injecting attributes onto the dag-map-emitted SVG) or in dag-map itself (library option) — settle in tracking doc; either is acceptable so long as the external contract is met.

2. **Topology node selection stroke rule (closes one-way cross-link for nodes).** A new `.node-selected` global CSS rule applies a chrome-token stroke to topology nodes when the node id matches `viewState.selectedCell?.nodeId`. Token: `--ft-highlight` (matches the existing card-title turquoise convention from M-043). A `$effect` in `+page.svelte` toggles the class on the dag-map-emitted node group whenever `viewState.selectedCell` changes, mirroring the existing `.edge-selected` effect at `+page.svelte:156-172`. Visible on Topology view; not rendered on Heatmap view (the dag-map SVG is unmounted there). Cross-link directions covered:
    - Click a topology node (existing path at `+page.svelte:127-141`) → node strokes turquoise + card title turquoise.
    - Click a workbench card body (existing path at `+page.svelte:774`) → node strokes turquoise on the topology DAG.
    - Click a heatmap cell (existing path) → node strokes turquoise on the topology DAG when the user switches to Topology view.
    - Click a validation panel row with `nodeId` (existing path from M-044) → node strokes turquoise on the topology DAG.

3. **Bidirectional edge cross-link via new `selectedEdge` field.** A new field `selectedEdge: { from: string; to: string } | null` on the shared view-state store, with `setSelectedEdge(from, to)` and `clearSelectedEdge()` setters. Semantics:
    - **Edge selection is independent of edge pinning.** A pinned edge is not necessarily selected; a selected edge is not necessarily pinned. Both can be true at once. Clicking a topology edge pins it (existing behaviour) *and* sets it as the `selectedEdge` (new behaviour).
    - **Topology rendering split into `pinned` and `selected` chrome.** The existing `.edge-selected` class (applied to every pinned edge today via `workbench.pinnedEdges`) is **renamed** to `.edge-pinned` with the same visual treatment it has today (`--ft-viz-amber` stroke). A new `.edge-selected` class with a distinct chrome treatment (settle in tracking doc — recommend `--ft-highlight` stroke + slightly heavier weight to distinguish from `.edge-pinned`) applies only to the single edge whose `from`/`to` matches `viewState.selectedEdge`. Both effects live side-by-side in `+page.svelte`; an edge can render both classes simultaneously (pinned + selected).
    - **Edge card `selected` prop driven from `selectedEdge`.** Replace the `edgeIdx === workbench.pinnedEdges.length - 1` heuristic at `+page.svelte:794` with `viewState.selectedEdge?.from === edge.from && viewState.selectedEdge?.to === edge.to`.
    - **Edge card body click sets the edge as selected.** Change `onSelect` at `+page.svelte:795` from `workbench.bringEdgeToFront(...)` to `viewState.setSelectedEdge(edge.from, edge.to)`. The `bringEdgeToFront` helper is preserved on the workbench store (other call sites may still rely on stack-ordering semantics) but is no longer wired to the edge-card body click.
    - **Validation panel edge rows wire `setSelectedEdge`.** When the user clicks a validation row whose identity is an edge (the `edgeWarnings`-keyed rows from M-044 AC9), the existing pin path is preserved *and* `viewState.setSelectedEdge(from, to)` is called — symmetric to the node path that already calls `setSelectedCell`. Edge rows whose edge id does not parse as `from→to` (rare; mismatched analyser output) preserve today's pin-only behaviour.
    - **Selection-clear on unpin.** When the selected edge is unpinned (via the edge card's ✕ button or any other unpin path), `clearSelectedEdge()` fires automatically — same shape as the M-043 `unpinAndClearSelection` helper for nodes.

4. **Dark-mode audit across all E-21 surfaces.** Run a single sweep over the workbench, analysis, validation, and chrome surfaces in dark mode looking for token-resolution bugs, contrast issues, and breakage of M-044's transparent severity-row backgrounds. Surfaces in scope:
    - `/time-travel/topology` — workbench cards, validation panel (rows + chips + severity tints), topology indicators (`--ft-warn` / `--ft-err` / `--ft-info` / `--ft-pin` / `--ft-highlight`), heatmap (cells, focus ring, selected overlay, fit-width toggle), run-selector dropdown, view-switcher, metric chip bar, class filter, timeline scrubber.
    - `/analysis` — sweep / sensitivity / goal-seek / optimize panels (config forms, result tables, charts), shared `AnalysisResultCard` + `ConvergenceChart` + `interval-bar-geometry` siblings.
    - `/run` and `/what-if` — confirmed not regressed (touch only if dark-mode breakage is found).
    
    Findings catalogued in the tracking doc; fixes land inline with the AC. Playwright dark-mode smoke spec asserts at minimum that (a) the topology page renders with `data-theme="dark"` (or whatever the theme attribute uses), (b) at least one severity-row tint reads correctly against the dark background, (c) at least one topology indicator dot is visible. Deeper visual regression is out of scope (no pixel-snapshotting in this milestone).

5. **Loading skeletons on workbench + analysis surfaces.** Replace the empty→populated flicker with `Skeleton` placeholders matching the eventual content geometry. Surfaces:
    - `/time-travel/topology`: while the `state_window` request for the selected run is in flight, the canvas region (topology *or* heatmap, whichever is active) renders a skeleton matching its eventual geometry; the workbench panel renders a row of skeleton cards proportional to the eventual pinned-card count (or a single placeholder if no pins yet).
    - `/analysis` sweep / sensitivity / goal-seek / optimize: while the corresponding compute call is in flight (`loading === true` already exists in the page state per `+page.svelte:86`), the result region renders a skeleton matching the eventual chart / table geometry. The "no result yet" empty state remains a separate concern (not a skeleton — matches current behaviour).
    - The `Skeleton` shadcn-svelte component is added under `ui/src/lib/components/ui/skeleton/` if not already shipped (verify at start-milestone; add if missing).
    - Vitest covers the loading-state classifier helper that decides skeleton-vs-content for each surface; Playwright smoke asserts a skeleton is present during a deliberately-slow run.

6. **Transitions rule applied across the workbench.** Audit and document a single transition rule in the tracking doc, then apply it consistently. Initial proposal:
    - **Card insert / remove:** 220 ms FLIP + 160 ms fade-in / 120 ms fade-out (already shipped in M-044; ratify here).
    - **`/analysis` result swap:** 160 ms cross-fade when re-running sweep / sensitivity / goal-seek / optimize replaces the previous result with a new one.
    - **Run-selector dropdown content swap:** 160 ms cross-fade.
    - **View-switcher topology ↔ heatmap:** **no transition** (context change; instant is correct).
    - **Validation row highlight on selection (M-044 AC10 cross-link):** **no transition** (selection feedback should be instant).
    
    The exact durations and easing land in the tracking doc; the contract is that the rule is documented and applied consistently to every result-swap surface. Playwright covers at minimum one cross-fade assertion (compute-finished → result-rendered) on `/analysis` to prove the transition is wired.

7. **Elevation audit + token normalization.** Single pass over the shadow / border / layering tokens used across the E-21 surfaces. Catalogue the current uses (border-only, `shadow-sm`, `shadow-md`, custom `box-shadow`, ring-1, …) in the tracking doc; pick a small canonical set (recommend: flat / `shadow-sm` for cards / `shadow-md` for popovers and dropdowns / no shadow on the topology canvas); apply consistently. Out of scope: introducing new elevation tokens; this is a normalization-to-existing pass.

8. **`validation-panel.svelte:157` cosmetic collapse.** Replace `{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}` with `{row.key}`. Verify no behavioural change via the existing M-044 vitest tests on the panel.

9. **"Indicators absent on heatmap" Playwright assertion.** Add an explicit assertion to `tests/ui/specs/svelte-validation.spec.ts` (or a sibling spec) confirming that when `viewState.activeView === 'heatmap'`, no `[data-warning-indicator]` elements are present in the canvas region. Closes the contract guard the M-044 wrap audit identified as covered indirectly today.

10. **Testing — Playwright + vitest, every reachable branch covered.** Per the project's UI-testing hard rule. **Vitest:**
    - Topology a11y attribute helper (AC1) — node label assembly, edge label assembly, fallback when class / metric is unknown.
    - View-state `setSelectedEdge` / `clearSelectedEdge` — set, replace, clear, no-op-when-already-cleared, equality on existing selection.
    - Bidirectional matchers — "is this edge currently selected" (`isSelectedEdge(viewState, from, to)`) helper; covers both null and present cases.
    - Loading-state classifier (AC5) — skeleton-vs-content branch per surface.
    
    **Playwright** (extending or siblinging existing E-21 specs; graceful-skip on dev-server / API unavailability):
    - **AC1** — topology node receives focus via Tab; Enter pins the node.
    - **AC1** — focus ring visible on focused node (data attribute or computed style assertion).
    - **AC2** — clicking a workbench card body strokes the matching topology node turquoise (`.node-selected` present).
    - **AC2** — clicking a heatmap cell, then switching to Topology view, shows the matching node strokes turquoise.
    - **AC3** — clicking an edge card body sets `viewState.selectedEdge` and the topology edge gains `.edge-selected` chrome distinct from `.edge-pinned`.
    - **AC3** — clicking an edge-attributed validation row pins the edge, sets `selectedEdge`, edge card and topology edge both light up; unpin clears `selectedEdge`.
    - **AC4** — dark-mode smoke: topology page renders, severity tints + indicator dots are visible.
    - **AC5** — `/analysis` sweep run: skeleton visible during compute, result visible after.
    - **AC5** — `/time-travel/topology` run-switch: skeleton visible during `state_window` load.
    - **AC6** — `/analysis` re-run cross-fade asserted via element-level transition state.
    - **AC9** — heatmap view shows zero `[data-warning-indicator]` elements (explicit assertion).
    
    A line-by-line branch audit of the new UI + helpers against tests is recorded in the tracking doc's Coverage Notes section, matching the M-042 / M-043 / M-044 audit structure.

## Constraints

- **No new analytical capability.** This is a polish milestone. No new analysis modes, no new data fields on responses, no new endpoints. AC3 introduces a new field on the *client-side* shared store (`selectedEdge`) but does not change any wire shape.
- **No regression on what landed.** What-if (`/what-if`), run orchestration (`/run`), heatmap (`/time-travel/topology` heatmap view), validation panel, sweep / sensitivity / goal-seek / optimize must all stay green. Playwright suite must continue to pass.
- **Chrome tokens, not data-viz tokens.** A11y focus ring, `.node-selected` stroke, `.edge-selected` (the new selection variant) all use chrome-scale tokens (`--ft-highlight` already exists; introduce new ones only if a clean visual demands it). Data-viz hues stay reserved for metric encoding.
- **Library-versus-component placement.** Where dag-map a11y attributes belong (library option vs post-render component injection) is settled in the tracking doc, not here. Both are acceptable; library is preferable if the change stays general-purpose per ADR-E21-02.
- **`bringEdgeToFront` is preserved as a workbench helper.** Only its wiring to the edge-card body click is changed. Future call sites (e.g. a "show this edge most recently inspected" mechanic) remain available.
- **No pixel-snapshot regression suite.** The dark-mode audit is human + smoke-spec-verified; introducing a pixel-snapshot harness is out of scope for this milestone.
- **Color-blind validation + pattern encoding deferred.** Per user decision 2026-04-28; remains an open gap in `work/gaps.md` and a candidate for a follow-up after E-21 wraps.

## Design Notes

- **Topology a11y placement.** The topology Svelte component already runs a post-render `$effect` that injects warning-indicator SVG elements (`+page.svelte:188-303`). Adding a parallel post-render pass that walks `[data-node-id]` and `[data-edge]` and applies `tabindex` / `role` / `aria-label` is the lowest-friction path. Library-side support (a `renderSVG` option that emits these attributes natively) is also acceptable and preferable per ADR-E21-02 if dag-map maintenance bandwidth allows; settle at start-milestone.
- **Focus management for topology keyboard nav.** The simplest model: nodes are in DOM order (dag-map's render order); Tab and Shift-Tab walk that order; the browser's built-in focus management does the rest. A more sophisticated model would walk topologically (sources first, sinks last) — out of scope for this milestone unless the simple model proves disorienting in user testing.
- **`.node-selected` stroke style.** `stroke: var(--ft-highlight); stroke-width: 2; fill-opacity: 1;` — same hue as the workbench-card title cross-highlight, mirrors the M-043 convention. Avoid changing the node fill; the metric color stays the encoding signal, the stroke is the chrome signal.
- **`.edge-selected` (new) vs `.edge-pinned` (renamed) chrome.** Recommend:
    - `.edge-pinned`: `stroke: var(--ft-viz-amber); stroke-width: 3;` (current `.edge-selected` treatment).
    - `.edge-selected`: `stroke: var(--ft-highlight); stroke-width: 4; stroke-dasharray: 0;` — distinct hue and slightly heavier so the user can distinguish "I have this edge pinned" from "this is my current edge selection" when both are present.
    Settled at start-milestone; locked in tracking doc.
- **Edge identity normalization.** `selectedEdge` uses the `{from, to}` shape (matches the workbench's `pinnedEdges` shape and avoids the string-key parsing edge cases). The `edgeWarnings` map keys remain string-form (`from→to`), and the validation row → `setSelectedEdge` path normalizes by parsing on the arrow boundary (already done in M-044 at `+page.svelte:262-265`).
- **Loading-skeleton component.** shadcn-svelte's `Skeleton` is a thin `<div>` with a pulse animation and a configurable shape. If the component is not yet vendored under `ui/src/lib/components/ui/skeleton/`, add it (one-file shadcn add) at start-milestone; do not invent a custom skeleton.
- **Transitions library.** Svelte's built-in `transition:fade` and `animate:flip` cover every requirement here. No additional motion library is needed.
- **Elevation token candidates.** Today's ad-hoc set: `border` only (validation panel), `shadow-sm` (workbench cards), implicit shadcn shadows (dropdown). Recommend the canonical set in AC7 above; settle in tracking doc.
- **Dark mode audit method.** Manual visual sweep in the devcontainer with the theme toggle, page-by-page screenshot capture (not pixel-snapshotted; just for the tracking doc's findings table), Playwright smoke spec covering the highest-risk surfaces (topology indicators, validation severity tints).

## Out of Scope

- **Color-blind validation + pattern encoding** (`--ft-pattern-encode` toggle, simulator pass). Deferred to a follow-up by user decision 2026-04-28.
- **Heatmap sliding-window scrubber.** Tracked in `work/gaps.md` §"Heatmap sliding-window scrubber"; needs its own milestone.
- **Topological focus order on topology Tab walk.** Simple DOM-order Tab walk in this milestone; topological order is a follow-up if the simple model proves disorienting.
- **Pixel-snapshot regression suite for visual changes.** Manual + smoke-spec only; introducing a pixel-snapshot harness is a separate decision.
- **New chrome tokens beyond what AC3 settles for `.edge-selected`.** No new token surface; reuse `--ft-highlight` and existing chrome scale.
- **Mobile / responsive layout audit.** Out of E-21 scope per epic spec.
- **Heatmap-row warning badges.** M-044 follow-up; not this milestone.
- **Decomposition / comparison / flow-balance views.** Not E-21 scope.
- **Editor-time validation surface.** Not E-21 scope; expert-authoring epic.

## Dependencies

- **M-043 Heatmap View** — provides the shared view-state store (`view-state.svelte.ts`) that gains the `selectedEdge` field per AC3, the `selectedCell` field that AC2 reads, and the chrome-token convention (`--ft-pin`, `--ft-highlight`) that AC2 reuses.
- **M-044 Validation Surface** — provides the validation panel, the severity-row click path that AC3 extends with `setSelectedEdge` for edge rows, the `--ft-warn` / `--ft-err` / `--ft-info` chrome tokens that AC4 audits, and the workbench-card severity dot + FLIP card animation that AC6 ratifies.
- **dag-map library** at `ui/dag-map/` — AC1's a11y retrofit may add a `renderSVG` option for emitting `tabindex` / `role` / `aria-label` natively. If chosen as the implementation path, the library change must remain general-purpose per ADR-E21-02.
- **shadcn-svelte `Skeleton`** — AC5 requires this component under `ui/src/lib/components/ui/skeleton/`. Add at start-milestone if not already vendored.

## ADRs

None at draft time. AC1 placement (library vs post-render component) is settled in the tracking doc, not as an ADR — both options are within ADR-E21-02's general-purpose-library spirit. AC3's `.edge-pinned` rename + new `.edge-selected` semantics is a chrome-token decision recorded in tracking-doc Design Notes per the M-042 / M-043 / M-044 precedent.
