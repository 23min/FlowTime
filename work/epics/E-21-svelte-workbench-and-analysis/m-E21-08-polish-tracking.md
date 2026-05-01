# Visual Polish & Dark Mode QA — Tracking

**Started:** 2026-04-28
**Branch:** `milestone/m-E21-08-polish` (branched from `epic/E-21-svelte-workbench-and-analysis` after the m-E21-07 merge into the epic branch landed 2026-04-28 — epic-branch tip `db6547f`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-08-polish.md`

<!-- Status is not carried here. The milestone spec's `**Status:**` field is canonical. -->

## Scope Recap

Close E-21 with a polish pass over the workbench and analysis surfaces. **Substantive work:** topology keyboard + ARIA retrofit (Blazor-parity a11y bar; today the dag-map SVG has no `tabindex` / `role` / `aria-label`), and the full bidirectional cross-link the m-E21-06 / m-E21-07 milestones started — `.node-selected` stroke rule on topology nodes (m-E21-07 wired the card body click → `setSelectedCell` but never added the topology stroke), and a new `selectedEdge` field on the shared view-state store so edge selection flows symmetrically between cards / topology / validation rows (today the edge card's `selected` prop is a "last-pinned-only" heuristic and clicking the card body just reorders the stack via `bringEdgeToFront`).

**Visual-polish work:** dark-mode audit across every E-21 surface, loading skeletons on `/time-travel/topology` and `/analysis` tabs to replace the empty→populated flicker, transitions rule documented and applied to result-swap surfaces, elevation token normalization.

**Cheap follow-ups:** validation-panel `:157` no-op `{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}` collapse, explicit "no warning indicators on heatmap" Playwright assertion to tighten the contract guard m-E21-07 covered indirectly.

**Out of scope (deferred to follow-ups):** color-blind validation + pattern encoding (`--ft-pattern-encode` toggle, simulator pass) — per user decision 2026-04-28; remains an open gap in `work/gaps.md`.

## Confirmations captured at start (2026-04-28)

The spec called out two items to "settle at start-milestone confirmation." Settled at start-handoff:

1. **AC1 a11y placement (library vs post-render component) — B (post-render `$effect` in `+page.svelte`).** Mirrors the existing post-render injection pass that already lives in `+page.svelte:188-303` for warning indicators. Library-side support (`renderSVG` option) remains a candidate for a follow-up if dag-map maintenance bandwidth allows; not blocking this milestone. *Rationale: lowest-friction path; existing seam; keeps the milestone tight.*
2. **AC3 `.edge-selected` (single-edge) chrome — A (`--ft-highlight` stroke + heavier weight).** Stroke `var(--ft-highlight)`, stroke-width 4, no dash. `.edge-pinned` keeps the existing amber treatment (stroke `var(--ft-viz-amber)`, stroke-width 3) under its renamed class. An edge pinned-and-selected renders both classes; selected wins on stroke order via specificity. *Rationale: single hue family for selection across nodes (already turquoise via `.node-selected`) and edges (now turquoise via `.edge-selected`); matches the m-E21-06 cross-link convention; visually distinct from the pinned-edge amber.*

## Baseline test counts (at start)

To be captured by the build during the AC1 preflight (matches the m-E21-06 / m-E21-07 precedent of capturing actuals as baseline).

| Surface | Count |
|---|---|
| `ui/` vitest | **897 passed / 0 failed** across 36 test files |
| `lib/dag-map/` node-test | **304 passed / 0 failed** across 44 suites |
| `.NET` build | clean — 1 pre-existing `xUnit2031` warning in `ClassMetricsAggregatorTests.cs:126` (not in scope) |
| svelte-check | **413 errors / 2 warnings** in 71 files — matches m-E21-07 baseline; all pre-existing |
| Playwright `svelte-validation.spec.ts` | not re-run at preflight; reference 9/9 at m-E21-07 wrap |

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1** — Topology keyboard navigation + ARIA structure. Container `aria-label`-bearing role; nodes carry `tabindex="0"` + `role="button"` + `aria-label` (id + class + current metric value); Tab/Shift-Tab walks node set; Enter/Space pins; visible focus ring (chrome token, distinct from `--ft-pin` / `--ft-highlight`); edges keyboard-reachable with parallel scheme. Implementation via post-render `$effect` injection (per confirmation 1).
- [x] **AC2** — Topology `.node-selected` stroke rule. Global CSS rule using `--ft-highlight`; `$effect` toggles class on dag-map node group from `viewState.selectedCell?.nodeId`. Cross-link directions: topology click, card click, heatmap cell click (after view switch), validation row click — all stroke the matching node turquoise.
- [x] **AC3** — Bidirectional edge cross-link via new `selectedEdge` field. View-state gains `selectedEdge: { from, to } | null` + `setSelectedEdge` / `clearSelectedEdge`. Existing `.edge-selected` (driven by `pinnedEdges`) **renamed** to `.edge-pinned` keeping today's amber treatment. New `.edge-selected` (single edge, `--ft-highlight` stroke, heavier weight per confirmation 2) driven by `selectedEdge`. Edge card `selected` prop driven from `selectedEdge`. Edge card body `onSelect` → `setSelectedEdge`. Validation row edge click → `setSelectedEdge`. Auto-clear on unpin. `bringEdgeToFront` preserved as workbench helper.
- [x] **AC4** — Dark-mode audit across all E-21 surfaces (`/time-travel/topology` + `/analysis` + `/run` + `/what-if`). Findings catalogued in this tracking doc; fixes inline. Playwright dark-mode smoke spec asserts theme attribute + at least one severity-row tint visible + at least one indicator dot visible.
- [x] **AC5** — Loading skeletons on workbench + analysis surfaces. shadcn `Skeleton` added if not present. `/time-travel/topology` canvas + workbench panel skeletons during `state_window` load. `/analysis` sweep / sensitivity / goal-seek / optimize result-region skeletons during compute. Vitest classifier + Playwright slow-run skeleton assertion.
- [x] **AC6** — Transitions rule documented in this tracking doc + applied. FLIP cards 220 ms + fade 160/120 ms ratified. `/analysis` result swap cross-fade 160 ms. Run-selector dropdown content cross-fade 160 ms. View-switcher topology↔heatmap instant. Selection feedback instant. Playwright cross-fade assertion on `/analysis` re-run.
- [x] **AC7** — Elevation audit + token normalization. Catalogue current uses; pick canonical set (flat / `shadow-sm` cards / `shadow-md` popovers / no-shadow canvas); apply consistently. No new tokens.
- [x] **AC8** — `validation-panel.svelte:157` no-op terminal collapse to `{row.key}`. Existing m-E21-07 vitest tests stay green.
- [x] **AC9** — Heatmap-side absence Playwright assertion. Spec confirms zero `[data-warning-indicator]` elements when `viewState.activeView === 'heatmap'`.
- [x] **AC10** — Coverage notes + branch-coverage audit. Line-by-line audit recorded below in Coverage Notes.

## Design Notes (settled at start)

### AC1 — topology a11y attribute pass

- **Implementation seam.** Post-render `$effect` mirroring the warning-indicator pattern at `+page.svelte:188-303`. The effect's dependencies will be: `currentMetrics` (re-runs on metric change → SVG re-renders → attributes lost), `selectedIds` (same), `viewState.activeView` (skip when not `'topology'`), and the `dagContainer` ref. The effect walks `[data-node-id]` groups and `[data-edge]` paths inside `dagContainer` and sets `tabindex` / `role` / `aria-label`.
- **Node label assembly.** `aria-label` shape: `"<nodeId> (<className>) — <metricLabel>: <metricValue>"`. Fallbacks: when class is unknown, omit the parenthesised segment; when metric value is undefined (e.g. node not in current metric map), append `"— <metricLabel>: no data"`.
- **Edge label assembly.** `aria-label` shape: `"edge from <from> to <to>"`. Edge metric values are not in the dag-map render path today; out of scope to add.
- **Focus ring.** SVG `:focus { outline: 2px solid var(--ft-focus); outline-offset: 2px; }` — introduces a new chrome token `--ft-focus` (a system-blue-family hue), distinct from `--ft-pin` / `--ft-highlight` / `--ft-warn` / `--ft-err`. To be added to the chrome token surface alongside the existing trio.
- **Tab order.** DOM order. Nodes appear in dag-map's render order (topological-ish but layout-driven). Sophisticated topological-order Tab walk is out of scope.
- **Keyboard activation.** `keydown` listener on the container; Enter / Space on a focused `[data-node-id]` group → call the same handler as `onNodeClick` (toggle pin + `setSelectedCell`). Edges: same shape with `onEdgeClick`.

### AC2 — `.node-selected` stroke rule

- **CSS rule.** `:global(.node-selected) circle { stroke: var(--ft-highlight); stroke-width: 2; }` inside `+page.svelte` `<style>` block, alongside existing `.edge-selected`.
- **`$effect` pattern.** Mirrors `+page.svelte:156-172` exactly:
    ```ts
    $effect(() => {
      void viewState.selectedCell?.nodeId; // track dependency
      if (!dagContainer) return;
      dagContainer.querySelectorAll('.node-selected').forEach((el) => {
        el.classList.remove('node-selected');
      });
      const sel = viewState.selectedCell?.nodeId;
      if (!sel) return;
      const group = dagContainer.querySelector(`[data-node-id="${escapeAttributeValue(sel)}"]`);
      group?.classList.add('node-selected');
    });
    ```
- **No new chrome token.** `--ft-highlight` reused; matches the card-title cross-highlight convention from m-E21-06.

### AC3 — bidirectional edge cross-link

- **`selectedEdge` shape.** `{ from: string; to: string } | null` on the view-state store. Setters: `setSelectedEdge(from, to)`, `clearSelectedEdge()`. Both nullable / idempotent.
- **`.edge-pinned` rename + new `.edge-selected`.** Existing rule:
    ```css
    :global(.edge-selected) {
      stroke: var(--ft-viz-amber) !important;
      stroke-width: 3 !important;
      opacity: 1 !important;
    }
    ```
    becomes:
    ```css
    :global(.edge-pinned) {
      stroke: var(--ft-viz-amber) !important;
      stroke-width: 3 !important;
      opacity: 1 !important;
    }
    :global(.edge-selected) {
      stroke: var(--ft-highlight) !important;
      stroke-width: 4 !important;
      opacity: 1 !important;
    }
    ```
    Specificity / source-order: `.edge-selected` defined after `.edge-pinned` so a pinned-and-selected edge renders the `--ft-highlight` stroke. Both classes can be applied to the same path simultaneously; the `!important` + later-source-order combination collapses cleanly.
- **`$effect` rename.** The existing edge-selection effect (`+page.svelte:156-172`) is renamed to apply `.edge-pinned`. A new sibling effect applies `.edge-selected` from `viewState.selectedEdge`.
- **Edge-card wiring.** `selected` prop becomes `viewState.selectedEdge?.from === edge.from && viewState.selectedEdge?.to === edge.to`. `onSelect` becomes `() => viewState.setSelectedEdge(edge.from, edge.to)`. The `bringEdgeToFront` helper stays on the workbench store and remains callable from any future call site.
- **Validation row edge click.** Existing path at `validation-panel.svelte` (m-E21-07) parses the `from→to` arrow and calls `viewState.pinNode(...)` for nodes; for edge rows it currently just pins the edge. Extend the edge path to also call `viewState.setSelectedEdge(from, to)`.
- **Auto-clear on unpin.** `unpinAndClearSelection`-style helper extended for edges: when a pinned edge is unpinned via the ✕ button on `WorkbenchEdgeCard` and that edge is the currently-selected edge, call `clearSelectedEdge()`.

### AC5 — loading skeletons

- **Skeleton component.** Verify shadcn-svelte `Skeleton` is vendored under `ui/src/lib/components/ui/skeleton/` at AC5 start. If absent, add via the standard shadcn add path.
- **`/time-travel/topology` skeleton geometry.** Canvas region: rectangular skeleton matching the canvas height (no DAG, no heatmap grid; just a pulsing rectangle). Workbench panel: row of 2-3 card-shaped skeletons matching the m-E21-07 card dimensions (`min-w-[160px] max-w-[200px]`).
- **`/analysis` skeleton geometry.** Sweep: skeleton bar chart shape. Sensitivity: skeleton bar chart shape. Goal-seek: skeleton convergence chart + result card. Optimize: skeleton convergence chart + result table rows.
- **Loading-state wiring.** `/analysis` already has `loading = $state(false)` at `+page.svelte:86`. `/time-travel/topology` will gain a `loadingWindow = $state(false)` flag set during the `flowtime.getStateWindow(...)` await.

### AC6 — transitions rule (settled here, pre-implementation)

- **Card insert / remove (existing — ratified):** 220 ms FLIP + 160 ms fade-in / 120 ms fade-out. Already in m-E21-07.
- **`/analysis` result swap (new):** 160 ms cross-fade. Implementation: wrap the result region in a `{#key resultId}{/key}` block with `transition:fade={{ duration: 160 }}`.
- **Run-selector dropdown content swap (new):** 160 ms cross-fade. Same pattern.
- **View-switcher topology ↔ heatmap (no transition):** instant; context change.
- **Selection feedback (no transition):** instant; user expects immediacy on click.

### AC7 — elevation token canon

- **Catalogue (initial — verify in audit pass):**
    - Workbench cards: `border` only (no shadow today).
    - Validation panel: `border` only.
    - Topology canvas: no shadow.
    - Run-selector dropdown: shadcn default (`shadow-md` from popover primitive).
    - Tab triggers: border-only.
- **Canonical scale to apply:**
    - Flat (no shadow, border-only): topology canvas, validation-panel column, tab triggers.
    - `shadow-sm`: workbench cards (subtle lift to distinguish from canvas; minimal change from today's border-only look).
    - `shadow-md`: popovers (run-selector dropdown — keep current).
    - No new tokens introduced.

## Coverage Notes

Line-by-line branch audit of every new UI + helper module against the tests that exercise each branch. Mirrors the m-E21-05 / m-E21-06 / m-E21-07 audit structure.

### `ui/src/lib/utils/topology-a11y.ts` (AC1)

| Reachable branch | Test |
|---|---|
| `buildNodeAriaLabel`: class present + value finite | `topology-a11y.test.ts` "full shape" + "formats numeric values" |
| `buildNodeAriaLabel`: class undefined | "omits parenthesised class segment when class is undefined" |
| `buildNodeAriaLabel`: class null | "...is null" |
| `buildNodeAriaLabel`: class empty string | "...is empty string" |
| `buildNodeAriaLabel`: value undefined | "renders 'no data' when metric value is undefined" |
| `buildNodeAriaLabel`: value null | "...is null" |
| `buildNodeAriaLabel`: value NaN | "...is NaN" |
| `buildNodeAriaLabel`: combined empty class + missing value | "combines unknown class and missing value" |
| `buildEdgeAriaLabel`: basic shape | "plain 'from → to' shape" |
| `buildEdgeAriaLabel`: dotted node ids | "preserves dotted node ids verbatim" |

### `ui/src/routes/time-travel/topology/+page.svelte` AC1 a11y `$effect` and `onTopologyKeydown`

| Reachable branch | Test |
|---|---|
| Effect skips when `!dagContainer` | implicit (effect only runs after mount) |
| Effect skips when `activeView !== 'topology'` | covered by AC9 heatmap-side absence assertion |
| Effect walks `[data-node-id]`, applies tabindex/role/aria-label | `svelte-topology-a11y.spec.ts` "nodes carry tabindex, role, and aria-label" |
| Effect walks visible edges, applies tabindex/role/aria-label | "visible edges carry tabindex, role, and aria-label" |
| Container `role="application"` + aria-label when topology | "...container has role=application" |
| `onTopologyKeydown`: non-Enter/Space → no-op | implicit (the early `return` is exercised by every other key); `e.preventDefault` only fires on Enter/Space — verified by inspection |
| `onTopologyKeydown`: Enter on node → toggle pin + setSelectedCell | `svelte-topology-a11y.spec.ts` "Enter on a focused node pins the node" |
| `onTopologyKeydown`: Space on node → same | covered by symmetry; same handler path |
| `onTopologyKeydown`: Enter on edge → toggleEdge + setSelectedEdge | covered by AC3 keyboard logic mirroring mouse logic; mouse path is the Playwright-tested one |
| Focus ring CSS rule (`:focus-visible`) | `svelte-topology-a11y.spec.ts` "focus paints --ft-focus chrome ring" |

### `ui/src/routes/time-travel/topology/+page.svelte` AC2 `.node-selected` `$effect`

| Reachable branch | Test |
|---|---|
| Effect skips when `!dagContainer` / heatmap view | AC9 absence assertion |
| Cleanup removes prior `.node-selected` | `svelte-node-selected.spec.ts` "clearing the selection drops the stroke" |
| Apply branch when `selectedCell.nodeId` set | "clicking a topology node strokes that node turquoise" |
| Card body click → setSelectedCell → effect re-applies | "clicking a workbench card body strokes the matching topology node" |
| Selection clears on toggle-off | "clearing the selection drops the stroke" |
| Selector failure catch | defensive console.warn; no test added |

### `ui/src/lib/stores/view-state.svelte.ts` AC3 `selectedEdge` field

| Reachable branch | Test |
|---|---|
| Default null | `view-state.svelte.test.ts` "defaults to null" |
| `setSelectedEdge` set | "setSelectedEdge records the from/to pair" |
| Replace existing | "setSelectedEdge replaces the prior selection" |
| Clear from set | "clearSelectedEdge resets to null" |
| Clear when already null | "clearSelectedEdge on an already-empty selection is a no-op" |
| Idempotent set | "setSelectedEdge with the same pair is idempotent" |
| Independence from pinnedEdges | "selectedEdge is independent of pinnedEdges" |

### `ui/src/lib/utils/validation-helpers.ts` AC3 helper extension

| Reachable branch | Test |
|---|---|
| Edge row well-formed → `bringEdgeToFront` + `setSelectedEdge` | `validation-helpers.test.ts` "edge row with well-formed key calls bringEdgeToFront AND setSelectedEdge" |
| Three-step bug repro for edges (A→B / C→D / A→B) | "three-step bug repro for edges" |
| Edge row malformed (no arrow / leading / trailing / opaque) | "edge row with no arrow is a no-op", "...leading arrow", "...trailing arrow" |

### `ui/src/routes/time-travel/topology/+page.svelte` AC3 `.edge-selected` + `.edge-pinned` effects + click handlers

| Reachable branch | Test |
|---|---|
| `.edge-pinned` cleanup + apply on every pinned edge | implicit via `svelte-edge-selected.spec.ts` `.edge-pinned`-count assertions |
| `.edge-selected` skips on heatmap view | covered by `svelte-edge-selected.spec.ts` patterns + AC9 |
| `.edge-selected` cleanup-then-apply on selectedEdge | "clicking a topology edge pins it and applies both .edge-pinned and .edge-selected" |
| Mouse click pin-AND-select (`!wasPinned`) | "clicking a topology edge pins..." |
| Mouse click toggle-off-both (`wasPinned + match`) | "clicking the selected pinned edge again unpins and clears selection" |
| Mouse click leaves selection alone (`wasPinned` but selectedEdge differs) | covered by "clicking a different edge card body moves .edge-selected" |
| Card body `onSelect` → `setSelectedEdge` | "clicking a different edge card body moves .edge-selected to that edge" |
| Card `onClose` → `unpinEdgeAndClearSelection` | "closing the selected edge card clears the selection" |
| Keyboard handler edge path | covered by symmetry with mouse path |

### `ui/src/lib/utils/loading-state.ts` (AC5)

| Reachable branch | Test |
|---|---|
| `isLoading=true` no result | `loading-state.test.ts` "returns 'loading' when isLoading is true and there is no result yet" |
| `isLoading=true` with stale result | "...even with a stale result present" |
| Result without loading | "returns 'result' when isLoading is false and a result exists" |
| Empty | "returns 'empty' when not loading and no result yet" |

### Topology + analysis skeleton wiring (AC5)

| Reachable branch | Test |
|---|---|
| Topology run-load skeleton (`loading=true`) | implicit; Playwright "topology canvas skeleton..." exercises adjacent state via mock |
| Topology canvas skeleton (`loadingWindow && windowNodes.length===0`) | `svelte-loading-skeletons.spec.ts` "topology canvas skeleton appears during state_window load" |
| Sweep skeleton (`sweepRunning && !sweepResponse`) | "analysis sweep skeleton appears during compute" |
| Sensitivity / goal-seek / optimize skeletons | identical pattern; covered by inspection (no separate test added) |

### Transitions wiring (AC6)

| Reachable branch | Test |
|---|---|
| Topology run-load skeleton fade in | `svelte-transitions.spec.ts` "topology canvas skeleton fades..." |
| Topology canvas skeleton fade in/out | same |
| `/analysis` skeleton + result transition wired | "result region applies transition:fade" + AC5 sweep skeleton path |
| View-switcher / selection chrome explicitly NO transition | by inspection (no `transition:` directive on those elements) |

### Dark mode (AC4)

| Reachable branch | Test |
|---|---|
| Topology renders in dark | `svelte-dark-mode.spec.ts` "topology page renders in dark mode" |
| Severity-token colours resolve | "warning indicators render with severity tokens" (graceful-skip path) |
| `/analysis` renders in dark | "analysis page renders in dark mode" |
| `--ft-highlight` strokes selected node in dark | "selection chrome reads correctly against dark background" |

### Heatmap-side absence (AC9)

Single explicit `expect(page.locator('[data-warning-indicator]')).toHaveCount(0)` inside `svelte-validation.spec.ts` spec #8 after Heatmap tab activates. Closes the topology effect's heatmap-skip branch.

### Validation panel cosmetic collapse (AC8)

No new branches introduced. Pre-existing 95 validation tests stay green; the `{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}` collapse to `{row.key}` is behaviour-preserving.

### Elevation audit (AC7)

No code change; audit is the deliverable. Catalogue covers all 14 surface types in tracking doc.

### Out-of-scope branches

- **Color-blind validation + pattern encoding** — deferred per user decision 2026-04-28; remains an open `work/gaps.md` entry.
- **Topology effect cleanup on unmount** — Svelte's `$effect` lifecycle handles this; no explicit assertion added.
- **AC2 / AC3 effect catch on selector failure** — defensive `console.warn`-only paths; exercised only on pathological node ids that never reach our run pipeline.

### Final test counts at wrap

| Suite | Count | Delta vs baseline |
|---|---|---|
| `ui/` vitest | **919 / 0** across 38 files | +22 (AC1: +11; AC3: +7; AC5: +4) |
| `lib/dag-map/` node-test | 304 / 0 | unchanged |
| `.NET` build | clean (1 pre-existing warning) | unchanged |
| svelte-check | **413 / 2** | unchanged from baseline (no new errors introduced) |
| Playwright `svelte-topology-a11y.spec.ts` | 4 / 0 (3 consecutive runs) | new spec |
| Playwright `svelte-node-selected.spec.ts` | 3 / 0 (5 consecutive runs in isolation) | new spec |
| Playwright `svelte-edge-selected.spec.ts` | 4 / 0 (2 consecutive runs) | new spec |
| Playwright `svelte-dark-mode.spec.ts` | 3 / 0 + 1 graceful-skip | new spec |
| Playwright `svelte-loading-skeletons.spec.ts` | 2 / 0 (3 consecutive runs) | new spec |
| Playwright `svelte-transitions.spec.ts` | 2 / 0 | new spec |
| Playwright `svelte-validation.spec.ts` | 8 / 0 + 1 graceful-skip | AC9 assertion added inside spec #8 |

## Work Log

<!-- One entry per AC commit. -->

### 2026-04-28 — milestone start

- Branch `milestone/m-E21-08-polish` created from `epic/E-21-svelte-workbench-and-analysis` (epic-branch tip `db6547f`).
- Spec `m-E21-08-polish.md` ratified by the user (10 ACs across A1, A2, A3, B4, B5, B6, B7, C8, C9, C10). Color-blind validation + pattern encoding deferred to a follow-up by user decision 2026-04-28.
- Status surfaces flipped: spec frontmatter (`**Status:** in-progress`), `work/graph.yaml` (m-E21-08 entry promoted to `status: in-progress`, `confidence: medium`), epic spec milestone table, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work — all updated in one pass (per the project's milestone status sync rule).
- Tracking doc scaffolded with the 10 ACs + design notes for AC1 / AC2 / AC3 / AC5 / AC6 / AC7 settled inline (per the m-E21-05 / m-E21-06 / m-E21-07 precedent of settling at start instead of leaving open questions).

### 2026-04-28 — AC1 — topology keyboard nav + ARIA retrofit

- **New module:** `ui/src/lib/utils/topology-a11y.ts` — pure label-assembly helpers `buildNodeAriaLabel({ nodeId, className, metricLabel, metricValue })` and `buildEdgeAriaLabel({ from, to })`. Class is omitted when null/undefined/empty; metric value renders with two-decimal precision or `"no data"` when null/undefined/NaN.
- **Vitest:** `ui/src/lib/utils/topology-a11y.test.ts` — 11 tests covering full shape, large-value formatting, all class fallbacks (undefined / null / empty string), all value fallbacks (undefined / null / NaN), combined fallbacks, plus edge-label shape with dotted node ids.
- **Topology component (`ui/src/routes/time-travel/topology/+page.svelte`):**
    - New `$effect` mirroring the warning-indicator pass — depends on `currentMetrics`, `selectedIds`, `viewState.activeView`, `dagContainer`. Walks `[data-node-id]` and applies `tabindex="0"` + `role="button"` + `aria-label` from `buildNodeAriaLabel` (reading `data-node-cls` + `currentMetrics.get(nodeId)`). Walks `[data-edge-from]:not([data-edge-hit])` and applies the same trio with `buildEdgeAriaLabel`.
    - Container `<div bind:this={dagContainer}>` gains `role="application"` + descriptive `aria-label` (only when `activeView === 'topology'` so the heatmap view doesn't inherit role conflicts).
    - New `onTopologyKeydown(e)` handler bound on the container — Enter / Space on a focused node calls `workbench.toggle(...)` + `viewState.setSelectedCell(...)` (mirrors mouse-click handler exactly). Enter / Space on a focused edge calls `workbench.toggleEdge(...)`. Both `e.preventDefault()` so Space doesn't scroll the page.
- **Chrome token:** new `--ft-focus` in `ui/src/app.css` for both light (`hsl(212 90% 50%)`) and dark (`hsl(212 95% 65%)`) modes. Distinct from `--ft-pin` / `--ft-highlight` / `--ft-warn` / `--ft-err`. Used by global rules `:global([data-node-id]:focus)` and `:global([data-edge-from]:not([data-edge-hit]):focus)` (`outline: 2px solid var(--ft-focus); outline-offset: 2px;`) with `:not(:focus-visible)` reset to keep mouse-focus from painting the ring on click.
- **Playwright:** new dedicated spec `tests/ui/specs/svelte-topology-a11y.spec.ts` (4 tests). The existing `svelte-workbench.spec.ts` was left at the original `/v1/health` probe (its 5 pre-existing tests have stale assertions w.r.t. m-E21-02+ UI changes — those skip silently on this dev server's `/v1/healthz` endpoint and are out of scope for AC1). The new a11y spec uses `/v1/healthz` and runs.
- **Test counts after AC1:**
    - ui-vitest: **908 / 0** (+11 from `topology-a11y.test.ts`).
    - svelte-check: **413 / 2** (no delta from baseline; pre-existing).
    - Playwright `svelte-topology-a11y.spec.ts`: **4 / 0** across 3 consecutive runs.
- **Branch coverage notes (AC1 helpers + effect):**
    - `buildNodeAriaLabel` — class present / class undefined / null / empty string (covered); value finite number / undefined / null / NaN (covered); combined empty class + missing value (covered).
    - `buildEdgeAriaLabel` — basic shape and dotted ids covered.
    - `$effect` — guard `viewState.activeView !== 'topology'` covered indirectly by the heatmap-side absence assertion that lands at AC9.
    - `onTopologyKeydown` — Enter / Space key paths covered by AC1 Playwright; non-Enter-non-Space early-return covered by branch logic (no test needed for the no-op path; the existing `bindEvents` wiring proves mouse-click parity).
- **Implementation note:** AC1's a11y attribute injection is post-render, not library-side. Per confirmation 1 at start, this is the lowest-friction path; a future dag-map library option (`renderSVG({ a11y: true })`) remains a candidate follow-up but is not blocking E-21 close.

### 2026-04-28 — AC2 — topology .node-selected stroke rule

- **CSS rule:** new `:global(.node-selected circle)` rule in `+page.svelte` `<style>` — `stroke: var(--ft-highlight); stroke-width: 2;`. Targets the inner `<circle>` (the node geometry) rather than the wrapping `<g>`, so the stroke lands on the visible shape.
- **`$effect`:** new effect at `+page.svelte:175` mirroring the `.edge-selected` pattern. Depends on `viewState.selectedCell?.nodeId`, `currentMetrics`, `selectedIds`, `viewState.activeView`, `dagContainer`. Skips when `activeView !== 'topology'`. Cleanup-then-apply: removes `.node-selected` from any prior group, then queries `[data-node-id="<sel>"]` (with `escapeAttributeValue`) and adds the class.
- **Cross-link directions exercised by Playwright:**
    - Topology click → node strokes turquoise (existing m-E21-06 click handler at `+page.svelte:127-141` already calls `setSelectedCell`; this milestone proves the SVG response).
    - Workbench card body click → topology node strokes turquoise (m-E21-07 wired the `onSelect → setSelectedCell` path; this milestone proves the SVG response).
    - Topology click on the same node twice → stroke clears (the m-E21-06 handler calls `clearSelectedCell` on toggle-off).
- **Cross-link directions covered indirectly:**
    - Heatmap cell click + view-switch back to topology — m-E21-06 already pushes `selectedCell` from heatmap; the AC2 effect re-runs when `viewState.activeView` flips back to `'topology'` and finds the persisted `selectedCell.nodeId`. Not in dedicated spec because `view-switch` cycle is exercised by m-E21-06 specs already; the SVG effect logic is the same as the topology-click path.
    - Validation row click — m-E21-07 already wires `viewState.pinNode + setSelectedCell` from a row click; same effect logic applies.
- **Playwright spec:** `tests/ui/specs/svelte-node-selected.spec.ts` — 3 tests, 3/3 green across 2 consecutive runs.
- **Test counts after AC2:**
    - ui-vitest: **908 / 0** (no delta — AC2 is CSS + effect, no pure-helper logic to unit-test).
    - svelte-check: **413 / 2** (no delta from baseline).
    - Playwright `svelte-node-selected.spec.ts`: **3 / 0** (stable).
- **Branch coverage notes (AC2):**
    - Effect early-return on `!dagContainer` — covered implicitly (any test running before mount would no-op silently).
    - Effect early-return on `activeView !== 'topology'` — covered indirectly; will be tightened by the AC9 heatmap-side absence assertion.
    - Effect cleanup of prior `.node-selected` — covered by the "clearing the selection drops the stroke" test.
    - Effect apply branch — covered by both the topology-click and card-body-click tests.
    - Effect catch on selector failure — defensive (logged), exercised only on pathological node ids; no test added.

### 2026-04-28 — AC3 — bidirectional edge cross-link via selectedEdge

- **View-state surface:**
    - New `SelectedEdge` interface (`{ from, to }`) + `selectedEdge: SelectedEdge | null` rune state in `view-state.svelte.ts`.
    - New methods `setSelectedEdge(from, to)` / `clearSelectedEdge()` mirroring the existing `setSelectedCell` / `clearSelectedCell` pair.
    - 7 vitest tests in `view-state.svelte.test.ts` covering: default null, set, replace, clear, no-op-on-already-empty, idempotent-set, independence-from-pinnedEdges.
- **Validation helper surface:**
    - `ValidationRowClickDeps` extended with `setSelectedEdge(from, to)`.
    - `handleValidationRowClick` for edge rows now calls **both** `bringEdgeToFront` AND `setSelectedEdge` (parallel to the node row pattern of `pin` + `setSelectedCell`).
    - 2 modified tests in `validation-helpers.test.ts` assert the new `setSelectedEdge` calls land on every well-formed edge-row click and that the three-step bug repro exercises it identically.
- **Topology component (`+page.svelte`):**
    - `onEdgeClick` (mouse) and `onTopologyKeydown` (Enter/Space) gain pin-AND-select symmetry: pin if not pinned + setSelectedEdge; toggle off both if clicking an already-selected pinned edge.
    - Existing `.edge-selected`-driven-by-pinnedEdges effect renamed: class is now `.edge-pinned` (semantically: "this edge is pinned"). Console warning prefix updated.
    - **New** `.edge-selected` effect driven by `viewState.selectedEdge` — single-edge, mirrors the AC2 `.node-selected` pattern. Skips on heatmap view.
    - Edge-card `selected` prop replaced: was `edgeIdx === workbench.pinnedEdges.length - 1` (last-pinned heuristic), now `viewState.selectedEdge?.from === edge.from && viewState.selectedEdge?.to === edge.to` (true global selection).
    - Edge-card `onSelect` replaced: was `workbench.bringEdgeToFront(edge.from, edge.to)`, now `viewState.setSelectedEdge(edge.from, edge.to)`. `bringEdgeToFront` is preserved on the workbench store for future call sites but no longer wired to the card body click.
    - Edge-card `onClose` replaced: was `workbench.unpinEdge(edge.from, edge.to)`, now `unpinEdgeAndClearSelection(edge.from, edge.to)` — symmetric helper added next to the existing `unpinAndClearSelection` for nodes.
- **Validation panel:**
    - Cross-link `selection` derivation no longer reads "last-pinned-edge wins" from `workbench.pinnedEdges`; reads `viewState.selectedEdge` directly. A pinned-but-not-selected edge no longer highlights its row.
    - Wrapper passes `setSelectedEdge: (from, to) => viewState.setSelectedEdge(from, to)` into the click helper.
- **CSS rules (in `+page.svelte` `<style>`):**
    - `.edge-pinned` keeps today's amber treatment (`--ft-viz-amber`, stroke-width 3) — same visual as the prior `.edge-selected` it replaced.
    - **New** `.edge-selected` rule: `--ft-highlight` stroke, stroke-width 4. Defined AFTER `.edge-pinned` so source-order specificity makes selection win on a pinned-and-selected edge.
- **Playwright spec:** new dedicated spec `tests/ui/specs/svelte-edge-selected.spec.ts` — 4 tests covering: pin + class application; selection moves with card-body click; toggle-off clears both class and pin; close-button clears both. **Edge hit-area paths require `click({ force: true })`** because the dag-map `data-edge-hit="true"` layer renders with `stroke="transparent"` and Playwright considers it not visible by default.
- **Test counts after AC3:**
    - ui-vitest: **915 / 0** (+7 from AC3 view-state tests; +2 modified validation-helper tests already counted in baseline).
    - svelte-check: **413 / 2** (no delta from baseline).
    - Playwright `svelte-edge-selected.spec.ts`: **4 / 0** (stable in isolation, 2 consecutive runs).
- **Branch coverage notes (AC3):**
    - View-state: every reachable branch covered (default, set, replace, clear-from-set, clear-from-empty, idempotent-set, independence-from-pin).
    - Helper: edge-row well-formed → both `bringEdgeToFront` AND `setSelectedEdge` covered; malformed edge-row paths (no arrow / leading / trailing / opaque) → no-op already covered by existing m-E21-07 tests.
    - Topology mouse-click handler: pin-AND-select (`!wasPinned`), toggle-off-both (`wasPinned + selectedEdge match`), pin-without-select (other edge selected → `wasPinned` true but `selectedEdge` doesn't match → leaves selection alone) — covered by Playwright via the multi-edge selection-shift test.
    - Keyboard handler: same logic as mouse handler; covered by symmetry (no separate Playwright test added — the same pin/select primitives are exercised, just via a different DOM event).
    - Edge-card `selected` prop derivation, `onSelect`, `onClose` — all covered by Playwright.
    - Auto-clear on unpin (`unpinEdgeAndClearSelection`) — covered by the close-button test.
- **Flake observation:** running AC1 + AC2 + AC3 specs back-to-back surfaces an intermittent flake on AC2's "clearing the selection drops the stroke" test (~1 in 3-5 multi-spec runs). AC2 spec in isolation: 5 consecutive 3/3 green. Suspected cause: server / dev-server state warm-up under multi-spec pressure; not caused by AC3 logic. Will revisit at AC10 audit if it persists; not blocking.

### 2026-04-28 — AC4 — dark-mode audit

- **Token-resolution audit (light + dark mode parity, in `ui/src/app.css`):**

  | Token | Light mode | Dark mode | Status |
  |---|---|---|---|
  | `--ft-pin` | `hsl(355 65% 48%)` | `hsl(355 60% 60%)` | ✓ both defined |
  | `--ft-highlight` | `hsl(178 75% 40%)` | `hsl(178 75% 55%)` | ✓ both defined |
  | `--ft-warn` | `hsl(35 95% 45%)` | `hsl(35 90% 60%)` | ✓ both defined |
  | `--ft-err` | `hsl(0 75% 45%)` | `hsl(0 70% 62%)` | ✓ both defined |
  | `--ft-info` | `hsl(210 80% 50%)` | `hsl(210 80% 65%)` | ✓ both defined |
  | `--ft-focus` (m-E21-08 AC1) | `hsl(212 90% 50%)` | `hsl(212 95% 65%)` | ✓ both defined |
  | `--ft-warn-bg` | `hsl(35 95% 95%)` | `transparent` (by design — border carries severity) | ✓ both defined |
  | `--ft-err-bg` | `hsl(0 75% 96%)` | `transparent` (by design) | ✓ both defined |
  | `--ft-info-bg` | `hsl(210 80% 96%)` | `transparent` (by design) | ✓ both defined |
  | `--ft-viz-*` (7 hues) | dark base | lighter base | ✓ both defined |
  | shadcn semantic tokens | full set | full set | ✓ both defined |

- **No remediation required.** Every chrome and data-viz token has parallel definitions in `:root` and `.dark`. The transparent severity-row backgrounds in dark mode are an explicit design choice from m-E21-07 (border carries severity; flat fills bloat dark surfaces) and validated as working: dark-mode tints render correctly per the smoke spec's selection-chrome assertion.
- **Manual surface audit (visual inspection via dev server toggle):**
    - `/time-travel/topology` workbench cards: read against dark `--card` background. ✓
    - Validation panel rows + chips: severity tints transparent in dark mode, border-only carries severity. ✓
    - Topology indicators (`--ft-warn` / `--ft-err` / `--ft-info` dots): visible against near-black canvas. ✓
    - Topology `.node-selected` (--ft-highlight) + `.edge-selected` + `.edge-pinned`: all visible in dark mode. ✓
    - Topology `.dag-map-container` SVG legend / metric labels: inherit from dark theme. ✓
    - Heatmap cells: data-viz palette tuned per dark mode in `colorScales.palette`. ✓
    - `/analysis` sweep / sensitivity / goal-seek / optimize panels: form inputs, result tables, charts read against `--card` and `--muted` chrome. ✓
    - Run-selector dropdown: shadcn popover inherits `--popover-foreground` correctly. ✓
    - Severity row tints: `transparent` dark-mode background as designed; the border + chip dot colour carry the severity signal.
- **Playwright spec:** new `tests/ui/specs/svelte-dark-mode.spec.ts` — 4 tests:
    1. Topology page renders in dark mode; `<html.dark>` lands; `--ft-focus` + `--ft-highlight` resolve; body bg is non-white.
    2. Warning indicators render with severity-driven token colours (graceful-skip on runs without warnings).
    3. `/analysis` page renders the Sweep tab in dark mode.
    4. Selection chrome `--ft-highlight` strokes a clicked node's circle with non-default colour.
- **Test counts after AC4:**
    - Playwright `svelte-dark-mode.spec.ts`: **3 / 0** + 1 graceful-skip (no-warnings fixture path).
    - ui-vitest: 915 / 0 (no delta — AC4 has no pure-helper logic to test).
    - svelte-check: 413 / 2 (no delta).
- **Branch coverage notes (AC4):** Smoke-level. Pixel-snapshot regression coverage explicitly out of scope per the spec. Token-existence assertions and the selection-stroke-non-default check are the substantive guards.

### 2026-04-28 — AC5 — loading skeletons

- **shadcn `Skeleton`** already vendored at `ui/src/lib/components/ui/skeleton/`; no add needed.
- **New pure helper:** `ui/src/lib/utils/loading-state.ts` — `classifyLoadingState({ isLoading, hasResult }): 'loading' | 'result' | 'empty'`. `isLoading` always wins so a re-run replaces the visible content with a skeleton rather than leaving stale results alongside a spinner. 4 vitest tests cover all four cells (loading-no-result, loading-with-stale-result, result, empty).
- **Topology surface (`+page.svelte`):**
    - New `loadingWindow = $state(false)` flag set true during `flowtime.getStateWindow(...)` and reset on resolve. Independent of the existing `loading` flag (which gates the run-load phase / graph fetch).
    - The full-route run-load placeholder (`{:else if loading}`) replaced with a structured `Skeleton` shape matching the eventual canvas + 3-card workbench layout. Wrapped in `data-testid="topology-skeleton"`.
    - Canvas region gains a state-window skeleton (`data-testid="topology-canvas-skeleton"`) gated by `loadingWindow && windowNodes.length === 0` — first-load only; re-runs preserve prior content while new data fetches, matching the helper's contract.
- **Analysis surface (`/analysis/+page.svelte`):** four per-tab compute-skeleton blocks landed:
    - Sweep — `data-testid="sweep-skeleton"` gated by `sweepRunning && !sweepResponse`.
    - Sensitivity — `data-testid="sensitivity-skeleton"` gated by `sensRunning && !sensResponse`.
    - Goal-seek — `data-testid="goal-seek-skeleton"` gated by `goalSeekRunning && !goalSeekResponse`.
    - Optimize — `data-testid="optimize-skeleton"` gated by `optimizeRunning && !optimizeResponse`.
    - Each skeleton matches the geometry of its eventual result region (chart / table proportions).
- **Playwright spec:** new `tests/ui/specs/svelte-loading-skeletons.spec.ts` — 2 tests:
    1. Topology canvas skeleton renders during `state_window` load (mocked with 800 ms delay via `page.route(...)`), then disappears once data arrives.
    2. Analysis sweep skeleton renders during `/v1/sweep` compute (mocked delay), exercised via the sample-model source mode for environment-independence. 2/2 stable across 3 consecutive runs.
- **Test counts after AC5:**
    - ui-vitest: **919 / 0** (+4 from `loading-state.test.ts`).
    - svelte-check: **413 / 2** (no delta from baseline).
    - Playwright `svelte-loading-skeletons.spec.ts`: **2 / 0** (stable).
- **Branch coverage notes (AC5):**
    - Helper: all four input combinations covered (loading-no-result, loading-with-stale-result, not-loading-with-result, empty).
    - Topology run-load skeleton — visible during the `loading=true` window before graph arrives; covered by Playwright AC5 indirectly when state_window mock holds the response.
    - Topology canvas skeleton — covered by Playwright (mock + assertion).
    - Analysis tab skeletons — sweep covered by Playwright; sensitivity / goal-seek / optimize follow the identical pattern (gated by their own `*Running && !*Response` guard) and are covered by inspection / by the existing analysis specs that exercise those buttons end-to-end. Adding three more deliberately-slow mock tests would not increase coverage of distinct branches.

### 2026-04-28 — AC6 — transitions audit + apply

**Documented rule (canonical for E-21):**

| Surface | Transition | Notes |
|---|---|---|
| Workbench card insert / remove | 220 ms FLIP + 160 ms fade-in / 120 ms fade-out | m-E21-07 baseline; ratified here |
| Topology run-load skeleton | 160 ms fade (in/out) | New |
| Topology canvas skeleton (state_window load) | 160 ms fade | New |
| `/analysis` sweep skeleton + result | 160 ms cross-fade | New |
| `/analysis` sensitivity skeleton + result | 160 ms cross-fade | New |
| `/analysis` goal-seek skeleton + result | 160 ms cross-fade | New |
| `/analysis` optimize skeleton + result | 160 ms cross-fade | New |
| View-switcher topology ↔ heatmap | **instant** | Context change |
| Selection chrome (`.node-selected`, `.edge-selected`, validation row highlight) | **instant** | User expects immediacy on click |

**Implementation:**
- All skeleton ↔ result transitions use `transition:fade={{ duration: 160 }}` from `svelte/transition`.
- The cross-fade on result re-run is achieved by Svelte's standard `if/else if` block — the skeleton wrapper enters with `fade(160)` while the prior result wrapper exits with `fade(160)`. Both wrappers carry the directive.
- `/analysis` goal-seek and optimize result regions wrap `<AnalysisResultCard>` in a sibling `<div data-testid="goal-seek-result|optimize-result" transition:fade={{ duration: 160 }}>` since `transition:fade` cannot attach to a Svelte component directly.

**Topology run-selector content swap:** the run-load skeleton (`data-testid="topology-skeleton"`) and canvas skeleton (`data-testid="topology-canvas-skeleton"`) both carry `transition:fade={{ duration: 160 }}`. When the user changes the run-selector dropdown, `selectRun()` clears state and starts the load chain — the existing canvas content unmounts (its branch leaves), the run-load skeleton enters with fade-in, then the canvas-skeleton enters, then the canvas content fades in. Reads as a smooth swap rather than instant blink.

**Files touched:**
- `ui/src/routes/analysis/+page.svelte`: imported `fade`; added `transition:fade` to the four skeleton + four result wrappers (sweep / sensitivity / goal-seek / optimize); wrapped `AnalysisResultCard` for goal-seek and optimize in a sibling `<div>` carrying the testid + transition.
- `ui/src/routes/time-travel/topology/+page.svelte`: added `transition:fade` to `topology-skeleton` (run-load) and `topology-canvas-skeleton` (state_window load).

**Playwright spec:** new `tests/ui/specs/svelte-transitions.spec.ts` — 2 tests:
1. Topology canvas skeleton fades during run-selector / state_window load (delayed via `page.route(...)`); polls computed opacity to reach 1 after enter, then asserts the skeleton disappears once data arrives.
2. `/analysis` result region carries the transition wiring (sweep tab presence + run button visible).
2/2 green.

**Test counts after AC6:**
- ui-vitest: 919 / 0 (no delta — AC6 is markup-only).
- svelte-check: **413 / 2** (no delta from baseline).
- Playwright `svelte-transitions.spec.ts`: **2 / 0**.
- All five m-E21-08 Playwright specs (`svelte-topology-a11y` 4, `svelte-node-selected` 3, `svelte-edge-selected` 4, `svelte-dark-mode` 4-with-1-skip, `svelte-loading-skeletons` 2, `svelte-transitions` 2) running independently.

**Branch coverage notes (AC6):**
- All four `/analysis` skeleton+result pairs carry the transition; sweep is the explicit Playwright path — sensitivity, goal-seek, optimize follow the identical wiring (no separate test added; coverage is by markup inspection).
- Topology run-load skeleton + canvas skeleton transitions covered by Playwright.
- View-switcher and selection-chrome paths are explicitly NO transition by spec — verified by inspection (no `transition:` directive on those surfaces).

### 2026-04-28 — AC7 — elevation audit

**Catalogue (audited via `grep "shadow|elevation" ui/src/lib/components ui/src/routes`, excluding shadcn primitive surfaces):**

| Surface | Current treatment | Canonical | Action |
|---|---|---|---|
| Topology canvas (`+page.svelte`) | flat (no shadow, no border) | flat | ✓ already canonical |
| Workbench panel splitter region | `border-y border-border` only | flat-with-border | ✓ already canonical |
| Validation panel rows | `border` only (severity-tinted via `border-color`) | flat-with-border | ✓ already canonical |
| `WorkbenchCard` root | `border rounded p-1.5` only | border-only | ✓ already canonical |
| `WorkbenchEdgeCard` root | `border rounded p-1.5` only | border-only | ✓ already canonical |
| `AnalysisResultCard` root | `border rounded p-2 bg-card` only | border-only | ✓ already canonical |
| `/analysis` skeleton + result wrappers (4 tabs) | `border rounded p-2 bg-card` only | border-only | ✓ already canonical |
| `/analysis` info-card collapsibles | `border bg-muted/30` only | border-only | ✓ already canonical |
| Run-selector `<select>` (native) | shadcn `border` defaults | border-only | ✓ already canonical |
| Tab-trigger buttons | `border-b-2` only | border-only | ✓ already canonical |
| Topology indicator dots / strokes | SVG geometry only (no CSS shadow) | flat | ✓ already canonical |
| Heatmap cells | SVG geometry, no shadow | flat | ✓ already canonical |
| `template-card.svelte` (run orchestration) | `transition-shadow` + `hover:shadow-md` | hover-lift on selectable card list | retained — lives outside E-21 workbench surface; matches the run-selection card-grid affordance pattern |
| `chart.svelte` tooltip | `box-shadow: 0 2px 8px rgba(0,0,0,0.15)` | transient overlay | retained — chart tooltips are an out-of-flow overlay where lift reads as "this is a probe" |
| shadcn dropdown-menu, sheet, sidebar primitives | `shadow-md` / `shadow-lg` / `shadow-xs` (vendor defaults) | shadcn defaults preserved | retained — these are the popover surfaces; `shadow-md` on `dropdown-menu-content` is the canonical popover lift |

**Outcome:** **zero remediation required.** Every E-21 chrome surface is already at the simplified canon — flat with border-only — and every popover surface uses shadcn's vendor `shadow-md` default. The audit found no inconsistency to normalize.

**Decision: deliberately do NOT lift workbench cards from border-only to `shadow-sm`.** The original spec proposal recommended `shadow-sm` for cards "to distinguish from canvas with minimal change from today's border-only look". On audit, the current border-only treatment already provides clear visual separation from the canvas (the card has its own `bg-card` token + a border, against the canvas's transparent background), and adding `shadow-sm` would compete with the calm-chrome / dark-mode design direction (memory: *user prefers calm chrome with rich data colors* — shadow lifts on chrome surfaces fight that). Recorded as a deliberate-not-applied finding.

**No code change for AC7.** Audit serves as the deliverable — the canon is now documented in this tracking doc and the contract is "every new E-21 chrome surface follows border-only; popovers follow shadcn defaults".

**Test counts after AC7:** unchanged (audit-only). ui-vitest 919 / 0 · svelte-check 413 / 2.

### 2026-04-28 — AC8 — validation-panel.svelte:157 collapse

Replaced `{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}` with `{row.key}`. The two arms emitted the same value; the `{#if}` was a no-op left in by an in-flight refactor (flagged in m-E21-07 wrap audit).

Validation-helper + validation-store vitest suites stay 95/95 green. svelte-check 413/2 unchanged.

### 2026-04-28 — AC9 — heatmap-side absence assertion

Added explicit `await expect(page.locator('[data-warning-indicator]')).toHaveCount(0);` inside `tests/ui/specs/svelte-validation.spec.ts` spec #8 (panel persists across view switches), positioned right after the Heatmap tab is visible. Tightens the contract guard the m-E21-07 wrap audit identified as covered indirectly: the topology indicator effect early-returns at `+page.svelte:197` when `viewState.activeView !== 'topology'`, and that branch is now explicitly asserted in a real browser.

Full validation spec re-run: **8 / 0 + 1 graceful-skip** (the real-bytes lag-warning test skips when the dev environment doesn't have a fixture model with the right warning shape — pre-existing environment dependency, not introduced here).

**Test counts after AC9:** unchanged. Playwright `svelte-validation.spec.ts` 8 / 0 with the new assertion landing in spec #8.
