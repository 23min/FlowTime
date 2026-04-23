# Milestone: Heatmap View

**ID:** m-E21-06-heatmap-view
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** in-progress
**Created:** 2026-04-23
**Started:** 2026-04-23
**Branch:** `milestone/m-E21-06-heatmap-view` (branched from `epic/E-21-svelte-workbench-and-analysis`)

## Goal

Deliver a nodes-x-bins heatmap view as a **sibling of topology** under `/time-travel/topology`, sharing the toolbar, class filter, metric selector, timeline scrubber, workbench sidebar, and pin state. Heatmap reuses the existing `GET /v1/runs/{runId}/state_window` endpoint — **zero backend changes**. Introduce a typed `ViewSwitcher` component, a shared view-state store that both views consume, and a shared full-window color-scale normalization so "bright red at (N, T)" on the heatmap matches "bright red on node N" on topology when the scrubber is at bin T.

## Context

E-21's first five milestones delivered the workbench paradigm (m-E21-01 foundation + click-to-pin cards), the metric selector + edge cards + class filter (m-E21-02), and the `/analysis` analysis surfaces (m-E21-03 sweep/sensitivity, m-E21-04 goal-seek, m-E21-05 optimize). What remains on the "views around the data" side of E-21 is:

1. A second view of the same model — the heatmap — that reveals temporal patterns a single-bin topology snapshot cannot (a node that is fine in bins 1–4 but saturated in bins 5–8 is invisible on topology; the heatmap makes that obvious).
2. A reusable view-switcher shape so later views (decomposition, comparison, flow-balance — all out of E-21 scope) can slot in without structural refactoring.

The heatmap's data need (`state_window` per-node per-bin series) is already served by the Engine API. `ui/src/lib/api/flowtime.ts:101` already exposes `getStateWindow(runId, startBin, endBin)`. The topology page (`ui/src/routes/time-travel/topology/+page.svelte`) already calls it to populate sparklines. The heatmap calls it once per scenario, identically.

### Design decisions settled at planning (2026-04-23 Q&A)

The 14-question Q&A on 2026-04-23 locked every design decision below. The key shape:

- **View location (Q1):** Heatmap is a sibling of topology under `/time-travel/topology`, behind a view switcher on the canvas. Not an `/analysis` tab. Not its own route.
- **Workbench integration (Q2):** Heatmap replaces the **canvas** when selected; toolbar + scrubber + workbench sidebar persist unchanged across view switches. Pin state and scrubber position survive view switches in both directions.
- **Shared normalization (Q3):** Shared full-window color scale with 99th-percentile clipping. **Topology's per-bin normalization changes to match** — this is explicitly a cross-view parity change, captured under ADR-m-E21-06-02 below.
- **Axis orientation (Q4):** Nodes as rows (Y, labels on left), bins as columns (X, left-to-right).
- **View switcher (Q5, Q13):** Horizontal tabs above the canvas (`[ Topology | Heatmap ]`), shadcn-style underline, `Alt+1` / `Alt+2` shortcuts. Typed `<ViewSwitcher views={[...]} active={view}>` component with inline view array on the topology page — **no manifest registry, no Svelte context API**. Captured under ADR-m-E21-06-01.
- **Class filter (Q6):** Full parity with topology — hides rows AND restricts metric computation AND domain computation. Adds a **row-stability toggle** that dims filtered rows in place (off by default).
- **Row sort (Q7):** Topological order is the default; modes = topological / node id / max desc / mean desc / variance desc. Pinned-first modifier is always-on.
- **Cell states (Q8):** Three states — observed (colored), no-data-for-bin (neutral grey + subtle hatch), metric-undefined-for-node (row-level muted). Tooltip always disambiguates.
- **Scrubber coupling (Q9):** Two-way. Scrubber position highlights the current-bin column in the heatmap; clicking a cell jumps the scrubber and pins the node.
- **Pinned row markers (Q10):** Pin glyph in the row-label gutter, click-to-unpin. Redundant with positional float (Q7).
- **Bin-axis labels (Q11):** Sparse human time labels on the top axis; stride chosen by column pixel width × bin size. Absolute time if the run has a `startTime`, offset-from-start otherwise. Tooltip always shows both bin index and time.
- **Accessibility baseline (Q12):** Keyboard nav, ARIA grid structure, focus ring, tooltip-on-focus, one keyboard Playwright spec. Pattern encoding / high-contrast / screen-reader polish deferred to m-E21-08.
- **Testing (Q14):** 13 Playwright critical-path specs + 5 vitest pure-logic suites.
- **Node-mode toggle (Q15, added 2026-04-23 after Q14):** Shared toolbar toggle `[ Operational | Full ]` controlling the `mode` parameter on `GET /v1/runs/{runId}/state_window`. Operational (default) hides `expr`/`const`/`pmf` computed nodes — matches the Blazor UI's "operational nodes" toggle. Full exposes them; they render as row-level-muted rows under operational metrics (utilization, queue depth) per AC4, and as coloured rows under metrics that are defined for them (value / output). Toggle state lives in the shared view-state store and applies to **both** topology and heatmap (re-fetches `state_window` on change).

### API contract

**GET /v1/runs/{runId}/state_window** — `src/FlowTime.API/Program.cs:1028`

Query: `startBin`, `endBin` (required); optional `mode` (`operational` default, `full` available), `edgeIds`, `edgeMetrics`, `classIds`. Heatmap calls this **once per scenario** with `startBin=0`, `endBin=binCount-1`, no edge/class filter in the request (class filter is applied client-side to allow the toggle-in-place behaviour from Q6). `mode` is passed from the shared view-state store (AC15 node-mode toggle) and drives which node kinds appear in the response. Response is the same `StateWindowResponse` shape topology already consumes. No new endpoints, no additive response fields, no carve-outs needed.

Client surface: `getStateWindow` gains an `mode?: 'operational' | 'full'` parameter (default `operational`, backward-compatible for existing call sites). The heatmap and topology both read `mode` from the shared store and pass it through.

## Acceptance Criteria

1. **View switcher renders above the canvas.** `/time-travel/topology` shows a horizontal tab bar `[ Topology | Heatmap ]` above the DAG/heatmap area, implemented as a new `<ViewSwitcher views={[...]} active={view} onChange={...}>` component at `ui/src/lib/components/view-switcher.svelte`. Views are listed inline in the route's `<script>` block (not via a manifest file). `Alt+1` selects Topology, `Alt+2` selects Heatmap. Active view is visually indicated with a thin underline in shadcn style. Initial view is Topology on first load; active view is **not** persisted across page reloads in this milestone. (Q1, Q5, Q13)

2. **Heatmap replaces the canvas only.** When Heatmap is active, the DAG area is replaced by the heatmap grid. The toolbar (run selector, metric selector, class filter, view switcher), timeline scrubber, and workbench panel all persist unchanged. Splitter ratio + workbench pins + scrubber position are preserved across view switches. Switching back to Topology restores the DAG with no loss of state. (Q2)

3. **Heatmap grid renders correct dimensions.** For a scenario with `N` nodes and `B` bins, the grid renders `N` rows and `B` columns. Row labels (node ids) appear on the left in the active sort order; bin-axis labels appear on the top with a sparse stride chosen by a pure helper (`pickBinLabelStride(columnPixelWidth, binSize, binUnit)`). If the run carries `startTime`, bin labels are absolute times (`09:00`, `Mon 12:00`); otherwise they are offset-from-start (`+00:00`, `+03:30`). Tooltip on any cell shows both `Bin N · <time>`. (Q4, Q11)

4. **Three cell states render correctly with disambiguating tooltips.**
    - **Observed** (including `0`): cell fill from the shared color scale. Tooltip: `<metric-label>: <value>` (e.g. `Utilization: 0.34` or `Utilization: 0 (observed)` for zeroes).
    - **No data for bin**: neutral grey fill with a subtle diagonal-hatch pattern (distinct from any color on the scale; excluded from the normalization domain). Tooltip: `<metric-label>: — (no data for this bin)`.
    - **Metric undefined for node**: if `state_window` returns no value for this metric across **all** bins of this node, render the **entire row** uniformly muted (row-level optimization — not per-cell hatch). If undefined for only some bins, collapse to the no-data rendering per cell. Tooltip for the muted-row state: `<metric-label>: not defined for this node`. The classifier keys are **per-(node, metric)** — a `const` node has no `utilization` metric and therefore renders as a row-level-muted row under the utilization metric, but may render normally under a `value` metric defined for it. When the node-mode toggle (AC15) is set to `full`, `expr`/`const`/`pmf` computed nodes appear as rows and typically surface as row-level-muted under operational metrics (utilization, queue depth).
    - Click on a non-observed cell is a no-op (no pin, no scrubber jump). Click on a row label is **not** a click-to-pin target; cells are the pinning surface (clicking the pin glyph in the gutter is the un-pin surface, per AC10). (Q8)

5. **Shared color-scale normalization (full window, 99p-clipped, excluding non-observed and filtered).** Both topology and heatmap compute metric color with the same domain: `[min, 99th-percentile]` over the **full-window** cell set for the current metric and the current class filter, excluding cells in the no-data or metric-undefined states. Topology's previous per-bin normalization is replaced by this shared-window normalization; the topology-at-bin-T color of node N equals the heatmap cell color at (N, T). A shared helper `computeSharedColorDomain(cells, { classFilter, excludeNonObserved: true, clipPercentile: 99 })` owns this computation; topology's `buildMetricMapForDefFiltered` (and any downstream color mapper) is refactored to consume it rather than computing a per-bin domain. Fixed per-metric ranges and per-row normalization are **deferred** (tracked in `work/gaps.md`). (Q3, ADR-m-E21-06-02)

6. **Row sort modes implemented with pinned-first modifier always on.** Sort modes shipped: **topological (default)** / node id (alphabetical) / max desc / mean desc / variance desc. Pinned nodes always float to the top regardless of active sort. Sort control appears in the heatmap toolbar (compact dropdown or segmented control). Topological order is derived from the graph's parent/child edges (same ordering topology uses as its natural top-to-bottom walk); the comparator falls back to node-id for topologically-equivalent nodes (deterministic tie-break). Changing the metric preserves the active sort mode and recomputes values (value-based modes reorder accordingly). Current-bin-value and trend/slope modes are **deferred** (tracked in `work/gaps.md`). (Q7)

7. **Class filter with optional row-stability toggle.** Class filter (already present on the topology toolbar) hides heatmap rows by default, exactly as it hides topology nodes. Normalization domain recomputes over the filtered cell set. An empty-state message (`No nodes match the current class filter`) renders when the filter collapses the grid to zero rows. A new **row-stability toggle** (compact control in the heatmap toolbar, label "Keep filtered rows") is off by default; when **on**, filtered rows remain in the grid as dimmed placeholders: muted / italic row label, low-alpha grey fill across all cells (or subtle hatch — whichever reads cleaner against the active theme), excluded from the normalization domain, click on a dimmed cell is a no-op with tooltip `Node filtered by class`, filtered rows sink to the bottom of the grid (and are sorted independently by the active sort within the dimmed block). The toggle persists as a user preference in `localStorage` (`ft.heatmap.rowStability`). Pin state for filtered nodes persists across filter-toggle changes. The **node-mode toggle** (AC15) is a sibling toolbar control that governs a different dimension (node kind, not class); the two filters compose — a class-filtered row in `full` mode is still class-filtered. (Q6)

8. **Click-a-cell pins node and jumps scrubber.** Click on an observed cell at (N, T) pins node N in the workbench and moves the scrubber to bin T. Both effects happen together (single click, atomic visually). Already-pinned → click → stays pinned (no unpin via cell click; unpin is via the pin glyph in the row gutter, AC10, or via the workbench card's existing close button). Scrubber-jump triggers the existing `loadBin(T)` flow, which refreshes topology's `stateNodes` and the workbench card's current-bin metrics — view-specific side effects (topology re-color, card refresh) happen automatically because they subscribe to `currentBin`. (Q2, Q9)

9. **Scrubber-to-column highlight (two-way coupling).** Moving the scrubber draws a subtle column highlight on the heatmap at the current bin: thin 1px outline around that column's cells or a low-alpha chrome-toned strip behind them. Updates in real time as the scrubber drags (no full grid re-render — implemented as a CSS class toggle or a single SVG attribute update on a pre-rendered overlay rect). The highlight color uses a chrome token (not a data-viz token) so it does not compete with cell colors. Clicking a cell jumps the scrubber, which automatically moves the highlight. (Q9)

10. **Pinned row glyph in the label gutter.** Pinned rows render a small pin/bookmark glyph next to their label in the row-label gutter (chrome color). Clicking the glyph unpins the node. The glyph is keyboard-focusable and Tab-reachable; Enter / Space on the glyph unpins. Combined with AC6's pinned-first float, pinned rows are marked both positionally (top of the grid) and iconically (glyph). (Q10)

11. **Bin-axis labels with hover parity.** Top axis renders sparse human time labels with auto-chosen stride (`pickBinLabelStride`): labels step so that every label is clearly readable at the current column pixel width, and the stride uses sensible round units given `binSize × binUnit` (e.g. hourly ticks for a 5-minute grid; daily ticks for an hourly grid). Thin 1–2px tick marks render between labels. If the run has `startTime` (`RunGrid.startTime` in `ui/src/lib/api/types.ts:21`), labels are absolute (`09:00`, `Mon 12:00`); otherwise offset-from-start (`+00:00`, `+03:30`). Tooltip on any cell always shows **both** bin index and time, e.g. `Bin 42 · +03:30` or `Bin 42 · 12:30`. (Q11)

12. **Accessibility baseline — keyboard nav + ARIA grid + focus ring + tooltip-on-focus.**
    - **Keyboard nav:** Tab enters the grid (focus lands on the first cell of the first row in current sort order); arrow keys move focus between cells, respecting the active sort + filter; Enter / Space on an observed cell triggers the pin + scrubber-jump (same semantics as mouse click); Escape returns focus to the heatmap toolbar.
    - **ARIA structure:** `role="grid"` on the container, `role="row"` on rows, `role="gridcell"` on cells with `aria-label` containing node id + bin time + metric + value, or state (`no data for this bin`, `metric not defined for this node`, `filtered by class`).
    - **Focus ring:** clear visible focus indicator on the currently-focused cell; also visible on the pin glyph (AC10) when Tab lands there.
    - **Tooltip triggers on keyboard focus** in addition to mouse hover.
    - **One Playwright spec** proves the keyboard path end-to-end (AC14 spec #12).
    - **Homework check during implementation**: confirm whether the topology page today has comparable keyboard nav + ARIA. If it does, match its bar. If it does not, m-E21-06 sets the bar going forward — record the finding in the tracking doc under a "Prior accessibility posture" note and file a gap against topology in `work/gaps.md`. Likewise confirm whether the m-E21-02 data-viz color scales were validated for color-blind friendliness; if not, file a broader gap.
    - Pattern encoding (redundant hatch/stripe overlay for color-blind mode), high-contrast tuning, and NVDA/VoiceOver audit are **deferred to m-E21-08**. (Q12)

13. **Shared view-state store.** Introduce `ui/src/lib/stores/view-state.svelte.ts` exposing:
    - `selectedMetric` (already on workbench store — may be re-exposed / proxied here if the view store becomes the canonical reader)
    - `activeClasses` (currently route-local in topology — move into the view store so both views share one filter)
    - `currentBin`, `binCount`, `playing` (currently route-local — move into the view store)
    - `pinnedNodes`, `pinnedEdges` (already on workbench store — may continue to live there; view store can hold references/selectors)
    - `activeView` (`'topology'` | `'heatmap'`), `sortMode`, `rowStabilityOn`
    - `nodeMode` (`'operational'` | `'full'`, AC15) — drives the `mode` query parameter on `state_window` calls from both views

    The store is the single source of truth for what both views consume. Route `+page.svelte` becomes thin — it wires the store to the toolbar, view switcher, and active view's component. The existing `workbench` store stays authoritative for pin state; `view-state.svelte.ts` is additive. No breaking changes to `workbench.svelte.ts`'s public surface. (Q13)

14. **Testing — 13 Playwright specs + 5 vitest pure-logic suites, every reachable branch covered.**
    - **Playwright** (live Rust engine + Svelte dev server, graceful-skip on probe, under `tests/ui/specs/svelte-heatmap.spec.ts` or extending `svelte-topology.spec.ts` — authoring choice at implementation time, but one file is fine):
        1. Loads a run and renders the grid with expected `(N nodes × B bins)` dimensions.
        2. Renders all three cell states (observed / no-data / metric-undefined) with correct disambiguating tooltips on a fixture known to exercise each state.
        3. Click-an-observed-cell pins the node in the workbench **and** moves the scrubber to that bin.
        4. Metric switch recolors cells **and** reorders rows under max-descending sort.
        5. View-switch state preservation: pin a node on topology → switch to heatmap → row is marked pinned (glyph present) and floated to the top → switch back to topology → pin is still in the workbench.
        6. Scrubber drag moves the column highlight in real time; clicking a cell jumps the scrubber and the highlight follows.
        7. Sort modes reorder rows: assert row order changes when cycling topological → node-id → max-desc → mean-desc.
        8. Class filter default = hide: rows disappear. Row-stability toggle ON = filtered rows render dimmed at the bottom with muted labels. Toggle OFF = filtered rows hidden again.
        9. Empty-state message appears when the class filter collapses the grid to zero rows.
        10. (Requirement on every spec, not a standalone test) Graceful skip when API / dev server is unavailable.
        11. **Correctness spec:** cell color matches the expected metric-value bucket for a known fixture. Assert via a `data-value-bucket` attribute on the cell (`low` / `mid` / `high` — the bucketing is a pure helper that lives alongside the color-scale code, with its own vitest coverage), **not** by parsing CSS color strings. Buckets are coarse by design; the vitest suite for `computeSharedColorDomain` + the bucket helper is what enforces normalization correctness at value level.
        12. Keyboard nav critical path — Tab into grid → arrow keys to a known cell → Enter → assert pin + scrubber jump happen (same observable effect as spec #3, different input modality).
        13. Node-mode toggle (AC15): default `operational` — grid shows N rows (service/queue/etc. only); toggle to `full` — grid row count grows to include `expr`/`const`/`pmf` nodes; under the `utilization` metric the newly-appearing computed-node rows render as row-level-muted; toggle back to `operational` — grid returns to N rows. Assert topology's visible node count matches the heatmap's row count under each toggle state (shared-state parity).
    - **Vitest pure-logic suites** (branch-covered per the hard rule):
        1. **Sort comparators** (`heatmap-sort.ts` / `.test.ts`): topological / id / max / mean / variance + pinned-first modifier. Exercises: empty input, single-node, all-equal metric values (tie-break to id), pinned + unpinned mix across every sort mode, topological tie-break for siblings.
        2. **Color-scale domain** (`shared-color-domain.ts` / `.test.ts`): shared-window 99p-clipped normalization excluding missing / undefined / filtered cells. Exercises: all-observed, mix of observed and missing, all-missing (domain is empty — caller must fall back), single-value domain (min == max), 99p edge cases (exactly 100 cells, fewer than 100 cells), class-filter exclusion.
        3. **Cell-state classifier** (`cell-state.ts` / `.test.ts`): observed (including 0) / no-data-for-bin / metric-undefined-for-node (row-level optimization). Exercises: undefined on all bins → row-level, undefined on some bins → per-cell no-data, zero value → observed.
        4. **Bin-axis label stride** (`bin-label-stride.ts` / `.test.ts`): `pickBinLabelStride(columnPixelWidth, binSize, binUnit)`. Exercises: 5-min grid wide columns → hourly ticks, 5-min grid narrow columns → larger stride, hourly grid → daily ticks, degenerate inputs (0 columns, 1 bin, huge binSize).
        5. **View-state store selectors** (`view-state.svelte.test.ts`): metric get/set, class filter add/remove/clear, scrubber set/move, pin/unpin proxies to workbench store, row-stability toggle persist, sort-mode set, active-view switch, node-mode toggle (operational ↔ full) with localStorage persistence.
    - **Branch audit** — line-by-line on the new UI + helpers, matching every reachable branch to a test, recorded in the tracking doc's Coverage Notes section (match m-E21-05's audit structure). (Q14)

15. **Node-mode toggle (operational / full).** A compact segmented-control toggle in the shared toolbar labelled `[ Operational | Full ]` (default `Operational`). The toggle's state lives in the shared view-state store as `nodeMode` and drives the `mode` query parameter on `GET /v1/runs/{runId}/state_window` calls from both topology and heatmap. Switching to `Full` re-fetches `state_window` with `mode=full`, which includes `expr`/`const`/`pmf` computed-node kinds in the response; switching back to `Operational` re-fetches with `mode=operational`, hiding them. Both views re-render under the new node set. Under operational metrics (utilization, queue depth), computed nodes surface as row-level-muted rows per AC4; under metrics defined for them (if any surface in the response), they colour normally. Pin state for a node that disappears when toggling to `Operational` persists in the workbench; the row simply stops rendering until the user returns to `Full`. Toggle value persists in `localStorage` (`ft.view.nodeMode`). Matches the Blazor UI's pre-existing "operational nodes" toggle — parity with that affordance is explicit. (Q15)

## Technical Notes

- **Data loading.** Heatmap consumes the same `state_window` response topology already loads for sparklines (`windowNodes` in `ui/src/routes/time-travel/topology/+page.svelte:186`). No extra network call is introduced. If topology has not yet loaded `windowNodes` when the user switches to heatmap first, heatmap triggers the same load; both views then share the cached data via the view-state store.

- **Rendering choice.** SVG. For the E-21 target scale (~20 nodes × ~100 bins = 2000 rects), SVG is well within limits. Virtualization is not in scope for this milestone. If a future real-world model exceeds ~50 nodes × ~300 bins and lag becomes visible, file a gap — do not pre-optimize.

- **Client API change.** `getStateWindow` gains an `mode?: 'operational' | 'full'` parameter (appended to the query string when provided). Default remains `operational`. All existing call sites continue to work unchanged. Active use: both topology and heatmap call sites read `nodeMode` from the shared view-state store (AC13, AC15) and pass it through on every `state_window` fetch.

- **Node-mode toggle placement and persistence.** A segmented-control style `[ Operational | Full ]` toggle lives in the shared toolbar on `/time-travel/topology`, next to the class filter. Persisted in `localStorage` as `ft.view.nodeMode` (default `operational`). Changing the toggle triggers a re-fetch of `state_window` — not a local re-filter — because `mode` selects which node kinds the API returns. Both topology and heatmap re-render against the fresh response. Parity target: the Blazor UI's "operational nodes" toggle; the Svelte UI has been missing this affordance.

- **Normalization helper placement.** `computeSharedColorDomain` lives in `ui/src/lib/utils/shared-color-domain.ts` and is consumed by both views. Topology's `buildMetricMapForDefFiltered` (in `ui/src/lib/utils/metric-defs.ts`) is refactored to delegate the domain computation to this helper. The metric-to-cell-color mapper (which takes a domain + a value and returns a color token) also moves / aligns here so both views stay in sync. This is the "topology coloring behaviour changes" seam from ADR-m-E21-06-02.

- **View switcher shape.**
    ```ts
    type View = { id: string; label: string; shortcut?: string; icon?: unknown; };
    // route usage:
    const views: View[] = [
      { id: 'topology', label: 'Topology', shortcut: 'Alt+1' },
      { id: 'heatmap',  label: 'Heatmap',  shortcut: 'Alt+2' },
    ];
    ```
    `<ViewSwitcher views={views} active={state.activeView} onChange={v => state.activeView = v} />`. No context API, no registry, no `supports(run)` predicates. Adding a future view = one inline array entry + one conditional render branch in the route.

- **Scrubber coupling mechanics.** The column highlight is a single pre-rendered `<rect>` overlay (full grid height, 1-column wide) whose `x` attribute updates when `currentBin` changes. Zero grid re-render cost. Alternative implementation: a CSS class on the column's cells — but SVG class-toggling at scale is more expensive than a single `setAttribute` on one overlay rect. Use the overlay rect.

- **Pin glyph.** Lucide icon (`PinIcon` or `BookmarkIcon`) rendered in the row-label gutter. Wrap in a `<button>` for keyboard focusability + click handling + accessible name (`aria-label="Unpin <node id>"`).

- **Row-stability toggle storage key.** `ft.heatmap.rowStability` (follows the `ft.topology.split` convention already used on the topology page).

- **Bin-label stride math.** Target ~8–12 visible labels across the typical viewport width. Pick the stride whose (`columnPixelWidth × stride`) is closest to `viewport / 10`, rounded to a "nice" multiple given `binSize × binUnit` (multiples of `60s` for seconds, multiples of `5min` / `15min` / `60min` for minutes, etc.). Helper is pure; vitest covers it.

- **Topology homework during implementation.** Before declaring AC12 done, audit topology's current keyboard + ARIA posture. If the DAG area is not keyboard-navigable today, **don't retrofit it in this milestone** — just record the gap and match m-E21-06's bar going forward.

## Out of Scope

- **Any backend API changes.** `state_window` exists and suffices; no carve-outs needed (contrast with D-2026-04-17-033 for m-E21-03 and D-2026-04-21-034 for m-E21-04).
- **Decomposition view, comparison view, flow-balance view.** All explicitly deferred in the E-21 epic spec; none are touched by this milestone.
- **Pattern encoding / high-contrast / screen-reader polish / focus-indicator visual polish.** All deferred to m-E21-08.
- **Fixed per-metric color ranges.** Deferred (in `work/gaps.md`).
- **Per-row color-scale normalization toggle.** Deferred (in `work/gaps.md`).
- **Current-bin-value sort mode and trend/slope sort mode.** Deferred (in `work/gaps.md`).
- **View-manifest registry / plug-in pattern / runtime view loading.** Deferred (in `work/gaps.md`); introduce when a third asymmetric view lands.
- **User-configurable cell-rendering preferences** beyond the row-stability toggle.
- **Export / snapshot functionality** (PNG / SVG export of the grid).
- **Active-view persistence across page reloads.** Active view resets to Topology on reload. If this becomes a UX ask, extend after shipping.
- **Heatmap virtualization / canvas rendering.** Not needed at E-21 target scale; file a gap if measured performance requires it.
- **Edge-level heatmap** (edges × bins). Current scope is nodes × bins only; edges remain on the DAG.

## Dependencies

- **m-E21-01 Workbench Foundation** (complete) — density system, dag-map events, workbench store, pin-to-inspect.
- **m-E21-02 Metric Selector & Edge Cards** (complete) — metric selector, class filter, data-viz palette, timeline scrubber (the one we will share).
- **Existing `GET /v1/runs/{runId}/state_window`** — `src/FlowTime.API/Program.cs:1028`. No changes required.
- **Existing `flowtime.getStateWindow(...)`** — `ui/src/lib/api/flowtime.ts:101`. Gains an optional `mode` parameter; signature remains backward-compatible.
- **Shared view-state store** — introduced in this milestone at `ui/src/lib/stores/view-state.svelte.ts`. Consumers added in this milestone: view switcher, heatmap grid, topology page (route refactor to consume the store instead of route-local `$state`).

## ADRs

### ADR-m-E21-06-01: View-switcher architecture is a typed component with inline view arrays (no registry, no context API)

**Context.** Heatmap is the first of potentially several "views of the same model" (decomposition, comparison, flow-balance — all out of E-21 scope). There was a choice between (A) a manifest / registry file listing views with a plug-in shape, (B) a Svelte context API wrapping the active view, or (C) a typed `<ViewSwitcher>` component consuming an inline array on the parent route.

**Decision.** Ship option (C). Views are listed inline in `/time-travel/topology/+page.svelte`; the switcher is a dumb typed component. Shared view state lives in a store (`view-state.svelte.ts`), not in a context. Adding a future view = one import + one array entry + one conditional render branch.

**Consequences.** For 2–4 views with similar shape (all use toolbar + scrubber + workbench sidebar), this is the smallest possible shape. When a future view lands with real asymmetry (e.g. a comparison view that needs two runs + no single scrubber), the cost is graduating to a registry — tracked as a gap. No sunk cost in registry plumbing that turned out to be wrong.

**Rejected alternatives.**
- **Manifest / registry file.** Too much upfront indirection for two views.
- **Svelte context API.** Adds hidden wiring; the store is more discoverable.
- **`supports(run)` predicates / runtime view loading.** Solving a problem we don't have.

### ADR-m-E21-06-02: Topology color-scale normalization switches from per-bin to shared full-window 99p-clipped

**Context.** Topology today computes its metric color domain **per bin** — every scrubber step recomputes `[min, max]` over the current bin's nodes. The heatmap needs a stable domain over the full window so that "bright red" is comparable across bins. Leaving topology per-bin and heatmap full-window would make the two views visually disagree at bin T and undermine the "same model, two views" contract.

**Decision.** Both views adopt **shared full-window 99p-clipped normalization**, excluding cells in the no-data or metric-undefined states, and excluding cells filtered out by the active class filter. The single helper `computeSharedColorDomain` owns the computation; topology's existing mapper is refactored to delegate to it.

**Consequences.** Topology's coloring changes visibly — a node that was "relatively hot" in bin T (because bin T's range was small) may now be muted if the full-window range is wider, and vice versa. This is the point: comparability across bins and across views is more valuable than within-bin contrast. Users who want per-bin / per-row normalization will get that as a follow-up toggle (tracked in `work/gaps.md`). The Playwright correctness spec (AC14 #11) proves the color-bucket stability; the vitest suite for `computeSharedColorDomain` proves the math.

**Rejected alternatives.**
- **Keep topology per-bin; let heatmap be full-window.** Breaks cross-view parity — the exact problem heatmap is supposed to solve.
- **Make topology configurable with per-bin vs full-window vs fixed-per-metric.** Deferred. Ship one correct default first; layer toggles on after we have the default in users' hands.

## Confirmations at start (2026-04-23)

Confirmed by user at `/wf-start-milestone` invocation:

1. **ADR-m-E21-06-02 ships as a straight swap** (no `?normalization=per-bin` escape hatch). Topology's coloring visibly changes to match heatmap. Call out in milestone release notes.
2. **Playwright #11 correctness spec uses the `data-value-bucket` assertion only.** Numeric-value correctness is enforced by the `computeSharedColorDomain` vitest suite.
3. **AC12 homework surfaces file as separate gaps** in `work/gaps.md` rather than retrofitting topology's keyboard / ARIA posture or m-E21-02 color-blind validation in this milestone.
4. **Topological sort derivation:** prefer a topo-sorted list from dag-map if one is exposed; otherwise implement a small Kahn's-algorithm helper over `graph.edges` with node-id tiebreak.

